using System;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("On OpenVoice Speaking Stopped")]
    [Description("Executes when a PlayerVoice stops speaking/transmitting.")]
    [Category("OpenVoice/On Speaking Stopped")]
    [Serializable]
    public class EventOnOpenVoiceSpeakingStopped : Event
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

            if (previousSpeaking && !current)
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
