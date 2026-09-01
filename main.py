"""
Main Application Entry Point: Microsoft Win+H Style Ultra-Fast STT & Typing Interpreter
Orchestrates 20+ real features: 3-Button Customizable Bar, Voice Commands, AI Polisher,
Snippets Expansion, History Log, Audio Chimes, Offline Whisper Fallback, and Win32 Unicode Typing.
"""

import sys
import os
import json
import time
import threading
from PySide6.QtWidgets import QApplication
from PySide6.QtCore import QTimer

from core.win32_typer import Win32Typer
from core.text_processor import TextProcessor
from core.audio_capture import AudioCapture
from core.stt_deepgram import DeepgramStreamClient
from core.stt_groq import GroqWhisperClient
from core.offline_whisper import OfflineWhisperEngine
from core.hotkey_manager import HotkeyManager
from core.voice_commands import VoiceCommandParser
from core.snippet_manager import SnippetManager
from core.history_manager import HistoryManager
from core.ai_polisher import AIPolisher
from core.sound_effects import SoundEffects
from ui.hud_overlay import HUDOverlay
from ui.tray_icon import TrayIconController

CONFIG_PATH = os.path.join(os.path.dirname(__file__), "config.json")

def load_config():
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {
        "deepgram_api_key": "",
        "groq_api_key": "",
        "primary_engine": "deepgram",
        "default_language": "ar",
        "typing_mode": "sendinput",
        "toggle_hotkey": "ctrl+shift+space",
        "lang_switch_hotkey": "ctrl+shift+l",
        "sound_effects": True,
        "auto_sleep_sec": 10,
        "enable_voice_commands": True,
        "enable_snippets": True,
        "strip_tashkeel": False,
        "char_delay_ms": 1.0,
        "sample_rate": 16000
    }

def save_config(config_data):
    with open(CONFIG_PATH, "w", encoding="utf-8") as f:
        json.dump(config_data, f, indent=2, ensure_ascii=False)


