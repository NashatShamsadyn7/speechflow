"""
Audio Waveform Visualizer Widget
Renders smooth, reactive audio bars for the floating HUD overlay.
"""

import math
import numpy as np
from PySide6.QtCore import Qt, QTimer
from PySide6.QtGui import QPainter, QColor, QBrush, QPen, QLinearGradient
from PySide6.QtWidgets import QWidget

class AudioVisualizerWidget(QWidget):
    def __init__(self, bar_count: int = 14, parent=None):
        super().__init__(parent)
        self.bar_count = bar_count
        self.levels = [0.0] * self.bar_count
        self.target_level = 0.0
        self.phase = 0.0
        self.is_active = False

        self.setMinimumSize(80, 24)
        self.setMaximumHeight(32)

        # 60 FPS animation timer for smooth interpolation
        self.timer = QTimer(self)
        self.timer.timeout.connect(self._animate)
        self.timer.start(16)

    def set_audio_level(self, rms: float, is_speech: bool):
        """Updates target audio level from audio capture callback."""
        self.is_active = is_speech
        # Normalize and boost RMS for visual impact
        self.target_level = min(1.0, max(0.05 if is_speech else 0.0, rms * 8.0))

    def _animate(self):
        self.phase += 0.15
        for i in range(self.bar_count):
            if self.is_active:
                # Wave modulation
                wave = math.sin(self.phase + i * 0.5) * 0.3 + 0.7
                target_bar = self.target_level * wave
            else:
                target_bar = 0.05

            # Smooth exponential decay / attack
            self.levels[i] += (target_bar - self.levels[i]) * 0.35

        self.update()

    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        width = self.width()
        height = self.height()
        bar_width = max(2.5, (width - (self.bar_count - 1) * 2.5) / self.bar_count)
        spacing = 2.5

        for i in range(self.bar_count):
            bar_h = max(3.0, self.levels[i] * (height - 4))
            x = i * (bar_width + spacing)
            y = (height - bar_h) / 2

            # Vibrant gradient (Emerald to Cyan or Neon Coral depending on state)
            gradient = QLinearGradient(x, y, x, y + bar_h)
            if self.is_active:
                gradient.setColorAt(0.0, QColor(0, 255, 170, 240))  # Neon mint
                gradient.setColorAt(1.0, QColor(0, 180, 255, 240))  # Cyan
            else:
                gradient.setColorAt(0.0, QColor(120, 130, 150, 100))
                gradient.setColorAt(1.0, QColor(80, 90, 110, 80))

            painter.setPen(Qt.NoPen)
            painter.setBrush(QBrush(gradient))
            painter.drawRoundedRect(x, y, bar_width, bar_h, 1.5, 1.5)

        painter.end()
