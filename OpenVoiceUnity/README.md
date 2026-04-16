# OpenVoiceSharp for Unity

Vicinity voice chat for Unity MMOs, built on OpenVoiceSharp (Opus, RNNoise, WebRTC VAD)
with FishNet 4.x networking. No separate audio pipeline code — uses OpenVoiceSharp directly.

---

## Step 1 — Install NuGet Dependencies

OpenVoiceSharp needs three NuGet packages. Use NuGetForUnity:

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) from the Package Manager
2. NuGet > Manage NuGet Packages, install all three:
   - `OpusDotNet`
   - `RNNoise.NET`
   - `WebRtcVadSharp`

These provide Opus encoding, RNNoise suppression, and WebRTC VAD respectively.

## Step 2 — Install FishNet

Install FishNet 4.6 from the Unity Asset Store or Package Manager.

## Step 3 — Set Audio Sample Rate

**Edit > Project Settings > Audio > System Sample Rate = 48000**

The included Editor script warns you on compile and offers to open the setting for you.
Run **Tools > OpenVoiceSharp > Validate Setup** at any time to check.

## Step 4 — Configure Your Player Prefab

Add these three components to your player prefab:

| Component | Notes |
|---|---|
| `PlayerVoice` | This package. Configure distance and PTT in the Inspector. |
| `AudioSource` | Required by PlayerVoice. Leave defaults — PlayerVoice configures it. |
| `NetworkObserver` (FishNet) | Add a `DistanceCondition`. Set its range to match `PlayerVoice > MaxDistance`. |

The `DistanceCondition` is your vicinity system. FishNet only sends voice RPCs to players
within range — players outside range receive nothing and pay no CPU cost.

---

## How It Works

```
[Unity Microphone API]  ← MicrophoneCapture.cs (replaces NAudio BasicMicrophoneRecorder)
        ↓  byte[] 16-bit PCM, 20ms frames
[VoiceChatInterface.SubmitAudioData]  ← OpenVoiceSharp (VAD + RNNoise + Opus encode)
        ↓  byte[] Opus packet
[FishNet ServerRpc → ObserversRpc]  ← unreliable channel, distance scoped
        ↓  byte[] Opus packet
[VoiceChatInterface.WhenDataReceived]  ← OpenVoiceSharp (Opus decode)
        ↓  float[] samples
[CircularAudioBuffer<float>]  ← OpenVoiceSharp (18-chunk Unity buffer)
        ↓
[OnAudioFilterRead → AudioSource]  ← Unity handles 3D distance rolloff
```

## Push To Talk vs VAD

- `Push To Talk = true` on PlayerVoice → hold the configured key to transmit
- `Push To Talk = false` → WebRTC VAD built into VoiceChatInterface opens the mic automatically

## Noise Suppression

RNNoise is applied by the sender before encoding. The receiver does not apply it again.
Disable `Enable Noise Suppression` on PlayerVoice for low-spec hardware.

## Tuning the Buffer

`CircularAudioBuffer` is set to `RecommendedChunkAmount.Unity` (18 chunks × 20ms = 360ms).
If you hear crackle, the buffer is running dry — this usually means packet loss or
the sender's frame rate is inconsistent. 360ms is already conservative for an MMO.
