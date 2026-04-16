using UnityEditor;
using UnityEngine;
using OpenVoiceSharp;

namespace OpenVoiceSharp.Unity.Editor
{
    /// <summary>
    /// Validates that your Unity project is configured correctly for OpenVoiceSharp.
    /// Runs automatically when scripts recompile and is available under Tools > OpenVoiceSharp.
    /// </summary>
    [InitializeOnLoad]
    public static class VoiceSetupValidator
    {
        static VoiceSetupValidator()
        {
            // Run after every compile, but only log — don't pop a window on every compile
            ValidateAndLog();
        }

        [MenuItem("Tools/OpenVoiceSharp/Validate Setup")]
        public static void OpenValidationWindow()
        {
            bool sampleRateOk = AudioSettings.outputSampleRate == VoiceChatInterface.SampleRate;

            if (sampleRateOk)
            {
                EditorUtility.DisplayDialog(
                    "OpenVoiceSharp — Setup OK",
                    "✓ Audio sample rate is 48000 Hz\n\n" +
                    "Your project is configured correctly for OpenVoiceSharp.",
                    "Great"
                );
            }
            else
            {
                bool fix = EditorUtility.DisplayDialog(
                    "OpenVoiceSharp — Setup Issue",
                    $"Audio sample rate is {AudioSettings.outputSampleRate} Hz but OpenVoiceSharp requires 48000 Hz.\n\n" +
                    "This will cause garbled or silent voice audio at runtime.\n\n" +
                    "Fix it now?\n" +
                    "(Edit > Project Settings > Audio > System Sample Rate = 48000)",
                    "Open Audio Settings",
                    "Ignore"
                );

                if (fix)
                    SettingsService.OpenProjectSettings("Project/Audio");
            }
        }

        [MenuItem("Tools/OpenVoiceSharp/Player Prefab Setup Guide")]
        public static void ShowPrefabGuide()
        {
            EditorUtility.DisplayDialog(
                "OpenVoiceSharp — Player Prefab Setup",
                "Add these components to your player prefab:\n\n" +
                "1. PlayerVoice  (this package)\n" +
                "2. AudioSource  (required by PlayerVoice)\n" +
                "3. NetworkObserver (FishNet)\n" +
                "   └── Add a DistanceCondition\n" +
                "       └── Set its range to match PlayerVoice > MaxDistance\n\n" +
                "The DistanceCondition is your vicinity system.\n" +
                "FishNet will only send voice RPCs to players within range.",
                "Got it"
            );
        }

        private static void ValidateAndLog()
        {
            if (AudioSettings.outputSampleRate != VoiceChatInterface.SampleRate)
            {
                Debug.LogWarning(
                    $"[OpenVoiceSharp] Audio sample rate is {AudioSettings.outputSampleRate} Hz but 48000 Hz is required. " +
                    $"Voice chat will not work correctly. " +
                    $"Fix: Edit > Project Settings > Audio > System Sample Rate = 48000. " +
                    $"Or run Tools > OpenVoiceSharp > Validate Setup."
                );
            }
        }
    }
}
