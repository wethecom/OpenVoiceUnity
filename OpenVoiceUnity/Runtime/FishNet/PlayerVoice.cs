using System;
using System.Buffers;
using System.Collections.Concurrent;
using FishNet.Object;
using FishNet.Transporting;
using OpenVoiceSharp;
using UnityEngine;

namespace OpenVoiceSharp.Unity
{
    /// <summary>
    /// Add this to your player prefab alongside an AudioSource.
    ///
    /// SETUP CHECKLIST:
    ///   1. Add PlayerVoice and AudioSource to your player prefab
    ///   2. Add NetworkObserver component → add DistanceCondition, set its range to match MaxDistance
    ///   3. Edit > Project Settings > Audio > System Sample Rate = 48000
    ///      (the Editor validator will warn you if this is wrong)
    ///
    /// HOW IT WORKS:
    ///   Owner:    MicrophoneCapture → VoiceChatInterface.SubmitAudioData (encode + VAD + noise suppression)
    ///             → ServerRpc (unreliable) → ObserversRpc (unreliable, distance scoped by FishNet)
    ///   Receiver: VoiceChatInterface.WhenDataReceived (decode) → CircularAudioBuffer
    ///             → OnAudioFilterRead → Unity AudioSource (handles distance rolloff)
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerVoice : NetworkBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────

        [Header("Distance")]
        [Tooltip("Players beyond this range won't hear you. Match this to the DistanceCondition on NetworkObserver.")]
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private float minDistance = 2f;

        [Header("Input")]
        [Tooltip("When true, hold PushToTalkKey to transmit. When false, VAD opens the mic automatically.")]
        [SerializeField] private bool pushToTalk = false;
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        [Header("Audio Quality")]
        [Tooltip("Opus bitrate in bps. 16000 = 16kbps, fine for voice. Raise to 24000 for richer audio.")]
        [SerializeField] private int bitrate = VoiceChatInterface.DefaultBitrate;
        [Tooltip("Applies RNNoise suppression on the sender. Disable on low-spec hardware.")]
        [SerializeField] private bool enableNoiseSuppression = true;

        // ── Encode pipeline (owner only) ───────────────────────────

        private VoiceChatInterface encoder;
        private MicrophoneCapture micCapture;

        // NAudio-style events fire on Unity's main thread (MicrophoneCapture polls in Update).
        // We still queue to decouple capture timing from send timing cleanly.
        private readonly ConcurrentQueue<(byte[] data, int length)> micQueue = new();

        // ── Decode pipeline (all remote instances) ─────────────────

        private VoiceChatInterface decoder;

        // Thread-safe queue between network decode and Unity audio thread.
        // Buffers are rented from ArrayPool to avoid per-packet GC allocations.
        private readonly ConcurrentQueue<QueuedSamples> playbackQueue = new();
        private QueuedSamples activePlayback;
        private int activePlaybackIndex;
        private bool hasActivePlayback;

        private const int MaxQueuedFrames = 64;

        private struct QueuedSamples
        {
            public float[] Buffer;
            public int Length;
        }

        private AudioSource audioSource;

        // ── FishNet lifecycle ──────────────────────────────────────

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (AudioSettings.outputSampleRate != VoiceChatInterface.SampleRate)
                Debug.LogWarning($"[PlayerVoice] Unity audio output is {AudioSettings.outputSampleRate} Hz " +
                                 $"but OpenVoiceSharp requires 48000 Hz. " +
                                 $"Fix: Edit > Project Settings > Audio > System Sample Rate = 48000");

            SetupPlayback();

            if (IsOwner)
                SetupCapture();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (IsOwner && micCapture != null)
            {
                micCapture.DataAvailable -= OnMicDataAvailable;
                micCapture.StopRecording();
            }

