using System;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("On OpenVoice State Change")]
    [Description("Executes when the selected PlayerVoice state changes.")]
    [Category("OpenVoice/On Voice State Change")]
    [Serializable]
    public class EventOnOpenVoiceStateChange : Event
    {
        private enum VoiceState
        {
            Muted,
            PushToTalkEnabled,
            ForceTransmitEnabled
        }

        [SerializeField] private PlayerVoice playerVoice;
        [SerializeField] private bool requireOwnership = true;
        [SerializeField] private VoiceState state = VoiceState.Muted;
        [SerializeField] private bool triggerWhenTrue = true;
        [SerializeField] private bool triggerWhenFalse = true;

        private bool initialized;
        private bool previous;

        protected override void OnStart(Trigger trigger)
        {
            base.OnStart(trigger);

            PlayerVoice voice = ResolveVoice(trigger);
            if (voice == null)
            {
                initialized = false;
                return;
            }

            previous = ReadState(voice);
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

            bool current = ReadState(voice);
            if (!initialized)
            {
                previous = current;
                initialized = true;
                return;
            }

            if (current == previous) return;
            previous = current;

            if ((current && triggerWhenTrue) || (!current && triggerWhenFalse))
                _ = trigger.Execute(this.Self);
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

        private bool ReadState(PlayerVoice voice)
        {
            return state switch
            {
                VoiceState.Muted => voice.IsMuted,
                VoiceState.PushToTalkEnabled => voice.IsPushToTalkEnabled,
                VoiceState.ForceTransmitEnabled => voice.IsForceTransmitEnabled,
                _ => false
            };
        }
    }
}
