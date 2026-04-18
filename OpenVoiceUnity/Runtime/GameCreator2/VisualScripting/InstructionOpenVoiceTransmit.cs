using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice Force Transmit")]
    [Description("Starts or stops forced transmit for a PlayerVoice target.")]
    [Category("OpenVoice/Transmit")]
    [Serializable]
    public class InstructionOpenVoiceTransmit : Instruction
    {
        [SerializeField] private PropertyGetGameObject player = GetGameObjectSelf.Create();
        [SerializeField] private GameObject playerFallback;
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;
        [SerializeField] private bool transmitting = true;

        protected override Task Run(Args args)
        {
            PlayerVoice voice = OpenVoiceGc2Resolver.ResolveVoice(player, playerFallback, args, playerVoice, requireOwnership);
            if (voice != null) voice.SetForceTransmit(transmitting);
            return DefaultResult;
        }
    }
}
