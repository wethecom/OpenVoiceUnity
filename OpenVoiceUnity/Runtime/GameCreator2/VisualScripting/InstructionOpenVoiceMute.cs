using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice Mute")]
    [Description("Sets the mute state of a PlayerVoice target.")]
    [Category("OpenVoice/Mute")]
    [Serializable]
    public class InstructionOpenVoiceMute : Instruction
    {
        [SerializeField] private PropertyGetGameObject player = GetGameObjectSelf.Create();
        [SerializeField] private GameObject playerFallback;
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;
        [SerializeField] private bool muted = true;

        protected override Task Run(Args args)
        {
            PlayerVoice voice = OpenVoiceGc2Resolver.ResolveVoice(player, playerFallback, args, playerVoice, requireOwnership);
            if (voice != null) voice.SetMuted(muted);
            return DefaultResult;
        }
    }
}
