"""
Voice Activity Detection (VAD) Engine
Provides low-latency speech detection using adaptive energy thresholding and Silero VAD compatibility.
"""

import numpy as np

class VadDetector:
    def __init__(self, sample_rate: int = 16000, energy_threshold: float = 0.012, silence_duration_sec: float = 0.5):
        self.sample_rate = sample_rate
        self.energy_threshold = energy_threshold
        self.silence_limit_chunks = int((silence_duration_sec * sample_rate) / 512)
        
        self.silence_counter = 0
        self.is_speech_active = False
        self.adaptive_noise_floor = 0.005

    def process_chunk(self, audio_data: np.ndarray) -> bool:
        """
        Determines if an audio chunk contains active speech.
        :param audio_data: 1D numpy array of float32 or int16 PCM audio samples.
        :return: True if active speech is detected or during silence hangover.
        """
        if audio_data.dtype == np.int16:
            samples = audio_data.astype(np.float32) / 32768.0
        else:
            samples = audio_data

        # Calculate Root Mean Square (RMS) energy
        rms = np.sqrt(np.mean(samples**2) + 1e-10)

        # Dynamic noise floor adaptation during low energy
        if rms < self.adaptive_noise_floor * 1.5:
            self.adaptive_noise_floor = 0.95 * self.adaptive_noise_floor + 0.05 * rms

        current_threshold = max(self.energy_threshold, self.adaptive_noise_floor * 2.5)

        if rms > current_threshold:
            self.is_speech_active = True
            self.silence_counter = 0
            return True
        else:
            if self.is_speech_active:
                self.silence_counter += 1
                if self.silence_counter <= self.silence_limit_chunks:
                    # Hangover period (avoids chopping sentence ends)
                    return True
                else:
                    self.is_speech_active = False
                    return False
            return False

    def reset(self):
        self.silence_counter = 0
        self.is_speech_active = False