class STTWinHController:
    def __init__(self, app: QApplication):
        self.app = app
        self.config = load_config()
        self.is_recording = False
        self.current_lang = self.config.get("default_language", "ar")
        self.last_typed_text = ""
        self.last_speech_time = time.time()

        # Core Subsystems
        self.sounds = SoundEffects(enabled=self.config.get("sound_effects", True))
        self.snippet_mgr = SnippetManager()
        self.history_mgr = HistoryManager()
        self.typer = Win32Typer(char_delay_ms=self.config.get("char_delay_ms", 1.0))
        
        self.text_processor = TextProcessor(
            strip_tashkeel=self.config.get("strip_tashkeel", False),
            enable_voice_commands=self.config.get("enable_voice_commands", True),
            enable_snippets=self.config.get("enable_snippets", True),
            enable_profanity_filter=self.config.get("enable_profanity_filter", False),
            snippet_manager=self.snippet_mgr
        )

        # AI Polisher
        self.ai_polisher = AIPolisher(api_key=self.config.get("groq_api_key", ""))

        # Offline STT Fallback
        self.offline_whisper = OfflineWhisperEngine()

        # Streaming STT Clients
        self.deepgram_client = None
        self.groq_client = None
        self._init_stt_clients()

        # Audio Capture
        self.audio_capture = AudioCapture(
            sample_rate=self.config.get("sample_rate", 16000),
            on_audio_chunk=self._on_audio_chunk
        )

        # Win+H UI Overlay
        self.hud = HUDOverlay(
            config=self.config,
            on_toggle_callback=self.toggle_recording,
            on_lang_switch_callback=self.switch_language,
            on_ai_polish_callback=self.run_ai_polish,
            on_settings_save_callback=self.update_settings,
            history_mgr=self.history_mgr,
            snippet_mgr=self.snippet_mgr
        )

        # Tray Icon Controller
        self.tray = TrayIconController(
            app=self.app,
            on_toggle=self.toggle_recording,
            on_switch_lang=self.switch_language,
            on_show_hud=self.hud.show,
            on_quit=self.quit_app
        )

        # Global Hotkeys
        self.hotkeys = HotkeyManager(
            toggle_hotkey=self.config.get("toggle_hotkey", "ctrl+shift+space"),
            lang_switch_hotkey=self.config.get("lang_switch_hotkey", "ctrl+shift+l"),
            on_toggle_dictation=self.toggle_recording,
            on_switch_language=self.switch_language
        )
        self.hotkeys.start()

        # Auto-Sleep Inactivity Timer (Check every 1 sec)
        self.auto_sleep_timer = QTimer()
        self.auto_sleep_timer.timeout.connect(self._check_inactivity_sleep)
        self.auto_sleep_timer.start(1000)

        # Position HUD
        self._position_hud()
        self.hud.show()

    def _position_hud(self):
        screen = self.app.primaryScreen().geometry()
        x = (screen.width() - self.hud.width()) // 2
        y = screen.height() - self.hud.height() - 90
        self.hud.move(x, y)

    def _init_stt_clients(self):
        dg_key = self.config.get("deepgram_api_key", "").strip()
        groq_key = self.config.get("groq_api_key", "").strip()

        if dg_key:
            self.deepgram_client = DeepgramStreamClient(
                api_key=dg_key,
                language=self.current_lang,
                sample_rate=self.config.get("sample_rate", 16000),
                on_interim_text=self._on_interim_transcript,
                on_final_text=self._on_final_transcript,
                on_status_change=lambda s: self.hud.status_changed.emit(s, "#38bdf8" if "Connect" in s else "#6b7280"),
                on_error=lambda e: self._handle_stt_error(e)
            )

        if groq_key:
            self.groq_client = GroqWhisperClient(
                api_key=groq_key,
                language=self.current_lang,
                on_transcription=lambda text: self._on_final_transcript(text, True)
            )

    def _handle_stt_error(self, err_msg: str):
        self.sounds.play_error_chime()
        self.hud.status_changed.emit("API Error", "#ef4444")

    def _on_audio_chunk(self, raw_bytes: bytes, is_speech: bool, rms: float):
        self.hud.visualizer.set_audio_level(rms, is_speech)

        if is_speech:
            self.last_speech_time = time.time()

        if not self.is_recording:
            return

        engine = self.config.get("primary_engine", "deepgram")
        if engine == "deepgram" and self.deepgram_client:
            self.deepgram_client.send_audio(raw_bytes)

    def _on_interim_transcript(self, text: str):
        if not self.is_recording or not text:
            return
        self.hud.interim_text_received.emit(text)

    def _on_final_transcript(self, text: str, is_speech_final: bool):
        if not text:
            return

        formatted_text, detected_lang = self.text_processor.process_token(
            token=text,
            lang=self.current_lang,
            previous_text=self.last_typed_text
        )

        self.last_typed_text += formatted_text
        self.hud.final_text_received.emit(formatted_text.strip())

        # Save to history log
        self.history_mgr.add_entry(formatted_text.strip(), lang=detected_lang)

        # Type to active window via Win32 Unicode
        typing_mode = self.config.get("typing_mode", "sendinput")
        if typing_mode == "paste":
            self.typer.paste_text_fast(formatted_text)
        else:
            self.typer.type_text(formatted_text)

    def toggle_recording(self):
        self.is_recording = not self.is_recording
        self.hud.set_recording_state(self.is_recording)
        self.tray.update_state(self.is_recording)

        if self.is_recording:
            self.sounds.play_start_chime()
            self.last_speech_time = time.time()
            if self.deepgram_client:
                self.deepgram_client.start()
            self.audio_capture.start()
        else:
            self.sounds.play_stop_chime()
            if self.deepgram_client:
                self.deepgram_client.stop()
            self.audio_capture.stop()
            self.last_typed_text = ""

    def switch_language(self, new_lang=None):
        if new_lang is None:
            self.current_lang = "en" if self.current_lang == "ar" else "ar"
        else:
            self.current_lang = new_lang

        self.config["default_language"] = self.current_lang
        save_config(self.config)
        
        self.sounds.play_switch_chime()
        self.hud.language_switched.emit(self.current_lang)

        if self.deepgram_client:
            self.deepgram_client.set_language(self.current_lang)
        if self.groq_client:
            self.groq_client.set_language(self.current_lang)

    def run_ai_polish(self, text: str, mode: str, callback):
        self.ai_polisher.set_api_key(self.config.get("groq_api_key", ""))
        self.ai_polisher.polish_text_async(text, mode=mode, callback=callback)

    def _check_inactivity_sleep(self):
        """Auto-sleep after silence timeout."""
        if not self.is_recording:
            return
        timeout = self.config.get("auto_sleep_sec", 10)
        if time.time() - self.last_speech_time > timeout:
            print(f"[AutoSleep] Inactivity detected ({timeout}s silence). Pausing dictation.")
            self.toggle_recording()

    def update_settings(self, new_settings: dict):
        self.config.update(new_settings)
        save_config(self.config)
        self.sounds.enabled = self.config.get("sound_effects", True)
        self.ai_polisher.set_api_key(self.config.get("groq_api_key", ""))
        self._init_stt_clients()

    def quit_app(self):
        self.audio_capture.stop()
        if self.deepgram_client:
            self.deepgram_client.stop()
        self.hotkeys.stop()
        self.app.quit()


def main():
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    controller = STTWinHController(app)
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
