using System;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("On OpenVoice Speaking Started")]
    [Description("Executes when a PlayerVoice starts speaking/transmitting.")]
    [Category("OpenVoice/On Speaking Started")]
    [Serializable]
    public class EventOnOpenVoiceSpeakingStarted : Event
    {
        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;

        private bool initialized;
        private bool previousSpeaking;

        protected override void OnStart(Trigger trigger)
        {
            base.OnStart(trigger);
            PlayerVoice voice = ResolveVoice(trigger);
            if (voice == null)
            {
                initialized = false;
                return;
            }

            previousSpeaking = voice.IsSpeaking;
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

            bool current = voice.IsSpeaking;
            if (!initialized)
            {
                previousSpeaking = current;
                initialized = true;
                return;
            }

            if (!previousSpeaking && current)
                _ = trigger.Execute(this.Self);

            previousSpeaking = current;
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
