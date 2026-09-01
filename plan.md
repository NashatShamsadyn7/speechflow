# Windows Ultra-Fast Live STT & Live Typing Interpreter (Arabic & English)
## Architectural Blueprint & Implementation Plan (`plan.md`)

---

## 1. Executive Summary

This document outlines the architecture, technology stack, engine benchmarks, and implementation roadmap for building a **high-speed, low-latency Speech-to-Text (STT) Live Typing Interpreter for Windows**.

### Key Objectives
* **Sub-250ms End-to-End Latency**: Instant audio stream to text conversion.
* **Full Bilingual Mastery**: Seamless support for **Arabic** (Modern Standard Arabic & popular dialects) and **English**, with dynamic code-switching or instant hotkey switching.
* **Universal Live Typing (Interpreter Mode)**: Types directly into *any* active Windows application (Notepad, Word, Chrome, Slack, VS Code, Discord, WhatsApp) using native Win32 Unicode injection without relying on local keyboard layout states.
* **Floating Glassmorphic HUD**: Non-intrusive, always-on-top transparent widget with real-time audio visualizer, language indicator, and interim live transcription preview.
* **Internet-Powered Cloud Streaming**: Leveraging state-of-the-art streaming APIs (Deepgram Nova-2 / Groq Whisper-Turbo / Google Chirp-2) paired with local zero-latency Voice Activity Detection (Silero VAD).

---

## 2. STT Cloud Streaming Engines Comparison (Arabic + English)

| Feature / Metric | **Deepgram Nova-2 (WebSocket)** *(Top Recommendation)* | **Groq Whisper Large v3 Turbo (HTTP Chunk Streaming)** | **Google Cloud Chirp 2 (gRPC / Streaming)** | **OpenAI Realtime API (WebRTC/WS)** |
| :--- | :--- | :--- | :--- | :--- |
| **Streaming Protocol** | Bi-directional WebSocket | Chunked HTTP (500ms-1s buffers) | Bi-directional gRPC / WS | WebSocket / WebRTC |
| **Median Latency (TTFT)** | **~150ms – 250ms** ⚡ | **~200ms – 350ms** | ~350ms – 500ms | ~300ms – 600ms |
| **Arabic Accuracy** | High (MSA + Dialects) | Extremely High (Whisper v3 Turbo) | Very High (Chirp multilingual) | Very High |
| **English Accuracy** | Near-human (Word-level timestamps) | Near-human | High | Very High |
| **Live Interim Results** | Native `is_final: false` stream | Requires rolling window diffing | Native streaming chunks | Native streaming events |
| **Cost** | ~$0.0043 / min (Pay-as-you-go) | ~$0.0006 / min (Extremely cheap) | ~$0.016 / min | ~$0.06 / min |
| **Best Used For** | **Live Word-by-Word Typing** | **High Accuracy Fast Dictation** | **Enterprise Multilingual** | **Conversational Agents** |

### Recommended Engine Strategy: **Hybrid Multi-Engine Architecture**
1. **Primary Live Engine**: **Deepgram Nova-2 Streaming WebSocket** for instant word-by-word interim typing (Live Keystroke Mode).
2. **Alternative / Fallback Engine**: **Groq Whisper Large v3 Turbo** for push-to-talk block transcription with maximum phonetic Arabic accuracy.

---

## 3. System Architecture & Component Design

```
+-----------------------------------------------------------------------------------+
|                              Windows 10 / 11 Desktop                              |
|                                                                                   |
|  +---------------------+        +--------------------+       +-----------------+  |
|  | Hardware Microphone | -----> | SoundDevice Stream | ----> |   Silero VAD    |  |
|  +---------------------+        | (16kHz, 16bit PCM) |       | (Voice Detector)|  |
|                                 +--------------------+       +--------+--------+  |
|                                                                       | Audio     |
|                                                                       v Stream    |
|  +--------------------------------------------------------------------+--------+  |
|  |                     Low-Latency Network Client Layer                        |  |
|  |   - WebSocket Client (Deepgram / Cloud STT)                                 |  |
|  |   - Reconnection & Exponential Backoff                                     |  |
|  |   - Language Switcher (AR / EN / Auto-detect)                              |  |
|  +------------------------------------+----------------------------------------+  |
|                                       | Transcripts (Interim & Final)             |
|                                       v                                           |
|  +------------------------------------+----------------------------------------+  |
|  |                     Live Typing & Interpreter Core                          |  |
|  |   - Text Normalizer (Arabic Hamza / Tashkeel / Punctuation)                 |  |
|  |   - Delta / Diff Calculator (Interim vs Final words)                        |  |
|  |   - Win32 SendInput (KEYEVENTF_UNICODE / Zero-Layout Glitch)                |  |
|  |   - Clipboard Fast-Paste Fallback                                           |  |
|  +-------------------+--------------------------------+------------------------+  |
|                      |                                |                           |
|                      v                                v                           |
|      +---------------+----------------+    +----------+--------------------+      |
|      |    Active Focused Window       |    | Floating PySide6 Glassmorphic |      |
|      | (Word, Chrome, IDE, WhatsApp)  |    |     HUD & Live Waveform       |      |
|      +--------------------------------+    +-------------------------------+      |
+-----------------------------------------------------------------------------------+
```

