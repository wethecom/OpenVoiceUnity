using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using OpenVoiceSharp.Unity.GameCreator2;
using UnityEngine;

namespace OpenVoiceSharp.Unity.GameCreator2.VisualScripting
{
    [Title("OpenVoice Preferences")]
    [Description("Saves, loads or clears persisted OpenVoice preferences.")]
    [Category("OpenVoice/Preferences")]
    [Serializable]
    public class InstructionOpenVoicePreferences : Instruction
    {
        private enum PreferencesAction
        {
            Save,
            Load,
            Clear
        }

        [SerializeField] private PropertyGetGameObject target = GetGameObjectSelf.Create();
        [SerializeField] private GameObject targetFallback;
        [SerializeField] private OpenVoiceVoicePreferences preferences;
        [SerializeField] private PreferencesAction action = PreferencesAction.Save;

        protected override Task Run(Args args)
        {
            OpenVoiceVoicePreferences prefs = ResolvePreferences(args);
            if (prefs == null) return DefaultResult;

            switch (action)
            {
                case PreferencesAction.Save: prefs.Save(); break;
                case PreferencesAction.Load: prefs.Load(); break;
                case PreferencesAction.Clear: prefs.Clear(); break;
            }

            return DefaultResult;
        }

        private OpenVoiceVoicePreferences ResolvePreferences(Args args)
        {
            return OpenVoiceGc2Resolver.ResolvePreferences(target, targetFallback, args, preferences);
        }
    }
}
