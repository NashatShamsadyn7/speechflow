"""
Real-Time Microphone Audio Capture
Captures 16kHz 16-bit Mono PCM audio with non-blocking stream callback.
"""

import queue
import threading
import numpy as np
import sounddevice as sd
from typing import Callable, Optional
from core.vad_detector import VadDetector

class AudioCapture:
    def __init__(
        self,
        sample_rate: int = 16000,
        chunk_size: int = 512,
        device_index: Optional[int] = None,
        on_audio_chunk: Optional[Callable[[bytes, bool, float], None]] = None
    ):
        """
        :param sample_rate: Audio sample rate in Hz (16000 recommended for STT).
        :param chunk_size: Number of samples per buffer callback (~32ms at 16kHz).
        :param device_index: Specific input device ID (None for default).
        :param on_audio_chunk: Callback(raw_pcm_bytes, is_speech_active, rms_level).
        """
        self.sample_rate = sample_rate
        self.chunk_size = chunk_size
        self.device_index = device_index
        self.on_audio_chunk = on_audio_chunk

        self.vad = VadDetector(sample_rate=sample_rate)
        self.stream: Optional[sd.InputStream] = None
        self.is_running = False
        self._audio_queue: queue.Queue = queue.Queue(maxsize=100)
        self._worker_thread: Optional[threading.Thread] = None

    def _audio_callback(self, indata, frames, time_info, status):
        """Low-latency audio callback executed in sounddevice thread."""
        if status:
            # Drop/overflow warning if needed
            pass
        if self.is_running:
            # Copy indata to avoid buffer reuse issues
            self._audio_queue.put(indata.copy())

    def _process_loop(self):
        """Worker thread that processes chunks and notifies listeners."""
        while self.is_running:
            try:
                data = self._audio_queue.get(timeout=0.1)
            except queue.Empty:
                continue

            # data is float32 [-1.0, 1.0]
            rms = float(np.sqrt(np.mean(data**2) + 1e-10))
            is_speech = self.vad.process_chunk(data.flatten())

            # Convert to 16-bit PCM bytes for STT APIs
            pcm16_data = (data.flatten() * 32767).clip(-32768, 32767).astype(np.int16)
            raw_bytes = pcm16_data.tobytes()

            if self.on_audio_chunk:
                try:
                    self.on_audio_chunk(raw_bytes, is_speech, rms)
                except Exception as e:
                    print(f"[AudioCapture] Error in chunk callback: {e}")

    def start(self):
        """Start capturing audio from microphone."""
        if self.is_running:
            return

        self.is_running = True
        self.vad.reset()
        self._worker_thread = threading.Thread(target=self._process_loop, daemon=True)
        self._worker_thread.start()

        self.stream = sd.InputStream(
            samplerate=self.sample_rate,
            channels=1,
            dtype='float32',
            blocksize=self.chunk_size,
            device=self.device_index,
            callback=self._audio_callback
        )
        self.stream.start()

    def stop(self):
        """Stop capturing audio."""
        self.is_running = False
        if self.stream:
            try:
                self.stream.stop()
                self.stream.close()
            except Exception:
                pass
            self.stream = None

    @staticmethod
    def list_input_devices():
        """Lists available input audio devices on Windows."""
        devices = sd.query_devices()
        input_devs = []
        for i, dev in enumerate(devices):
            if dev['max_input_channels'] > 0:
                input_devs.append((i, dev['name']))
        return input_devs


if __name__ == "__main__":
    import time
    print("Available Input Devices:")
    for idx, name in AudioCapture.list_input_devices():
        print(f"  [{idx}] {name}")

    def sample_cb(raw_bytes, is_speech, rms):
        meter = "#" * int(rms * 150)
        status = "SPEECH" if is_speech else "......"
        print(f"\rLevel: {meter:<30} [{status}]", end="", flush=True)

    print("\nStarting 5 second audio test...")
    cap = AudioCapture(on_audio_chunk=sample_cb)
    cap.start()
    time.sleep(5)
    cap.stop()
    print("\nAudio capture test completed.")