---

## 4. Detailed Module Specifications

### Module 1: Audio Capture & Zero-Latency VAD (`audio_pipeline.py`)
* **Input Parameters**: 16,000 Hz, 1-channel Mono, 16-bit Linear PCM (`int16` or `float32`).
* **Chunk Size**: 20ms – 40ms buffers (320 – 640 samples per chunk).
* **VAD (Voice Activity Detection)**:
  * Employs **Silero VAD v5** (ONNX Runtime, ultra-lightweight, <1ms CPU inference).
  * Automatically filters out background keyboard clicks, breaths, and ambient noise.
  * Silence hangover time: 400ms before marking end-of-speech.

### Module 2: Cloud Streaming WebSocket Pipeline (`stt_client.py`)
* **Deepgram Nova-2 WebSocket Configuration**:
  ```json
  {
    "model": "nova-2",
    "language": "ar", // or "en" or multi
    "smart_format": true,
    "interim_results": true,
    "punctuate": true,
    "endpointing": 300,
    "encoding": "linear16",
    "sample_rate": 16000
  }
  ```
* **Event Handling**:
  * `on_message`: Parses `is_final` flag.
  * If `is_final == false`: Emits interim preview text to Floating HUD.
  * If `is_final == true`: Passes text to the Live Typing Engine to commit to the active window.

### Module 3: Win32 Unicode Live Typing Engine (`typer_engine.py`)
* **The Windows Typing Problem**: Standard `pyautogui` or `pynput` types simulated virtual keys based on the *currently active Windows keyboard layout*. If the user's layout is set to English and Arabic text is sent, it produces gibberish (e.g. `dfh.f`).
* **The Solution**: Native **Win32 `SendInput` with `KEYEVENTF_UNICODE` (`0x0004`)**:
  * Bypasses the active keyboard layout entirely.
  * Transmits exact 16-bit UTF-16 code units directly to the focused window's input queue.
  * Flawless Arabic character joining (shaping) and Right-to-Left (RTL) injection.
* **Dual Output Modes**:
  1. **Live Stream Typing Mode**: Types words as they are finalized, with automatic backspace handling for interim corrections.
  2. **Smart Paste Mode (Lightning Fast)**: Saves existing clipboard, sets text to clipboard, executes atomic `Ctrl + V`, then restores original clipboard content in < 30ms.

### Module 4: Bilingual (Arabic & English) Optimization (`language_handler.py`)
* **Arabic Handling**:
  * Modern Arabic punctuation mapping (`،` comma, `؟` question mark, `.` period).
  * Optional diacritics / Tashkeel normalization.
  * Intelligent spacing before/after Arabic words and punctuation.
* **English Handling**:
  * Auto-capitalization of sentence beginnings and "I".
  * Standard punctuation formatting.
* **Code-Switching / Instant Switch**:
  * Global Hotkey: `Right Alt` or `Caps Lock` or `Ctrl+Shift+L` instantly toggles between `Arabic (العربية)` and `English (EN)`.

### Module 5: Modern Glassmorphic HUD & Tray Controller (`ui_overlay.py`)
* Built with **PySide6 (Qt for Python)**.
* **Frameless, Transparent, Click-Through Option**:
  * Always on top (`Qt.WindowStaysOnTopHint | Qt.FramelessWindowHint | Qt.Tool`).
  * Live audio visualizer (reactive waveform/bars).
  * Status indicator: 🔴 Recording / 🟢 Ready / 🌐 Connecting.
  * Active Language Pill: `[AR]` / `[EN]`.
  * Interim live transcription ticker.
* **System Tray**:
  * Minimize to system tray with quick settings menu (Microphone selector, API key config, hotkey customization).

