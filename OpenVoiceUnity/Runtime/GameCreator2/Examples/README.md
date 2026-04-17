# OpenVoice + Game Creator 2 Examples

This folder documents suggested GC2 graph setups for OpenVoice integration.

## 1) Push To Talk Toggle
- Trigger: `Input Key Down` (for example `V`)
- Instruction: `OpenVoice Push To Talk` (`pushToTalkEnabled = true`)

## 2) Mute Toggle
- Trigger: `Input Key Down` (for example `M`)
- Instruction: `OpenVoice Control` (`action = ToggleMute`)

## 3) Show Mute Icon
- Trigger: `On OpenVoice State Change` (`state = Muted`)
- Instruction: `OpenVoice Set Indicator` (`state = Muted`, indicator = mute icon object)

## 4) Save Voice Preferences
- Trigger: `On Application Quit` or settings menu save
- Instruction: `OpenVoice Preferences` (`action = Save`)

## 5) Load Voice Preferences
- Trigger: scene start
- Instruction: `OpenVoice Preferences` (`action = Load`)
