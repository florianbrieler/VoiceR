# VoiceR
VoiceR is a utility to control Windows through voice, by giving it clear commands what to do. It processes your inputs with LLMs and uses Windows APIs to take action.

## Key Idea
You speak to your Windows installation and tell it what to do. Examples:
- Open Start Menu
- Click OK button
- Enter "John Doe" in that input field

Ideally, you can fully interact with your Window with voice only.

## How it works
VoiceR listens to what you say and tanscribes your words. In parallel, it uses Microsoft UI Automation (UIA) to understand all the components on your screen.
Both is fed into an LLM (API key required) which then decides which action to take. VoiceR will then execute the action.

## Tech Stack
C#, UIA, Win UI 3

VoiceR runs as tray application.

### Dependencies
- .Net 10.0.1: [https://dotnet.microsoft.com/en-us/download/dotnet/10.0]
- Windows App SDK 1.8.3: [https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads]
