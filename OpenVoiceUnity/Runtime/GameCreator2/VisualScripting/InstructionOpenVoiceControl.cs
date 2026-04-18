using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice Control")]
    [Description("Controls PlayerVoice state (mute, push-to-talk, forced transmit).")]
    [Category("OpenVoice/Voice Control")]
    [Serializable]
    public class InstructionOpenVoiceControl : Instruction
    {
        private enum VoiceAction
        {
            Mute,
            Unmute,
            ToggleMute,
            EnablePushToTalk,
            DisablePushToTalk,
            TogglePushToTalk,
            StartTransmit,
            StopTransmit
        }

        [SerializeField] private PropertyGetGameObject player = GetGameObjectSelf.Create();
        [SerializeField] private GameObject playerFallback;
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;
        [SerializeField] private VoiceAction action = VoiceAction.ToggleMute;

        protected override Task Run(Args args)
        {
            PlayerVoice voice = OpenVoiceGc2Resolver.ResolveVoice(player, playerFallback, args, playerVoice, requireOwnership);
            if (voice == null) return DefaultResult;

            switch (action)
            {
                case VoiceAction.Mute: voice.Mute(); break;
                case VoiceAction.Unmute: voice.Unmute(); break;
                case VoiceAction.ToggleMute: voice.ToggleMuted(); break;
                case VoiceAction.EnablePushToTalk: voice.SetPushToTalk(true); break;
                case VoiceAction.DisablePushToTalk: voice.SetPushToTalk(false); break;
                case VoiceAction.TogglePushToTalk: voice.TogglePushToTalk(); break;
                case VoiceAction.StartTransmit: voice.BeginForceTransmit(); break;
                case VoiceAction.StopTransmit: voice.EndForceTransmit(); break;
            }

            return DefaultResult;
        }
    }
}
