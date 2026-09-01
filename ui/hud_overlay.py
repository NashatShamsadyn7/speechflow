"""
Ultra-Compact Single-Line Win+H Voice Typing Pill
Minimalist, elegant, non-intrusive floating bar (Height: 36px).
Layout: [ 🔴/⚪ Status ] [ 🎙️ Mic ] [ Mini EQ Wave ] [ AR / EN ] [ ⚙️ ]
"""

from PySide6.QtCore import Qt, QPoint, Signal, QSize
from PySide6.QtGui import QColor, QFont, QCursor, QIcon, QAction
from PySide6.QtWidgets import (
    QWidget, QHBoxLayout, QVBoxLayout, QLabel, QPushButton,
    QGraphicsDropShadowEffect, QFrame, QMenu, QDialog,
    QLineEdit, QComboBox, QFormLayout, QMessageBox, QCheckBox, QSlider
)
from ui.visualizer import AudioVisualizerWidget


class HUDOverlay(QWidget):
    # Signals for thread-safe UI updates
    interim_text_received = Signal(str)
    final_text_received = Signal(str)
    status_changed = Signal(str, str) # status_text, color_hex
    language_switched = Signal(str)

    def __init__(
        self,
        config: dict,
        on_toggle_callback=None,
        on_lang_switch_callback=None,
        on_ai_polish_callback=None,
        on_settings_save_callback=None,
        history_mgr=None,
        snippet_mgr=None
    ):
        super().__init__()
        self.config = config
        self.on_toggle_callback = on_toggle_callback
        self.on_lang_switch_callback = on_lang_switch_callback
        self.on_ai_polish_callback = on_ai_polish_callback
        self.on_settings_save_callback = on_settings_save_callback
        self.history_mgr = history_mgr
        self.snippet_mgr = snippet_mgr

        self.current_lang = self.config.get("default_language", "ar")
        self.is_recording = False
        self._drag_pos = QPoint()

        self._init_window_flags()
        self._init_ui()
        self._connect_signals()

    def _init_window_flags(self):
        self.setWindowFlags(
            Qt.WindowStaysOnTopHint |
            Qt.FramelessWindowHint |
            Qt.Tool
        )
        self.setAttribute(Qt.WA_TranslucentBackground, True)
        self.setAttribute(Qt.WA_ShowWithoutActivating, True)

    def _init_ui(self):
        # Clean zero-margin root layout
        root_layout = QVBoxLayout(self)
        root_layout.setContentsMargins(6, 6, 6, 6)
        root_layout.setSpacing(0)

        # Ultra-Compact Single-Line Acrylic Pill Frame (Height ~38px)
        self.pill = QFrame(self)
        self.pill.setObjectName("pill_bar")
        self.pill.setStyleSheet("""
            QFrame#pill_bar {
                background: qlineargradient(x1:0, y1:0, x2:1, y2:1,
                    stop:0 rgba(17, 24, 39, 0.94),
                    stop:1 rgba(10, 15, 26, 0.97));
                border: 1px solid rgba(255, 255, 255, 0.16);
                border-radius: 19px;
            }
        """)

        # Sleek drop shadow
        shadow = QGraphicsDropShadowEffect(self)
        shadow.setBlurRadius(20)
        shadow.setColor(QColor(0, 0, 0, 180))
        shadow.setOffset(0, 4)
        self.pill.setGraphicsEffect(shadow)

        # Single-Line Horizontal Layout
        bar_layout = QHBoxLayout(self.pill)
        bar_layout.setContentsMargins(10, 4, 10, 4)
        bar_layout.setSpacing(8)

        # 1. Status Indicator Dot
        self.status_dot = QLabel()
        self.status_dot.setFixedSize(8, 8)
        self._set_dot_color("#6b7280") # Idle

        # 2. Main Mic Action Button
        self.mic_btn = QPushButton("🎙️")
        self.mic_btn.setFixedSize(30, 30)
        self.mic_btn.setCursor(QCursor(Qt.PointingHandCursor))
        self.mic_btn.setToolTip("Start / Stop Dictation (Ctrl+Shift+Space)")
        self.mic_btn.setStyleSheet(self._get_mic_btn_style(is_active=False))
        self.mic_btn.clicked.connect(self._on_mic_clicked)

        # 3. Mini EQ Visualizer (8 bars)
        self.visualizer = AudioVisualizerWidget(bar_count=8, parent=self)
        self.visualizer.setFixedSize(54, 18)

        # 4. Language Switch Pill (AR / EN)
        self.lang_btn = QPushButton("AR" if self.current_lang == "ar" else "EN")
        self.lang_btn.setFixedSize(36, 26)
        self.lang_btn.setCursor(QCursor(Qt.PointingHandCursor))
        self.lang_btn.setToolTip("Switch Language (Ctrl+Shift+L)")
        self.lang_btn.setStyleSheet(self._get_lang_btn_style(self.current_lang))
        self.lang_btn.clicked.connect(self._on_lang_clicked)

        # 5. Quick Settings / Tools Icon Button
        self.tools_btn = QPushButton("⚙️")
        self.tools_btn.setFixedSize(26, 26)
        self.tools_btn.setCursor(QCursor(Qt.PointingHandCursor))
        self.tools_btn.setToolTip("Settings & Tools (Right-click bar for menu)")
        self.tools_btn.setStyleSheet("""
            QPushButton {
                background: rgba(255, 255, 255, 0.06);
                color: #94a3b8;
                border: 1px solid rgba(255, 255, 255, 0.1);
                border-radius: 13px;
                font-size: 11px;
                padding: 0;
            }
            QPushButton:hover {
                background: rgba(255, 255, 255, 0.15);
                color: white;
            }
        """)
        self.tools_btn.clicked.connect(self._open_quick_menu)

        bar_layout.addWidget(self.status_dot)
        bar_layout.addWidget(self.mic_btn)
        bar_layout.addWidget(self.visualizer)
        bar_layout.addWidget(self.lang_btn)
        bar_layout.addWidget(self.tools_btn)

        root_layout.addWidget(self.pill)
        self.setFixedSize(210, 52)

    def _get_mic_btn_style(self, is_active: bool) -> str:
        if is_active:
            return """
                QPushButton {
                    background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 #ef4444, stop:1 #dc2626);
                    color: white;
                    border: none;
                    border-radius: 15px;
                    font-size: 13px;
                }
                QPushButton:hover { background: #f87171; }
            """
        return """
            QPushButton {
                background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 #10b981, stop:1 #059669);
                color: white;
                border: none;
                border-radius: 15px;
                font-size: 13px;
            }
            QPushButton:hover { background: #34d399; }
        """

    def _get_lang_btn_style(self, lang: str) -> str:
        if lang == "ar":
            return """
                QPushButton {
                    background: rgba(59, 130, 246, 0.22);
                    color: #60a5fa;
                    border: 1px solid rgba(96, 165, 250, 0.4);
                    border-radius: 13px;
                    font-size: 11px;
                    font-weight: bold;
                }
                QPushButton:hover { background: rgba(59, 130, 246, 0.35); }
            """
        return """
            QPushButton {
                background: rgba(168, 85, 247, 0.22);
                color: #c084fc;
                border: 1px solid rgba(192, 132, 252, 0.4);
                border-radius: 13px;
                font-size: 11px;
                font-weight: bold;
            }
            QPushButton:hover { background: rgba(168, 85, 247, 0.35); }
        """

    def _set_dot_color(self, hex_color: str):
        self.status_dot.setStyleSheet(f"background-color: {hex_color}; border-radius: 4px;")

    def _connect_signals(self):
        self.status_changed.connect(self._on_status_changed)
        self.language_switched.connect(self.update_language_display)

    def _on_mic_clicked(self):
        if self.on_toggle_callback:
            self.on_toggle_callback()

    def _on_lang_clicked(self):
        new_lang = "en" if self.current_lang == "ar" else "ar"
        self.current_lang = new_lang
        self.update_language_display(new_lang)
        if self.on_lang_switch_callback:
            self.on_lang_switch_callback(new_lang)

    def update_language_display(self, lang: str):
        self.current_lang = lang
        self.lang_btn.setText("AR" if lang == "ar" else "EN")
        self.lang_btn.setStyleSheet(self._get_lang_btn_style(lang))

    def set_recording_state(self, is_recording: bool):
        self.is_recording = is_recording
        self.mic_btn.setText("⏹️" if is_recording else "🎙️")
        self.mic_btn.setStyleSheet(self._get_mic_btn_style(is_recording))
        self._set_dot_color("#ef4444" if is_recording else "#6b7280")

    def _on_status_changed(self, text: str, color_hex: str):
        self._set_dot_color(color_hex)

    def _open_quick_menu(self):
        menu = QMenu(self)
        menu.setStyleSheet("""
            QMenu {
                background: #0f172a;
                color: #f1f5f9;
                border: 1px solid #334155;
                border-radius: 8px;
                padding: 4px;
            }
            QMenu::item {
                padding: 6px 16px;
                border-radius: 4px;
                font-size: 12px;
            }
            QMenu::item:selected {
                background: #2563eb;
            }
        """)

        s_action = menu.addAction("⚙️ Settings...")
        s_action.triggered.connect(self.open_settings_dialog)

        menu.addSeparator()

        h_action = menu.addAction("📜 Copy Last Transcription")
        h_action.triggered.connect(self._copy_last_transcription)

        q_action = menu.addAction("❌ Quit SpeechFlow")
        q_action.triggered.connect(lambda: QApplication.instance().quit())

        menu.exec(QCursor.pos())

    def contextMenuEvent(self, event):
        self._open_quick_menu()

    def _copy_last_transcription(self):
        if self.history_mgr and self.history_mgr.entries:
            last = self.history_mgr.entries[0]["text"]
            import win32clipboard, win32con
            win32clipboard.OpenClipboard()
            win32clipboard.EmptyClipboard()
            win32clipboard.SetClipboardText(last, win32con.CF_UNICODETEXT)
            win32clipboard.CloseClipboard()

    def open_settings_dialog(self):
        dialog = QDialog(self)
        dialog.setWindowTitle("SpeechFlow Settings")
        dialog.setFixedWidth(360)
        dialog.setStyleSheet("""
            QDialog { background: #0f172a; color: #f8fafc; }
            QLabel { color: #cbd5e1; font-size: 12px; }
            QLineEdit, QComboBox {
                background: #1e293b; color: white;
                border: 1px solid #475569; border-radius: 6px;
                padding: 5px; font-size: 12px;
            }
            QPushButton {
                background: #2563eb; color: white;
                border-radius: 6px; padding: 7px 14px; font-weight: bold;
            }
            QPushButton:hover { background: #3b82f6; }
        """)

        form = QFormLayout(dialog)
        form.setContentsMargins(16, 16, 16, 16)
        form.setSpacing(10)

        dg_input = QLineEdit(self.config.get("deepgram_api_key", ""))
        dg_input.setEchoMode(QLineEdit.Password)
        dg_input.setPlaceholderText("Optional: Deepgram API Key")

        groq_input = QLineEdit(self.config.get("groq_api_key", ""))
        groq_input.setEchoMode(QLineEdit.Password)
        groq_input.setPlaceholderText("Optional: Groq API Key")

        engine_box = QComboBox()
        engine_box.addItems(["Local Offline Whisper (Zero Cost)", "Deepgram Nova-2 (WebSocket)", "Groq Whisper-Turbo"])

        chimes_cb = QCheckBox("Audio Chimes (Start/Stop tones)")
        chimes_cb.setChecked(self.config.get("sound_effects", True))

        save_btn = QPushButton("Save Settings")

        def save():
            engine = "offline"
            if "Deepgram" in engine_box.currentText(): engine = "deepgram"
            elif "Groq" in engine_box.currentText(): engine = "groq"

            new_s = {
                "deepgram_api_key": dg_input.text().strip(),
                "groq_api_key": groq_input.text().strip(),
                "primary_engine": engine,
                "sound_effects": chimes_cb.isChecked()
            }
            if self.on_settings_save_callback:
                self.on_settings_save_callback(new_s)
            dialog.accept()

        save_btn.clicked.connect(save)

        form.addRow("Deepgram Key:", dg_input)
        form.addRow("Groq Key:", groq_input)
        form.addRow("Engine:", engine_box)
        form.addRow("Sounds:", chimes_cb)
        form.addRow(save_btn)

        dialog.exec()

    # Draggable Window Logic
    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self._drag_pos = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.LeftButton and not self._drag_pos.isNull():
            self.move(event.globalPosition().toPoint() - self._drag_pos)
            event.accept()