            FlushPlaybackQueue();
        }

        // ── Setup ──────────────────────────────────────────────────

        private void SetupPlayback()
        {
            // One VoiceChatInterface per remote speaker for decode — no encoder needed on this side.
            // EnableNoiseSuppression = false on the decoder: noise suppression was already applied by the sender.
            decoder = new VoiceChatInterface(enableNoiseSuppression: false);

            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 1f;                        // full 3D so distance rolloff works
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.loop = true;

            // Silent looping clip — keeps the AudioSource ticking so OnAudioFilterRead is called every DSP frame.
            // We replace the silence with real voice data inside OnAudioFilterRead.
            audioSource.clip = AudioClip.Create(
                "voice_stream",
                VoiceChatInterface.SampleRate,
                channels: 1,
                VoiceChatInterface.SampleRate,
                stream: false
            );
            audioSource.Play();
        }

        private void SetupCapture()
        {
            encoder = new VoiceChatInterface(bitrate: bitrate, enableNoiseSuppression: enableNoiseSuppression);

            micCapture = gameObject.AddComponent<MicrophoneCapture>();
            micCapture.DataAvailable += OnMicDataAvailable;
            micCapture.StartRecording();
        }

        // ── Capture ────────────────────────────────────────────────

        // MicrophoneCapture fires this on Unity's main thread (it polls in Update).
        private void OnMicDataAvailable(byte[] pcmData, int length)
        {
            byte[] copy = new byte[length];
            Array.Copy(pcmData, copy, length);
            micQueue.Enqueue((copy, length));
        }

        private void Update()
        {
            if (!IsOwner) return;

            while (micQueue.TryDequeue(out var item))
            {
                // Gate: push to talk key OR voice activity detection built into VoiceChatInterface
                if (pushToTalk && !Input.GetKey(pushToTalkKey)) continue;
                if (!pushToTalk && !encoder.IsSpeaking(item.data)) continue;

                var (encoded, encodedLength) = encoder.SubmitAudioData(item.data, item.length);

                // Trim to actual encoded length before sending over the network
                byte[] packet = new byte[encodedLength];
                Array.Copy(encoded, packet, encodedLength);

                SendVoiceToServer(packet);
            }
        }

        // ── Network ────────────────────────────────────────────────

        // Owner → Server, unreliable (drop packets rather than delay)
        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void SendVoiceToServer(byte[] packet, Channel channel = Channel.Unreliable)
        {
            BroadcastVoiceToObservers(packet);
        }

        // Server → all observers in vicinity, unreliable.
        // FishNet's DistanceCondition on NetworkObserver controls who receives this —
        // that IS your vicinity system. Players out of range are not observers and get nothing.
        [ObserversRpc(ExcludeOwner = true, RunLocally = false)]
        private void BroadcastVoiceToObservers(byte[] packet, Channel channel = Channel.Unreliable)
        {
            if (decoder == null) return;

            var (decoded, decodedLength) = decoder.WhenDataReceived(packet, packet.Length);
            if (decodedLength <= 0) return;

            // WhenDataReceived returns 16-bit PCM bytes.
            // decodedLength bytes / 2 = sample count (16-bit = 2 bytes per sample).
            int sampleCount = decodedLength / 2;
            float[] rented = ArrayPool<float>.Shared.Rent(sampleCount);

            // Convert only the valid decoded samples into the rented buffer.
            for (int i = 0; i < sampleCount; i++)
            {
                short s = BitConverter.ToInt16(decoded, i * 2);
                rented[i] = s / 32768f;
            }

            playbackQueue.Enqueue(new QueuedSamples
            {
                Buffer = rented,
                Length = sampleCount
            });

            // Avoid unbounded latency/memory growth when receivers fall behind.
            while (playbackQueue.Count > MaxQueuedFrames)
            {
                if (!playbackQueue.TryDequeue(out QueuedSamples dropped))
                    break;
                ArrayPool<float>.Shared.Return(dropped.Buffer);
            }
        }

        // ── Audio Thread ───────────────────────────────────────────

        // Unity calls this on its audio thread every DSP tick.
        // We drain the CircularAudioBuffer into Unity's output buffer.
        // AudioSource handles 3D distance rolloff automatically.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            int sampleCount = data.Length / channels;

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = 0f;

                if (!hasActivePlayback)
                {
                    if (playbackQueue.TryDequeue(out QueuedSamples next))
                    {
                        activePlayback = next;
                        activePlaybackIndex = 0;
                        hasActivePlayback = true;
                    }
                }

                if (hasActivePlayback)
                {
                    sample = activePlayback.Buffer[activePlaybackIndex++];
                    if (activePlaybackIndex >= activePlayback.Length)
                    {
                        ArrayPool<float>.Shared.Return(activePlayback.Buffer);
                        activePlayback = default;
                        activePlaybackIndex = 0;
                        hasActivePlayback = false;
                    }
                }

                for (int c = 0; c < channels; c++)
                    data[i * channels + c] = sample;
            }
        }

        private void FlushPlaybackQueue()
        {
            while (playbackQueue.TryDequeue(out QueuedSamples queued))
                ArrayPool<float>.Shared.Return(queued.Buffer);

            if (hasActivePlayback)
            {
                ArrayPool<float>.Shared.Return(activePlayback.Buffer);
                activePlayback = default;
                activePlaybackIndex = 0;
                hasActivePlayback = false;
            }
        }
    }
}
