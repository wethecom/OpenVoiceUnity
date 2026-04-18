using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice Set Indicator")]
    [Description("Sets a UI/scene indicator active state from PlayerVoice state.")]
    [Category("OpenVoice/UI Indicator")]
    [Serializable]
    public class InstructionOpenVoiceSetIndicator : Instruction
    {
        private enum VoiceState
        {
            Muted,
            PushToTalkEnabled,
            ForceTransmitEnabled,
            Speaking
        }

        [SerializeField] private PropertyGetGameObject player = GetGameObjectSelf.Create();
        [SerializeField] private GameObject playerFallback;
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;

        [SerializeField] private PropertyGetGameObject indicator = GetGameObjectSelf.Create();
        [SerializeField] private GameObject indicatorFallback;
        [SerializeField] private GameObject indicatorObject;

        [SerializeField] private VoiceState state = VoiceState.Muted;
        [SerializeField] private bool invert;

        protected override Task Run(Args args)
        {
            PlayerVoice voice = OpenVoiceGc2Resolver.ResolveVoice(player, playerFallback, args, playerVoice, requireOwnership);
            GameObject go = ResolveIndicator(args);
            if (voice == null || go == null) return DefaultResult;

            bool active = ReadState(voice);
            if (invert) active = !active;
            go.SetActive(active);

            return DefaultResult;
        }

        private GameObject ResolveIndicator(Args args)
        {
            return OpenVoiceGc2Resolver.ResolveGameObject(indicator, indicatorFallback, args, indicatorObject);
        }

        private bool ReadState(PlayerVoice voice)
        {
            return state switch
            {
                VoiceState.Muted => voice.IsMuted,
                VoiceState.PushToTalkEnabled => voice.IsPushToTalkEnabled,
                VoiceState.ForceTransmitEnabled => voice.IsForceTransmitEnabled,
                VoiceState.Speaking => voice.IsSpeaking,
                _ => false
            };
        }
    }
}
