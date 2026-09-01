"""
Audio Chimes & Earcons Feedback Module
Provides subtle, non-intrusive sound effects for voice typing events.
Uses native Windows winsound asynchronously with zero external dependencies.
"""

import threading
import winsound
from typing import Optional

class SoundEffects:
    def __init__(self, enabled: bool = True):
        self.enabled = enabled

    def play_start_chime(self):
        """Pleasant rising tone for listening start (Win+H style)."""
        if not self.enabled:
            return
        def _play():
            try:
                winsound.Beep(523, 60)  # C5
                winsound.Beep(659, 80)  # E5
            except Exception:
                pass
        threading.Thread(target=_play, daemon=True).start()

    def play_stop_chime(self):
        """Pleasant descending tone for listening stop."""
        if not self.enabled:
            return
        def _play():
            try:
                winsound.Beep(659, 60)  # E5
                winsound.Beep(440, 80)  # A4
            except Exception:
                pass
        threading.Thread(target=_play, daemon=True).start()

    def play_switch_chime(self):
        """Subtle double-click tone for language switch."""
        if not self.enabled:
            return
        def _play():
            try:
                winsound.Beep(880, 40)
            except Exception:
                pass
        threading.Thread(target=_play, daemon=True).start()

    def play_error_chime(self):
        """Low frequency error tone."""
        if not self.enabled:
            return
        def _play():
            try:
                winsound.Beep(300, 150)
            except Exception:
                pass
        threading.Thread(target=_play, daemon=True).start()
