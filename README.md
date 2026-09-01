# 🎙️ Windows Win+H Customizable Live STT & Typing Interpreter (Arabic & English)

A state-of-the-art, **Microsoft Win+H inspired customizable voice typing bar** for Windows 10/11 with **3 customizable action buttons**, ultra-low latency (<250ms), and **22 advanced features**.

---

## ✨ 22 Game-Changing Features (Verified 22/22)

| # | Feature | Details |
|---|---|---|
| 1 | **Win+H Acrylic Floating Bar** | Sleek Windows 11 rounded pill with Mica/Acrylic glassmorphism, draggable, and always on top. |
| 2 | **3-Button Customizable Action Bar** | Reassign the 3 action buttons in settings (Mic Toggle, Push-to-Talk, Lang Switch, AI Magic, Drawer). |
| 3 | **AI Text Polisher & Rewriter** | 1-Click AI cleanup ("Fix Grammar", "Professional Business Tone", "Bullet Points", "Translate AR<->EN"). |
| 4 | **Push-to-Talk vs Toggle Mode** | Hold key to talk or tap to toggle dictation. |
| 5 | **Voice Punctuation & Formatting Commands** | Speak "فاصلة", "نقطة", "علامة استفهام", "سطر جديد" or "comma", "period", "new line" to format text live! |
| 6 | **60FPS Real-Time Reactive EQ Visualizer** | Multi-bar smooth wave animation reactive to live microphone gain. |
| 7 | **Interim Live Ticker / Ghost Typing** | Floating preview box displaying real-time partial hypothesis before committing. |
| 8 | **Native Win32 Unicode Keystroke Injection** | `KEYEVENTF_UNICODE` typing that never corrupts Arabic/English regardless of Windows keyboard layout. |
| 9 | **Atomic Fast-Paste Burst Mode (`Ctrl+V`)** | Rapid clipboard burst with backup and instant restore in <20ms. |
| 10 | **Bilingual Auto-Detection & Code-Switching** | Seamlessly speak Arabic and English in the same session without manual toggling. |
| 11 | **Audio Chimes & Earcons Feedback** | Subtle pleasant audio tones for listening start, stop, error, and language switch. |
| 12 | **Custom Vocabulary & Voice Snippet Expansion** | Custom dictionary replacement (e.g. "بريدي" -> "myemail@domain.com", "تحياتي" -> "مع أطيب التحيات"). |
| 13 | **Arabic Tashkeel & Diacritics Management** | Strip Tashkeel, preserve Tashkeel, or normalize Hamzas. |
| 14 | **Profanity & Sensitive Word Filter** | Optional automatic masking of sensitive words with custom wordlist. |
| 15 | **Dictation History Log & 1-Click Copy** | Persistent history viewer with search, timestamp, and instant re-paste. |
| 16 | **Dynamic Microphone & Audio Device Switcher** | Hot-swap input microphones without restarting the application. |
| 17 | **Customizable Global Hotkeys Rebinding** | User-defined hotkeys for Dictate, Language Switch, PTT, and AI Polish. |
| 18 | **Auto-Sleep / Silence Inactivity Timer** | Automatically powers down audio stream after N seconds of silence to conserve API/battery. |
| 19 | **Multi-Engine Switching** | Deepgram Nova-2 WebSocket, Groq Whisper-Turbo, and Local Offline Whisper. |
| 20 | **Local Offline Fallback Mode (Faster-Whisper)** | Automatic fallback to local faster-whisper when internet is unavailable. |
| 21 | **Smart Auto-Capitalization & Spacing Engine** | Context-aware space insertion and capitalization rules for English and Arabic. |
| 22 | **HUD Screen Snapping & Caret Pinning** | Snap to bottom-center, top-center, or custom saved screen coordinates. |

---

## 🚀 Quick Start

### 1. Run the Application
```powershell
python main.py
```

### 2. Run the 22-Feature Automated Verification
```powershell
python verify_all_features.py
```

---

## ⌨️ Default Shortcuts

* **`Ctrl + Shift + Space`**: Start / Stop Dictation.
* **`Ctrl + Shift + L`**: Toggle Language (Arabic `العربية` / English `EN`).
* **`Click ⚡ Tools`**: Expand drawer with History, AI Magic, Snippets, and Settings.
