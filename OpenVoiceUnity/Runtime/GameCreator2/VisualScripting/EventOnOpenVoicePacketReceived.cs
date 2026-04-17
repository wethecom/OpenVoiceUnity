using System;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("On OpenVoice Packet Received")]
    [Description("Executes when a PlayerVoice receives a decoded voice packet.")]
    [Category("OpenVoice/On Packet Received")]
    [Serializable]
    public class EventOnOpenVoicePacketReceived : Event
    {
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = false;

        private bool initialized;
        private int previousPacketCount;

        protected override void OnStart(Trigger trigger)
        {
            base.OnStart(trigger);
            PlayerVoice voice = ResolveVoice(trigger);
            if (voice == null)
            {
                initialized = false;
                return;
            }

            previousPacketCount = voice.ReceivedPacketCount;
            initialized = true;
        }

        protected override void OnUpdate(Trigger trigger)
        {
            base.OnUpdate(trigger);
            PlayerVoice voice = ResolveVoice(trigger);
            if (voice == null)
            {
                initialized = false;
                return;
            }

            int current = voice.ReceivedPacketCount;
            if (!initialized)
            {
                previousPacketCount = current;
                initialized = true;
                return;
            }

            if (current > previousPacketCount)
                _ = trigger.Execute(this.Self);

            previousPacketCount = current;
        }

        private PlayerVoice ResolveVoice(Trigger trigger)
        {
            if (playerVoice != null && OpenVoiceGc2Resolver.IsAllowed(playerVoice, requireOwnership))
                return playerVoice;

            if (trigger != null && trigger.TryGetComponent(out PlayerVoice triggerVoice) &&
                OpenVoiceGc2Resolver.IsAllowed(triggerVoice, requireOwnership))
                return triggerVoice;

            return null;
        }
    }
}
