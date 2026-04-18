using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice Push To Talk")]
    [Description("Enables or disables push-to-talk for a PlayerVoice target.")]
    [Category("OpenVoice/Push To Talk")]
    [Serializable]
    public class InstructionOpenVoicePushToTalk : Instruction
    {
        [SerializeField] private PropertyGetGameObject player = GetGameObjectSelf.Create();
        [SerializeField] private GameObject playerFallback;
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;
        [SerializeField] private bool pushToTalkEnabled = true;

        protected override Task Run(Args args)
        {
            PlayerVoice voice = OpenVoiceGc2Resolver.ResolveVoice(player, playerFallback, args, playerVoice, requireOwnership);
            if (voice != null) voice.SetPushToTalk(pushToTalkEnabled);
            return DefaultResult;
        }
    }
}
