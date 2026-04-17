using System;
using System.Collections.Generic;
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
    ///   Receiver: VoiceChatInterface.WhenDataReceived (decode) → VoicePlaybackBuffer
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

        // Session-style playback map; this implementation uses one speaker per PlayerVoice instance.
        private readonly Dictionary<Guid, VoicePlaybackBuffer> speakerPlayback = new();
        private readonly object speakerPlaybackLock = new();
        private readonly Guid localSpeakerId = Guid.NewGuid();
        private byte[] audioReadBytes = Array.Empty<byte>();
        private float[] audioReadFloats = Array.Empty<float>();

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

            encoder?.Dispose();
            encoder = null;
            decoder?.Dispose();
            decoder = null;

            foreach (Guid speakerId in GetSpeakersWithPlayback())
                FlushSpeakerPlayback(speakerId);
        }

        // ── Setup ──────────────────────────────────────────────────

        private void SetupPlayback()
        {
            // One VoiceChatInterface per remote speaker for decode — no encoder needed on this side.
            // EnableNoiseSuppression = false on the decoder: noise suppression was already applied by the sender.
            decoder = new VoiceChatInterface(enableNoiseSuppression: false);
            lock (speakerPlaybackLock)
            {
                if (!speakerPlayback.ContainsKey(localSpeakerId))
                    speakerPlayback[localSpeakerId] = new VoicePlaybackBuffer();
            }

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
            try
            {
                micCapture.StartRecording();
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogWarning($"[PlayerVoice] Capture unavailable: {ex.Message}");
            }
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
                if (encoder == null)
                    break;

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

            VoicePlaybackBuffer pb;
            lock (speakerPlaybackLock)
            {
                if (!speakerPlayback.TryGetValue(localSpeakerId, out pb))
                {
                    pb = new VoicePlaybackBuffer();
                    speakerPlayback[localSpeakerId] = pb;
                }
            }

            pb.Enqueue(decoded, decodedLength);
        }

        // ── Audio Thread ───────────────────────────────────────────

        // Unity calls this on its audio thread every DSP tick.
        // We pull a fixed PCM16 byte count from VoicePlaybackBuffer into Unity's output buffer.
        // AudioSource handles 3D distance rolloff automatically.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            int sampleCount = data.Length / channels;
            int requestedBytes = sampleCount * 2; // mono PCM16
            EnsureAudioScratchCapacity(sampleCount, requestedBytes);

            ReadSpeakerPlayback(localSpeakerId, audioReadBytes, requestedBytes);
            int conversionLength = requestedBytes;
            if ((conversionLength & 1) != 0)
                conversionLength--;
            VoiceUtilities.Convert16BitToFloat(audioReadBytes, audioReadFloats, conversionLength);

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = audioReadFloats[i];
                for (int c = 0; c < channels; c++)
                    data[i * channels + c] = sample;
            }
        }

        public int ReadSpeakerPlayback(Guid speakerId, byte[] destination, int requestedBytes, int destinationOffset = 0)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));

            VoicePlaybackBuffer pb;
            lock (speakerPlaybackLock)
            {
                if (!speakerPlayback.TryGetValue(speakerId, out pb))
                {
                    if (requestedBytes > 0)
                        Array.Clear(destination, destinationOffset, requestedBytes);
                    return 0;
                }
            }

            return pb.ReadAndFillSilence(destination, requestedBytes, destinationOffset);
        }

        public void FlushSpeakerPlayback(Guid speakerId)
        {
            VoicePlaybackBuffer pb;
            lock (speakerPlaybackLock)
            {
                if (!speakerPlayback.TryGetValue(speakerId, out pb))
                    return;
            }

            pb.Flush();
        }

        /// <summary>
        /// Reads any currently buffered speaker bytes (up to <paramref name="maxBytes"/>)
        /// without zero-fill. Useful for "play tail once, then stop" flows.
        /// </summary>
        public int DrainSpeakerPlayback(Guid speakerId, byte[] destination, int maxBytes, int destinationOffset = 0)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));

            VoicePlaybackBuffer pb;
            lock (speakerPlaybackLock)
            {
                if (!speakerPlayback.TryGetValue(speakerId, out pb))
                    return 0;
            }

            return pb.ReadAvailable(destination, maxBytes, destinationOffset);
        }

        /// <summary>
        /// Drains available speaker tail bytes once, then flushes any remainder.
        /// Returns number of bytes drained.
        /// </summary>
        public int DrainAndFlushSpeakerPlayback(Guid speakerId, byte[] destination, int maxBytes, int destinationOffset = 0)
        {
            int drained = DrainSpeakerPlayback(speakerId, destination, maxBytes, destinationOffset);
            FlushSpeakerPlayback(speakerId);
            return drained;
        }

        public IReadOnlyCollection<Guid> GetSpeakersWithPlayback()
        {
            Guid[] ids;
            lock (speakerPlaybackLock)
            {
                ids = new Guid[speakerPlayback.Count];
                speakerPlayback.Keys.CopyTo(ids, 0);
            }
            return ids;
        }

        private void EnsureAudioScratchCapacity(int sampleCount, int requestedBytes)
        {
            if (audioReadFloats.Length < sampleCount)
                audioReadFloats = new float[sampleCount];
            if (audioReadBytes.Length < requestedBytes)
                audioReadBytes = new byte[requestedBytes];
        }
    }
}