---

## 5. End-to-End Latency Budget

```
[Audio Input (20ms)]
       │
       ▼
[Silero VAD Filter (~2ms)]
       │
       ▼
[TLS WebSocket Transport (~30-50ms)]
       │
       ▼
[Deepgram Nova-2 Inference (~120-160ms)]
       │
       ▼
[Response Parse & Normalization (~1ms)]
       │
       ▼
[Win32 SendInput Keystroke Injection (~5ms)]
       │
       ▼
Total End-to-End Latency: ~180ms – 240ms (Imperceptible Delay)
```

---

## 6. Implementation Roadmap & Project Structure

### File Tree
```
faisal-stt-windows/
├── plan.md                       # This plan
├── requirements.txt              # Python dependencies
├── config.json                   # User settings (API keys, hotkeys, language)
├── main.py                       # Main application entry point
├── core/
│   ├── __init__.py
│   ├── audio_capture.py          # SoundDevice mic capture + buffer
│   ├── vad_detector.py           # Silero VAD ONNX wrapper
│   ├── stt_deepgram.py           # WebSocket streaming client
│   ├── stt_groq.py               # Fast chunked Whisper fallback
│   ├── win32_typer.py            # Win32 SendInput Unicode typer
│   ├── text_processor.py         # Arabic/English formatting & rules
│   └── hotkey_manager.py         # Global hotkey listener (Win32 API)
└── ui/
    ├── __init__.py
    ├── hud_overlay.py            # PySide6 floating glassmorphism widget
    ├── visualizer.py             # Real-time audio waveform canvas
    └── tray_icon.py              # Windows tray icon and context menu
```

### Key Python Dependencies
```txt
pyside6>=6.6.0
sounddevice>=0.4.6
numpy>=1.24.0
websockets>=12.0
deepgram-sdk>=3.0.0
groq>=0.9.0
onnxruntime>=1.16.0
keyboard>=0.13.5
pywin32>=306
requests>=2.31.0
```

---

## 7. Step-by-Step Execution Plan

### Step 1: Core Audio Capture & Win32 Unicode Typer
* Implement `core/win32_typer.py` with `ctypes.windll.user32.SendInput` using `KEY_EVENT_RECORD` with `KEYEVENTF_UNICODE`. Test Arabic string typing into Notepad without changing keyboard layout.
* Implement `core/audio_capture.py` capturing 16kHz PCM audio buffers with continuous non-blocking callback.

### Step 2: Streaming STT Integration
* Implement `core/stt_deepgram.py` streaming audio chunks via async WebSockets to Deepgram Nova-2.
* Handle live Arabic and English audio feeds, receiving interim and final transcript tokens.

### Step 3: Text Normalizer & Typing Controller
* Connect the incoming token stream with `core/text_processor.py` for Arabic punctuation and spacing normalization.
* Connect finalized tokens to `win32_typer.py` for live typing injection.

### Step 4: Floating Glassmorphism HUD & Visualizer
* Design a sleek dark-mode glassmorphic widget using PySide6.
* Add an animated audio reactive waveform and status pill showing active language (`AR` / `EN`).

### Step 5: Global Hotkeys & Configuration Management
* Bind global hotkeys:
  * **Toggle Dictation On/Off**: `Ctrl + Shift + Space` or `Win + Alt + V`.
  * **Switch Language (AR / EN)**: `Ctrl + Shift + L` or `Alt + Shift`.
  * **Push-to-Talk Mode**: Optional hold-key mode.
* Provide clean `config.json` with API key storage and customizable settings.

---

## 8. Verification & Performance Testing Plan

1. **Arabic Typing Fidelity Test**:
   * Dictate standard Arabic sentences and colloquial phrases (e.g. "السلام عليكم ورحمة الله وبركاته، كيف الحال؟").
   * Verify text is typed into Windows Notepad with correct RTL characters, Tashkeel (if enabled), and punctuation (`،` `؟`).
2. **English Typing & Capitalization Test**:
   * Dictate complex technical English sentences.
   * Verify proper capitalization, acronym handling, and comma/period placement.
3. **Speed & Latency Benchmark**:
   * Measure timestamp difference from audio buffer creation to text appearance in the active text editor. Target: `< 250ms`.
4. **App Compatibility Test**:
   * Test typing across diverse Windows applications: Microsoft Word, Google Chrome, Visual Studio Code, Windows Terminal, Slack, WhatsApp Desktop.
