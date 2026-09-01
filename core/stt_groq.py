"""
Groq Whisper-Large-v3-Turbo STT Engine
Ultra-fast chunked and push-to-talk speech-to-text with exceptional Arabic and English precision.
Infers 5-second audio chunks in ~150-250ms.
"""

import io
import wave
import threading
from typing import Callable, Optional
import numpy as np

try:
    from groq import Groq
except ImportError:
    Groq = None

class GroqWhisperClient:
    def __init__(
        self,
        api_key: str,
        language: str = "ar",
        model: str = "whisper-large-v3-turbo",
        on_transcription: Optional[Callable[[str], None]] = None,
        on_error: Optional[Callable[[str], None]] = None
    ):
        self.api_key = api_key
        self.language = language
        self.model = model
        self.on_transcription = on_transcription
        self.on_error = on_error

        self.client = Groq(api_key=self.api_key) if (Groq and api_key) else None

    def set_language(self, language: str):
        self.language = language

    def transcribe_pcm_bytes(self, pcm_bytes: bytes, sample_rate: int = 16000) -> Optional[str]:
        """
        Converts in-memory 16-bit PCM bytes into a virtual WAV file and sends to Groq API.
        """
        if not self.client or not pcm_bytes:
            return None

        # Build in-memory WAV container
        wav_buffer = io.BytesIO()
        with wave.open(wav_buffer, 'wb') as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)  # 16-bit
            wf.setframerate(sample_rate)
            wf.writeframes(pcm_bytes)
        
        wav_buffer.seek(0)
        wav_buffer.name = "audio.wav"

        try:
            params = {
                "file": wav_buffer,
                "model": self.model,
                "response_format": "text",
                "temperature": 0.0,
            }
            if self.language in ["ar", "en"]:
                params["language"] = self.language

            transcription = self.client.audio.transcriptions.create(**params)
            result_text = str(transcription).strip()
            
            if self.on_transcription and result_text:
                self.on_transcription(result_text)
            
            return result_text
        except Exception as e:
            err_msg = f"[GroqWhisper] Error: {e}"
            if self.on_error:
                self.on_error(err_msg)
            return None

    def transcribe_async(self, pcm_bytes: bytes, sample_rate: int = 16000):
        """Dispatches transcription in a non-blocking background thread."""
        thread = threading.Thread(
            target=self.transcribe_pcm_bytes,
            args=(pcm_bytes, sample_rate),
            daemon=True
        )
        thread.start()
