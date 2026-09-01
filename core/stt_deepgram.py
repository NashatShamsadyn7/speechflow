"""
Deepgram Nova-2 Streaming WebSocket STT Client
Real-time bi-directional streaming speech-to-text with sub-200ms latency.
Supports Arabic and English with interim and final transcript streaming.
"""

import asyncio
import json
import threading
import urllib.parse
from typing import Callable, Optional
import websockets

class DeepgramStreamClient:
    def __init__(
        self,
        api_key: str,
        language: str = "ar",
        model: str = "nova-2",
        sample_rate: int = 16000,
        on_interim_text: Optional[Callable[[str], None]] = None,
        on_final_text: Optional[Callable[[str, bool], None]] = None,
        on_status_change: Optional[Callable[[str], None]] = None,
        on_error: Optional[Callable[[str], None]] = None
    ):
        """
        :param api_key: Deepgram API Key.
        :param language: 'ar' (Arabic) or 'en' (English).
        :param model: 'nova-2' or 'nova-3'
        :param sample_rate: Audio sample rate in Hz.
        :param on_interim_text: Callback(interim_text) for live preview.
        :param on_final_text: Callback(final_text, is_speech_final) for finalized words.
        """
        self.api_key = api_key
        self.language = language
        self.model = model
        self.sample_rate = sample_rate

        self.on_interim_text = on_interim_text
        self.on_final_text = on_final_text
        self.on_status_change = on_status_change
        self.on_error = on_error

        self._loop: Optional[asyncio.AbstractEventLoop] = None
        self._thread: Optional[threading.Thread] = None
        self._ws: Optional[websockets.WebSocketClientProtocol] = None
        self._audio_queue: asyncio.Queue = None
        self._is_running = False
        self._is_connected = False

    def set_language(self, language: str):
        """Switches active language ('ar' or 'en') and reconnects if active."""
        if self.language != language:
            self.language = language
            if self._is_running:
                self.restart()

    def _build_ws_url(self) -> str:
        base_url = "wss://api.deepgram.com/v1/listen"
        params = {
            "model": self.model,
            "language": self.language,
            "encoding": "linear16",
            "sample_rate": str(self.sample_rate),
            "channels": "1",
            "smart_format": "true",
            "interim_results": "true",
            "punctuate": "true",
            "endpointing": "250"
        }
        return f"{base_url}?{urllib.parse.urlencode(params)}"

    async def _sender(self):
        """Sends raw PCM audio chunks from queue to WebSocket."""
        while self._is_running and self._is_connected:
            try:
                chunk = await self._audio_queue.get()
                if chunk is None:
                    break
                if self._ws:
                    await self._ws.send(chunk)
            except Exception as e:
                if self._is_running:
                    print(f"[Deepgram] Audio sender error: {e}")
                break

    async def _receiver(self):
        """Receives and parses live JSON transcription packets."""
        while self._is_running and self._is_connected:
            try:
                msg = await self._ws.recv()
                data = json.loads(msg)
                
                # Check for Deepgram transcription structure
                channel = data.get("channel", {})
                alternatives = channel.get("alternatives", [])
                if not alternatives:
                    continue

                alt = alternatives[0]
                transcript = alt.get("transcript", "").strip()
                if not transcript:
                    continue

                is_final = data.get("is_final", False)
                speech_final = data.get("speech_final", False)

                if is_final or speech_final:
                    if self.on_final_text:
                        self.on_final_text(transcript, speech_final)
                else:
                    if self.on_interim_text:
                        self.on_interim_text(transcript)

            except websockets.ConnectionClosed:
                break
            except Exception as e:
                if self._is_running:
                    print(f"[Deepgram] Receiver error: {e}")
                break

    async def _run_ws(self):
        url = self._build_ws_url()
        headers = {"Authorization": f"Token {self.api_key}"}

        if self.on_status_change:
            self.on_status_change("Connecting...")

        while self._is_running:
            try:
                async with websockets.connect(url, extra_headers=headers, ping_interval=20, ping_timeout=10) as ws:
                    self._ws = ws
                    self._is_connected = True
                    self._audio_queue = asyncio.Queue()

                    if self.on_status_change:
                        self.on_status_change("Connected")

                    # Run sender and receiver concurrently
                    await asyncio.gather(self._sender(), self._receiver())

            except Exception as e:
                self._is_connected = False
                if self._is_running:
                    err_msg = f"Connection failed: {str(e)}"
                    if self.on_error:
                        self.on_error(err_msg)
                    if self.on_status_change:
                        self.on_status_change("Reconnecting in 2s...")
                    await asyncio.sleep(2)
                else:
                    break

        self._is_connected = False
        if self.on_status_change:
            self.on_status_change("Disconnected")

    def _event_loop_thread(self):
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        try:
            self._loop.run_until_complete(self._run_ws())
        finally:
            self._loop.close()

    def start(self):
        """Starts the WebSocket connection in background thread."""
        if self._is_running:
            return
        self._is_running = True
        self._thread = threading.Thread(target=self._event_loop_thread, daemon=True)
        self._thread.start()

    def stop(self):
        """Gracefully disconnects and stops streaming."""
        self._is_running = False
        self._is_connected = False
        if self._loop and self._audio_queue:
            try:
                self._loop.call_soon_threadsafe(self._audio_queue.put_nowait, None)
            except Exception:
                pass

    def restart(self):
        """Restarts connection (e.g. after language or parameter change)."""
        self.stop()
        threading.Timer(0.3, self.start).start()

    def send_audio(self, raw_pcm_bytes: bytes):
        """Enqueues raw 16kHz PCM audio bytes for streaming."""
        if self._is_running and self._is_connected and self._loop and self._audio_queue:
            try:
                self._loop.call_soon_threadsafe(self._audio_queue.put_nowait, raw_pcm_bytes)
            except Exception:
                pass
