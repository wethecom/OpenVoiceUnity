using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice State")]
    [Description("Checks PlayerVoice state (mute, push-to-talk, forced transmit).")]
    [Category("OpenVoice/Voice State")]
    [Serializable]
    public class ConditionOpenVoiceState : Condition
    {
        private enum VoiceState
        {
            Muted,
            PushToTalkEnabled,
            ForceTransmitEnabled
        }

        [SerializeField] private PropertyGetGameObject player = GetGameObjectSelf.Create();
        [SerializeField] private GameObject playerFallback;
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;
        [SerializeField] private VoiceState state = VoiceState.Muted;
        [SerializeField] private bool expected = true;

        protected override bool Run(Args args)
        {
            PlayerVoice voice = OpenVoiceGc2Resolver.ResolveVoice(player, playerFallback, args, playerVoice, requireOwnership);
            if (voice == null) return false;

            bool current = state switch
            {
                VoiceState.Muted => voice.IsMuted,
                VoiceState.PushToTalkEnabled => voice.IsPushToTalkEnabled,
                VoiceState.ForceTransmitEnabled => voice.IsForceTransmitEnabled,
                _ => false
            };

            return current == expected;
        }
    }
}
