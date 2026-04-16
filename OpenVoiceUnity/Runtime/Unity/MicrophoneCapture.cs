using System;
using System.Collections;
using UnityEngine;

namespace OpenVoiceSharp.Unity
{
    /// <summary>
    /// Unity replacement for BasicMicrophoneRecorder.
    /// Uses Unity's built-in Microphone API instead of NAudio so it works on all Unity platforms.
    ///
    /// Fires the same DataAvailable event as BasicMicrophoneRecorder so VoiceChatInterface
    /// integration is identical. Each callback delivers exactly one 20ms frame (960 samples)
    /// of 16-bit PCM at 48kHz.
    ///
    /// Attach this to any persistent GameObject (e.g. your local player).
    /// </summary>
    public class MicrophoneCapture : MonoBehaviour
    {
        // ── Events (same shape as BasicMicrophoneRecorder) ─────────

        public delegate void MicrophoneDataAvailableEvent(byte[] pcmData, int length);
        public event MicrophoneDataAvailableEvent DataAvailable;

        public delegate void MicrophoneDeviceChangedEvent(int index, string deviceName);
        public event MicrophoneDeviceChangedEvent AudioInputChanged;

        // ── State ──────────────────────────────────────────────────

        public bool IsRecording { get; private set; } = false;
        public string CurrentDevice { get; private set; }
        public int CurrentDeviceIndex { get; private set; } = 0;

        // ── Config ─────────────────────────────────────────────────

        // One 20ms frame at 48kHz = 960 float samples
        private const int FrameSamples = VoiceChatInterface.SampleRate * VoiceChatInterface.FrameLength / 1000;

        // Loopback clip length — 1 second ring buffer is plenty
        private const int ClipLengthSeconds = 1;

        private AudioClip micClip;
        private int lastSamplePosition;

        // Reusable buffers — allocated once, never in the hot path
        private readonly float[] frameFloat = new float[FrameSamples];
        private readonly short[] frameShort = new short[FrameSamples];
        private readonly byte[] frameBytes = new byte[FrameSamples * 2]; // 16-bit = 2 bytes per sample

        // ── Device Management ──────────────────────────────────────

        public static string[] GetMicrophones() => Microphone.devices;

        public void SetMicrophone(int index)
        {
            string[] devices = GetMicrophones();
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning("[MicrophoneCapture] No microphone devices found.");
                return;
            }

            index = Mathf.Clamp(index, 0, devices.Length - 1);
            CurrentDevice = devices[index];
            CurrentDeviceIndex = index;
            AudioInputChanged?.Invoke(index, CurrentDevice);

            // Restart if already recording so the new device takes effect
            if (IsRecording)
            {
                StopRecording();
                StartRecording();
            }
        }

        public void SetToDefaultMicrophone() => SetMicrophone(0);

        // ── Recording Control ──────────────────────────────────────

        public void StartRecording()
        {
            if (IsRecording) return;

            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicrophoneCapture] Cannot start recording — no microphones available.");
                return;
            }

            if (string.IsNullOrEmpty(CurrentDevice))
                SetToDefaultMicrophone();

            micClip = Microphone.Start(CurrentDevice, loop: true, lengthSec: ClipLengthSeconds, frequency: VoiceChatInterface.SampleRate);

            // Wait until the microphone is actually recording before tracking position
            // (Microphone.Start returns immediately but position stays 0 briefly)
            StartCoroutine(WaitForMicStart());
        }

        private IEnumerator WaitForMicStart()
        {
            while (Microphone.GetPosition(CurrentDevice) <= 0)
                yield return null;

            lastSamplePosition = Microphone.GetPosition(CurrentDevice);
            IsRecording = true;
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            Microphone.End(CurrentDevice);
            micClip = null;
        }

        // ── Frame Polling ──────────────────────────────────────────

        // Unity does not push mic data via events like NAudio does.
        // We poll every Update, slice out complete 20ms frames, and fire DataAvailable
        // for each — so callers see the exact same event-driven pattern.
        private void Update()
        {
            if (!IsRecording || micClip == null) return;

            int currentPosition = Microphone.GetPosition(CurrentDevice);
            int totalSamples = micClip.samples; // = SampleRate * ClipLengthSeconds

            // How many new samples have arrived since last poll?
            int newSamples = currentPosition >= lastSamplePosition
                ? currentPosition - lastSamplePosition
                : totalSamples - lastSamplePosition + currentPosition; // wrapped around

            // Dispatch every complete 20ms frame we have
            while (newSamples >= FrameSamples)
            {
                // Read one frame from the looping clip
                micClip.GetData(frameFloat, lastSamplePosition);

                // float[] → short[] → byte[]
                FloatToBytes(frameFloat, frameBytes);

                DataAvailable?.Invoke(frameBytes, frameBytes.Length);

                lastSamplePosition = (lastSamplePosition + FrameSamples) % totalSamples;
                newSamples -= FrameSamples;
            }
        }

        // ── Helpers ────────────────────────────────────────────────

        // Converts Unity float samples to 16-bit little-endian PCM bytes
        // in the same format VoiceChatInterface.SubmitAudioData expects.
        private void FloatToBytes(float[] input, byte[] output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                short s = (short)(Mathf.Clamp(input[i], -1f, 1f) * short.MaxValue);
                output[i * 2]     = (byte)(s & 0xFF);
                output[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
        }

        // ── Lifecycle ──────────────────────────────────────────────

        private void OnDestroy()
        {
            StopRecording();
        }
    }
}
