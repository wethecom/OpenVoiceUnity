# OpenVoice GC2 Smoke Test Checklist

## Defines
- `OPENVOICE_UNITY_VOICECHAT`
- `OPENVOICE_GAMECREATOR2`

## Compile
- No compiler errors in `OpenVoiceSharp.Unity.GameCreator2` assembly
- GC2 custom nodes appear under `OpenVoice/*` categories

## Runtime
- Local owner can mute/unmute via `InstructionOpenVoiceMute`
- Push-to-talk can be enabled/disabled via `InstructionOpenVoicePushToTalk`
- Forced transmit can be toggled via `InstructionOpenVoiceTransmit`
- `ConditionOpenVoiceState` returns expected values

## Events
- `EventOnOpenVoiceSpeakingStarted` fires on speech start
- `EventOnOpenVoiceSpeakingStopped` fires after speech ends
- `EventOnOpenVoicePacketReceived` fires when remote packets arrive
- `EventOnOpenVoiceStateChange` fires on mute/PTT/transmit changes

## Preferences
- `InstructionOpenVoicePreferences` Save/Load round-trips mute/PTT/transmit
- `Clear` removes saved keys

## UI
- `InstructionOpenVoiceSetIndicator` correctly mirrors selected voice state
