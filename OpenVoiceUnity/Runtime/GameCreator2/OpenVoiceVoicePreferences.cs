using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2
{
    /// <summary>
    /// Persists basic voice preferences using PlayerPrefs.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OpenVoiceSharp/OpenVoice Voice Preferences")]
    public sealed class OpenVoiceVoicePreferences : MonoBehaviour
    {
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool autoFindOnAwake = true;
        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private string keyPrefix = "openvoice.";

        private string KeyMute => $"{keyPrefix}muted";
        private string KeyPtt => $"{keyPrefix}ptt";
        private string KeyTransmit => $"{keyPrefix}transmit";

        private void Awake()
        {
            if (autoFindOnAwake && playerVoice == null)
                playerVoice = GetComponent<PlayerVoice>();

            if (loadOnAwake)
                Load();
        }

        public void SetPlayerVoice(PlayerVoice target) => playerVoice = target;
        public PlayerVoice GetPlayerVoice() => playerVoice;

        public void Save()
        {
            if (playerVoice == null) return;

            PlayerPrefs.SetInt(KeyMute, playerVoice.IsMuted ? 1 : 0);
            PlayerPrefs.SetInt(KeyPtt, playerVoice.IsPushToTalkEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KeyTransmit, playerVoice.IsForceTransmitEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            if (playerVoice == null) return;

            if (PlayerPrefs.HasKey(KeyMute))
                playerVoice.SetMuted(PlayerPrefs.GetInt(KeyMute) != 0);
            if (PlayerPrefs.HasKey(KeyPtt))
                playerVoice.SetPushToTalk(PlayerPrefs.GetInt(KeyPtt) != 0);
            if (PlayerPrefs.HasKey(KeyTransmit))
                playerVoice.SetForceTransmit(PlayerPrefs.GetInt(KeyTransmit) != 0);
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(KeyMute);
            PlayerPrefs.DeleteKey(KeyPtt);
            PlayerPrefs.DeleteKey(KeyTransmit);
            PlayerPrefs.Save();
        }
    }
}
