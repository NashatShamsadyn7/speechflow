# SpeechFlow

Real-time voice typing for Windows 10/11, in Arabic and English. Press a shortcut, speak, and the
text appears wherever your cursor is: Word, Chrome, WhatsApp, VS Code, any app.

## Features

- **Live dictation** over Deepgram's streaming API (nova-3), with a live preview on the bar.
- **Arabic dialects**: standard, Iraqi, Gulf, Egyptian, Levantine and Maghrebi.
- **Voice punctuation**: in Arabic say فاصلة، سطر جديد، علامة استفهام، فاصلة منقوطة، نقطة (at the end of a phrase);
  in English say "comma", "period", "question mark", "new line".
- **Undo last phrase** and **AI polish** (fix grammar, make it professional, or translate AR ⇄ EN) through Groq.
- **Snippets**: say "my email" and it types your address.
- A floating bar that never steals focus, plus a tray icon and global shortcuts.
- Nothing is lost while connecting: audio is buffered, the last words are flushed on stop, and it reconnects on its own.
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

## Getting an API key

SpeechFlow uses [Deepgram](https://console.deepgram.com/signup) for speech recognition. New accounts
get $200 of free credit. Create a key and paste it into Settings. AI polish is optional and needs a free
[Groq](https://console.groq.com/keys) key.

## Where things are stored

`%APPDATA%\SpeechFlow\` holds `settings.json` (with the keys encrypted), `snippets.json`, the optional
`history.jsonl`, and `speechflow.log`. The log never contains what you dictated.

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
tests/SpeechFlow.Tests/    unit tests for text formatting and shortcut parsing
installer/SpeechFlow.iss   per-user Inno Setup installer (no admin)
public/                    landing page (Firebase Hosting)
```

## Known limits

- Windows blocks typing into apps running as administrator unless SpeechFlow also runs as administrator.
- If you click into a different spot inside the same window, the space between phrases may be off by one.

## License

Copyright (c) 2026 Nashat Shamsadyn. See [LICENSE](LICENSE).
