using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2
{
    /// <summary>
    /// Optional bridge layer for Game Creator 2 workflows.
    /// Exposes simple method endpoints so GC2 Instructions/Events can control voice behavior.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OpenVoiceSharp/Game Creator 2 Voice Bridge")]
    public sealed class GameCreator2VoiceBridge : MonoBehaviour
    {
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool autoFindOnAwake = true;

        private void Awake()
        {
            if (autoFindOnAwake && playerVoice == null)
                playerVoice = GetComponent<PlayerVoice>();
        }

        public void SetPlayerVoice(PlayerVoice target) => playerVoice = target;
        public PlayerVoice GetPlayerVoice() => playerVoice;

        // ── Mute ───────────────────────────────────────────────────
        public void Mute() => playerVoice?.Mute();
        public void Unmute() => playerVoice?.Unmute();
        public void ToggleMute() => playerVoice?.ToggleMuted();
        public void SetMuted(bool value) => playerVoice?.SetMuted(value);

        // ── Push-To-Talk ───────────────────────────────────────────
        public void EnablePushToTalk() => playerVoice?.SetPushToTalk(true);
        public void DisablePushToTalk() => playerVoice?.SetPushToTalk(false);
        public void TogglePushToTalk() => playerVoice?.TogglePushToTalk();
        public void SetPushToTalk(bool value) => playerVoice?.SetPushToTalk(value);

        // Integer-based key setter for event systems that pass primitive params.
        public void SetPushToTalkKey(int keyCode)
        {
            if (playerVoice == null) return;
            playerVoice.SetPushToTalkKey((KeyCode)keyCode);
        }

        // ── Transmission Override ──────────────────────────────────
        public void StartTransmit() => playerVoice?.BeginForceTransmit();
        public void StopTransmit() => playerVoice?.EndForceTransmit();
        public void SetTransmit(bool value) => playerVoice?.SetForceTransmit(value);

        // ── State getters (useful for Conditions/Debug UI) ────────
        public bool IsMuted() => playerVoice != null && playerVoice.IsMuted;
        public bool IsPushToTalkEnabled() => playerVoice != null && playerVoice.IsPushToTalkEnabled;
        public bool IsTransmitForced() => playerVoice != null && playerVoice.IsForceTransmitEnabled;
        public int GetPushToTalkKey() => playerVoice == null ? (int)KeyCode.None : (int)playerVoice.PushToTalkKey;
    }
}
