# SpeechFlow

Real-time voice typing for Windows 10/11, in Arabic and English. Press a shortcut, speak, and the
text appears wherever your cursor is: Word, Chrome, WhatsApp, VS Code, any app.

## Features

- **Two speech engines**, both running OpenAI's Whisper:
  - **Cloud (Groq)**, the default: Whisper large-v3-turbo, under a second per phrase, nothing to download. Needs a free key.
  - **On this PC**: whisper.cpp runs locally, so it's free, private and works offline. The model
    (190 MB small or 574 MB turbo) downloads once and only if you pick this engine. A phrase takes about 1 s on a laptop CPU.
- Text is typed as soon as you pause, and the language can be **Arabic, English or detected automatically**.
- **Voice punctuation**: in Arabic say فاصلة، سطر جديد، علامة استفهام، فاصلة منقوطة، نقطة (at the end of a phrase);
  in English say "comma", "question mark", "new line", "period" (at the end of a phrase).
- **Undo last phrase** and **AI polish** (fix grammar, make it professional, or translate AR ⇄ EN) through Groq.
- **Snippets**: say "my email" and it types your address.
- A floating bar that never steals focus, plus a tray icon and global shortcuts.
- Nothing is lost while the model loads: audio is buffered, and the phrase you were saying is still typed when you stop.
- Private by default: API keys are encrypted for your Windows account, and history is off unless you turn it on.
- No admin rights and no keyboard hook.

## Default shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+Space` | Start / stop dictation |
| `Ctrl+Shift+L` | Switch Arabic / English |
| `Ctrl+Shift+Backspace` | Undo the last phrase (press again to go further back) |
| `Ctrl+Shift+K` | AI-polish the last dictation |

You can change all of them in Settings → Shortcuts.

## Getting a key (cloud engine)

Sign in at [console.groq.com/keys](https://console.groq.com/keys) with Google, create a key, and paste
it into Settings. It's free and needs no credit card. The same key powers AI polish. If you'd rather not
use a key, choose "On this PC" in Settings.

## Where things are stored

`%APPDATA%\SpeechFlow\` holds `settings.json` (with the key encrypted), `snippets.json`, the optional
`history.jsonl`, and `speechflow.log`. The log never contains what you dictated. Downloaded models go in
`%LOCALAPPDATA%\SpeechFlow\models\`, and you can delete them from Settings.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet test SpeechFlow.slnx
dotnet run --project src/SpeechFlow

# Release build: one self-contained exe, then the installer (needs Inno Setup 6)
dotnet publish src/SpeechFlow -c Release -r win-x64 --self-contained -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
iscc installer\SpeechFlow.iss
```

Pushing a tag like `v3.0.0` makes GitHub Actions run the tests, build the installer and a portable exe,
and attach both to a GitHub release.

## Project layout

```
src/SpeechFlow/
  Core/                    speech, audio, typing, text formatting, hotkeys, settings
  UI/                      floating bar, settings window, tray icon
  DictationController.cs   ties it all together; everything runs on the UI thread
  Core/Speech/             phrase splitting, Groq and local Whisper recognizers, model downloads
tests/SpeechFlow.Tests/    unit tests (set SPEECHFLOW_SAMPLES to run the local Whisper check)
installer/SpeechFlow.iss   per-user Inno Setup installer (no admin)
public/                    landing page (Firebase Hosting)
```

## Known limits

- Windows blocks typing into apps running as administrator unless SpeechFlow also runs as administrator.
- If you click into a different spot inside the same window, the space between phrases may be off by one.

## License

Copyright (c) 2026 Nashat Shamsadyn. See [LICENSE](LICENSE).
