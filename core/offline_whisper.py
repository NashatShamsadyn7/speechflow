"""
Local Offline Fallback STT Engine
Uses faster-whisper for on-device, private, zero-internet speech-to-text.
"""

import io
import wave
import numpy as np
from typing import Optional, Callable
import threading

try:
    from faster_whisper import WhisperModel
except ImportError:
    WhisperModel = None

class OfflineWhisperEngine:
    def __init__(self, model_size: str = "tiny", device: str = "cpu", compute_type: str = "int8"):
        self.model_size = model_size
        self.device = device
        self.compute_type = compute_type
        self.model: Optional[WhisperModel] = None
        self.is_loaded = False
        self._load_lock = threading.Lock()

    def load_model(self):
        """Loads model into memory on demand."""
        with self._load_lock:
            if self.is_loaded:
                return
            if WhisperModel is None:
                print("[OfflineWhisper] faster-whisper is not installed.")
                return
            try:
                print(f"[OfflineWhisper] Loading local model '{self.model_size}'...")
                self.model = WhisperModel(self.model_size, device=self.device, compute_type=self.compute_type)
                self.is_loaded = True
                print("[OfflineWhisper] Local model loaded successfully.")
            except Exception as e:
                print(f"[OfflineWhisper] Model load failed: {e}")

    def transcribe_pcm_bytes(self, pcm_bytes: bytes, language: str = "ar", sample_rate: int = 16000) -> str:
        """Transcribes in-memory PCM audio chunk offline."""
        if not self.is_loaded:
            self.load_model()
        if not self.model or not pcm_bytes:
            return ""

        # Convert bytes to float32 numpy array
        audio_int16 = np.frombuffer(pcm_bytes, dtype=np.int16)
        audio_float32 = audio_int16.astype(np.float32) / 32768.0

        try:
            segments, info = self.model.transcribe(
                audio_float32,
                beam_size=1,
                language=language if language in ["ar", "en"] else None,
                vad_filter=True
            )
            text_parts = [seg.text for seg in segments]
            return " ".join(text_parts).strip()
        except Exception as e:
            print(f"[OfflineWhisper] Transcription error: {e}")
            return ""
