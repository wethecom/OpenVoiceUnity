using System;
using System.Collections.Concurrent;
using FishNet.Object;
using FishNet.Transporting;
using OpenVoiceSharp;
using UnityEngine;

namespace OpenVoiceSharp.FishNet
{
    /// <summary>
    /// Add this component to your player prefab.
    /// Also add an AudioSource to the same GameObject — the distance rolloff
    /// is handled entirely by Unity's built-in AudioSource settings.
    ///
    /// IMPORTANT: Set Unity's audio output sample rate to 48000 Hz.
    ///            Edit > Project Settings > Audio > System Sample Rate = 48000
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerVoice : NetworkBehaviour
    {
        [Header("Distance")]
        [Tooltip("Full volume within this radius (Unity units)")]
        [SerializeField] private float minDistance = 2f;
        [Tooltip("Silent beyond this radius (Unity units)")]
        [SerializeField] private float maxDistance = 25f;

        [Header("Input")]
        [Tooltip("Hold a key to transmit instead of using voice activity detection")]
        [SerializeField] private bool pushToTalk = false;
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        // ── Capture pipeline (owner only) ──────────────────────────
        private BasicMicrophoneRecorder micRecorder;
        private VoiceChatInterface encoder;

        // NAudio fires DataAvailable on its own thread.
        // We queue the data and process it in Update() on the main thread
        // so FishNet RPCs are always called from the main thread.
        private readonly ConcurrentQueue<(byte[] data, int length)> micQueue = new();

        // ── Playback pipeline (all remote instances) ───────────────
        private VoiceChatInterface decoder;
        private VoicePlaybackBuffer playbackBuffer;
        private float[] monoReadBuffer;
        private AudioSource audioSource;

        // ── Lifecycle ──────────────────────────────────────────────

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (AudioSettings.outputSampleRate != VoiceChatInterface.SampleRate)
                Debug.LogWarning($"[PlayerVoice] Unity audio output is {AudioSettings.outputSampleRate} Hz. " +
                                 $"OpenVoiceSharp requires 48000 Hz. " +
                                 $"Fix this in Edit > Project Settings > Audio > System Sample Rate.");

            SetupPlayback();

            if (IsOwner)
                SetupCapture();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (IsOwner && micRecorder != null)
            {
                micRecorder.DataAvailable -= OnMicDataAvailable;
                micRecorder.StopRecording();
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            while (micQueue.TryDequeue(out var item))
            {
                // gate: push to talk or voice activity detection
                if (pushToTalk && !Input.GetKey(pushToTalkKey)) continue;
                if (!pushToTalk && !encoder.IsSpeaking(item.data)) continue;

                var (encoded, encodedLength) = encoder.SubmitAudioData(item.data, item.length);

                // trim to actual encoded length before sending
                byte[] packet = new byte[encodedLength];
                Array.Copy(encoded, packet, encodedLength);

                SendVoiceToServer(packet);
            }
        }

        // ── Setup ──────────────────────────────────────────────────

        private void SetupPlayback()
        {
            decoder = new VoiceChatInterface();

            // 1 second of float samples at 48kHz is plenty of headroom
            playbackBuffer = new VoicePlaybackBuffer(VoiceChatInterface.SampleRate);

            // 4096 covers the largest DSP buffer Unity will ever ask for in OnAudioFilterRead
            monoReadBuffer = new float[4096];

            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 1f;                          // full 3D so distance rolloff applies
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.loop = true;

            // A silent looping clip keeps the AudioSource active so OnAudioFilterRead is called.
            // We inject real voice data in OnAudioFilterRead below.
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
            encoder = new VoiceChatInterface();
            micRecorder = new BasicMicrophoneRecorder();
            micRecorder.DataAvailable += OnMicDataAvailable;
            micRecorder.StartRecording();
        }

        // ── Capture ────────────────────────────────────────────────

        // Fires on NAudio's background thread — copy the buffer and queue it.
        private void OnMicDataAvailable(byte[] pcmData, int length)
        {
            byte[] copy = new byte[length];
            Array.Copy(pcmData, copy, length);
            micQueue.Enqueue((copy, length));
        }

        // ── Network ────────────────────────────────────────────────

        // Client → Server (unreliable, owner only)
        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void SendVoiceToServer(byte[] packet, Channel channel = Channel.Unreliable)
        {
            BroadcastVoiceToObservers(packet);
        }

        // Server → all observers in range except the speaker (unreliable)
        // FishNet's DistanceCondition on the NetworkObject's NetworkObserver
        // component controls who is an observer — that IS your vicinity system.
        [ObserversRpc(ExcludeOwner = true, RunLocally = false)]
        private void BroadcastVoiceToObservers(byte[] packet, Channel channel = Channel.Unreliable)
        {
            var (decoded, decodedLength) = decoder.WhenDataReceived(packet, packet.Length);

            // decodedLength bytes → decodedLength/2 float samples (16-bit PCM = 2 bytes per sample)
            float[] floatSamples = new float[decodedLength / 2];
            VoiceUtilities.Convert16BitToFloat(decoded, floatSamples);

            playbackBuffer.Write(floatSamples, floatSamples.Length);
        }

        // ── Audio Thread ───────────────────────────────────────────

        // Unity calls this on the audio thread each DSP tick.
        // data is interleaved: L0,R0,L1,R1... so we fill every channel with the same mono sample.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            int sampleCount = data.Length / channels;
            int read = playbackBuffer.Read(monoReadBuffer, sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = i < read ? monoReadBuffer[i] : 0f;
                for (int c = 0; c < channels; c++)
                    data[i * channels + c] = sample;
            }
        }
    }

    /// <summary>
    /// Thread-safe float ring buffer.
    /// Written from Unity's main thread (RPC callbacks), read from Unity's audio thread.
    /// </summary>
    internal class VoicePlaybackBuffer
    {
        private readonly float[] buffer;
        private int writePos;
        private int readPos;
        private int available;
        private readonly int capacity;
        private readonly object syncLock = new();

        public VoicePlaybackBuffer(int capacity)
        {
            this.capacity = capacity;
            buffer = new float[capacity];
        }

        public void Write(float[] samples, int count)
        {
            lock (syncLock)
            {
                for (int i = 0; i < count; i++)
                {
                    if (available >= capacity) break; // drop silently if full — no overflow
                    buffer[writePos] = samples[i];
                    writePos = (writePos + 1) % capacity;
                    available++;
                }
            }
        }

        public int Read(float[] output, int count)
        {
            lock (syncLock)
            {
                int read = Math.Min(count, available);
                for (int i = 0; i < read; i++)
                {
                    output[i] = buffer[readPos];
                    readPos = (readPos + 1) % capacity;
                    available--;
                }
                return read;
            }
        }
    }
}
