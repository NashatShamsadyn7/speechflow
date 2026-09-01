"""
Microsoft Win+H Style Advanced Floating Voice Typing Bar
Customizable 3-button layout, Windows 11 Mica/Acrylic styling, live visualizer,
and expandable glassmorphic drawer for AI Polisher, History, Snippets, and Settings.
"""

from PySide6.QtCore import Qt, QPoint, Signal, QSize
from PySide6.QtGui import QColor, QFont, QCursor, QIcon
from PySide6.QtWidgets import (
    QWidget, QHBoxLayout, QVBoxLayout, QLabel, QPushButton,
    QGraphicsDropShadowEffect, QFrame, QTabWidget, QLineEdit,
    QComboBox, QListWidget, QListWidgetItem, QTextEdit, QCheckBox,
    QSlider, QFormLayout, QMessageBox, QSplitter
)
from ui.visualizer import AudioVisualizerWidget

class HUDOverlay(QWidget):
    # Signals
    interim_text_received = Signal(str)
    final_text_received = Signal(str)
    status_changed = Signal(str, str)
    language_switched = Signal(str)
    drawer_toggled = Signal(bool)

    def __init__(
        self,
        config: dict,
        on_toggle_callback=None,
        on_lang_switch_callback=None,
        on_ai_polish_callback=None,
        on_settings_save_callback=None,
        on_device_change_callback=None,
        history_mgr=None,
        snippet_mgr=None
    ):
        super().__init__()
        self.config = config
        self.on_toggle_callback = on_toggle_callback
        self.on_lang_switch_callback = on_lang_switch_callback
        self.on_ai_polish_callback = on_ai_polish_callback
        self.on_settings_save_callback = on_settings_save_callback
        self.on_device_change_callback = on_device_change_callback
        self.history_mgr = history_mgr
        self.snippet_mgr = snippet_mgr

        self.current_lang = self.config.get("default_language", "ar")
        self.is_recording = False
        self.drawer_expanded = False
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
        self.main_layout = QVBoxLayout(self)
        self.main_layout.setContentsMargins(10, 10, 10, 10)
        self.main_layout.setSpacing(6)

        # Win+H Style Acrylic Container
        self.container = QFrame(self)
        self.container.setObjectName("win_h_bar")
        self.container.setStyleSheet("""
            QFrame#win_h_bar {
                background: qlineargradient(x1:0, y1:0, x2:1, y2:1,
                    stop:0 rgba(18, 24, 38, 0.96),
                    stop:1 rgba(11, 15, 25, 0.98));
                border: 1px solid rgba(255, 255, 255, 0.14);
                border-radius: 22px;
            }
        """)

        shadow = QGraphicsDropShadowEffect(self)
        shadow.setBlurRadius(30)
        shadow.setColor(QColor(0, 0, 0, 200))
        shadow.setOffset(0, 8)
        self.container.setGraphicsEffect(shadow)

        self.container_layout = QVBoxLayout(self.container)
        self.container_layout.setContentsMargins(14, 10, 14, 10)
        self.container_layout.setSpacing(6)

        # ----------------------------------------------------
        # TOP BAR: 3 Customizable Action Buttons + Visualizer
        # ----------------------------------------------------
        top_bar = QHBoxLayout()
        top_bar.setContentsMargins(0, 0, 0, 0)
        top_bar.setSpacing(10)

        # Status indicator dot
        self.status_dot = QLabel()
        self.status_dot.setFixedSize(10, 10)
        self._set_dot_color("#6b7280")

        # Visualizer
        self.visualizer = AudioVisualizerWidget(bar_count=10, parent=self)

        # ==========================================
        # BUTTON 1: Main Action (Default: Mic Toggle)
        # ==========================================
        self.btn1 = QPushButton("🎙️ Start")
        self.btn1.setCursor(QCursor(Qt.PointingHandCursor))
        self.btn1.setStyleSheet(self._get_mic_btn_style(is_active=False))
        self.btn1.clicked.connect(self._on_btn1_clicked)

        # ==========================================
        # BUTTON 2: Center Action (Default: Language)
        # ==========================================
        self.btn2 = QPushButton("العربية (AR)" if self.current_lang == "ar" else "English (EN)")
        self.btn2.setCursor(QCursor(Qt.PointingHandCursor))
        self.btn2.setStyleSheet(self._get_lang_btn_style(self.current_lang))
        self.btn2.clicked.connect(self._on_btn2_clicked)

        # ==========================================
        # BUTTON 3: Right Action (Default: AI Tools / Drawer)
        # ==========================================
        self.btn3 = QPushButton("⚡ Tools")
        self.btn3.setCursor(QCursor(Qt.PointingHandCursor))
        self.btn3.setStyleSheet("""
            QPushButton {
                background: rgba(255, 255, 255, 0.08);
                color: #f1f5f9;
                border: 1px solid rgba(255, 255, 255, 0.15);
                border-radius: 12px;
                padding: 4px 10px;
                font-size: 11px;
                font-weight: 600;
            }
            QPushButton:hover {
                background: rgba(255, 255, 255, 0.18);
                border-color: #38bdf8;
            }
        """)
        self.btn3.clicked.connect(self._on_btn3_clicked)

        top_bar.addWidget(self.status_dot)
        top_bar.addWidget(self.visualizer)
        top_bar.addWidget(self.btn1)
        top_bar.addWidget(self.btn2)
        top_bar.addWidget(self.btn3)

        # ----------------------------------------------------
        # LIVE INTERIM TRANSCRIPTION TICKER / PREVIEW
        # ----------------------------------------------------
        self.preview_label = QLabel("Win+H Ready • Press Ctrl+Shift+Space to Dictate")
        self.preview_label.setAlignment(Qt.AlignCenter)
        self.preview_label.setWordWrap(True)
        self.preview_label.setStyleSheet("""
            QLabel {
                color: #e2e8f0;
                font-size: 12px;
                font-family: 'Segoe UI', Arial, sans-serif;
                padding: 4px 10px;
                background: rgba(0, 0, 0, 0.3);
                border-radius: 10px;
            }
        """)

        self.container_layout.addLayout(top_bar)
        self.container_layout.addWidget(self.preview_label)

        # ----------------------------------------------------
        # EXPANDABLE DRAWER (History, AI Polisher, Snippets, Settings)
        # ----------------------------------------------------
        self.drawer_widget = QWidget()
        self.drawer_layout = QVBoxLayout(self.drawer_widget)
        self.drawer_layout.setContentsMargins(0, 8, 0, 0)
        self.drawer_layout.setSpacing(6)

        self.tabs = QTabWidget()
        self.tabs.setStyleSheet("""
            QTabWidget::pane {
                border: 1px solid rgba(255, 255, 255, 0.1);
                background: rgba(15, 23, 42, 0.9);
                border-radius: 10px;
                padding: 6px;
            }
            QTabBar::tab {
                background: rgba(30, 41, 59, 0.8);
                color: #94a3b8;
                border-radius: 6px;
                padding: 4px 12px;
                margin-right: 4px;
                font-size: 11px;
                font-weight: bold;
            }
            QTabBar::tab:selected {
                background: #2563eb;
                color: white;
            }
        """)

        self._build_history_tab()
        self._build_ai_tab()
        self._build_snippets_tab()
        self._build_settings_tab()

        self.drawer_layout.addWidget(self.tabs)
        self.drawer_widget.setVisible(False)
        self.container_layout.addWidget(self.drawer_widget)

        self.main_layout.addWidget(self.container)
        self.setFixedWidth(460)
        self.adjustSize()

    def _get_mic_btn_style(self, is_active: bool) -> str:
        if is_active:
            return """
                QPushButton {
                    background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 #ef4444, stop:1 #dc2626);
                    color: #ffffff;
                    border: none;
                    border-radius: 12px;
                    padding: 4px 12px;
                    font-size: 11px;
                    font-weight: bold;
                }
                QPushButton:hover { background: #f87171; }
            """
        return """
            QPushButton {
                background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 #10b981, stop:1 #059669);
                color: #ffffff;
                border: none;
                border-radius: 12px;
                padding: 4px 12px;
                font-size: 11px;
                font-weight: bold;
            }
            QPushButton:hover { background: #34d399; }
        """

    def _get_lang_btn_style(self, lang: str) -> str:
        if lang == "ar":
            return """
                QPushButton {
                    background: rgba(59, 130, 246, 0.25);
                    color: #60a5fa;
                    border: 1px solid rgba(96, 165, 250, 0.4);
                    border-radius: 12px;
                    padding: 4px 10px;
                    font-size: 11px;
                    font-weight: bold;
                }
                QPushButton:hover { background: rgba(59, 130, 246, 0.4); }
            """
        return """
            QPushButton {
                background: rgba(168, 85, 247, 0.25);
                color: #c084fc;
                border: 1px solid rgba(192, 132, 252, 0.4);
                border-radius: 12px;
                padding: 4px 10px;
                font-size: 11px;
                font-weight: bold;
            }
            QPushButton:hover { background: rgba(168, 85, 247, 0.4); }
        """

    def _build_history_tab(self):
        tab = QWidget()
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(6, 6, 6, 6)

        self.history_list = QListWidget()
        self.history_list.setStyleSheet("""
            QListWidget {
                background: #0f172a;
                color: #f1f5f9;
                border: 1px solid #334155;
                border-radius: 6px;
                font-size: 11px;
            }
            QListWidget::item { padding: 6px; border-bottom: 1px solid #1e293b; }
            QListWidget::item:selected { background: #1e40af; }
        """)

        btn_bar = QHBoxLayout()
        copy_btn = QPushButton("📋 Copy Selected")
        clear_btn = QPushButton("🗑️ Clear History")
        for b in [copy_btn, clear_btn]:
            b.setStyleSheet("background: #334155; color: white; border-radius: 4px; padding: 4px 8px; font-size: 11px;")

        copy_btn.clicked.connect(self._copy_selected_history)
        clear_btn.clicked.connect(self._clear_history)

        btn_bar.addWidget(copy_btn)
        btn_bar.addWidget(clear_btn)

        layout.addWidget(self.history_list)
        layout.addLayout(btn_bar)
        self.tabs.addTab(tab, "📜 History")

    def _build_ai_tab(self):
        tab = QWidget()
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(6, 6, 6, 6)

        self.ai_input = QTextEdit()
        self.ai_input.setPlaceholderText("Enter text or last dictation will appear here...")
        self.ai_input.setFixedHeight(50)
        self.ai_input.setStyleSheet("background: #0f172a; color: white; border: 1px solid #334155; border-radius: 6px; font-size: 11px;")

        grid = QHBoxLayout()
        grammar_btn = QPushButton("✨ Fix Grammar")
        formal_btn = QPushButton("👔 Professional")
        bullets_btn = QPushButton("📋 Bullets")
        trans_btn = QPushButton("🌐 Translate")

        for b in [grammar_btn, formal_btn, bullets_btn, trans_btn]:
            b.setStyleSheet("background: #1e3a8a; color: #93c5fd; border-radius: 4px; padding: 4px 6px; font-size: 10px; font-weight: bold;")
        
        grammar_btn.clicked.connect(lambda: self._trigger_ai("grammar"))
        formal_btn.clicked.connect(lambda: self._trigger_ai("professional"))
        bullets_btn.clicked.connect(lambda: self._trigger_ai("bullets"))
        trans_btn.clicked.connect(lambda: self._trigger_ai("translate_ar_en"))

        grid.addWidget(grammar_btn)
        grid.addWidget(formal_btn)
        grid.addWidget(bullets_btn)
        grid.addWidget(trans_btn)

        layout.addWidget(self.ai_input)
        layout.addLayout(grid)
        self.tabs.addTab(tab, "🪄 AI Magic")

    def _build_snippets_tab(self):
        tab = QWidget()
        layout = QVBoxLayout(tab)
        layout.setContentsMargins(6, 6, 6, 6)

        self.snippet_list = QListWidget()
        self.snippet_list.setStyleSheet("background: #0f172a; color: white; border: 1px solid #334155; border-radius: 6px; font-size: 11px;")

        form = QHBoxLayout()
        self.snip_trigger = QLineEdit()
        self.snip_trigger.setPlaceholderText("Trigger (e.g. بريدي)")
        self.snip_expansion = QLineEdit()
        self.snip_expansion.setPlaceholderText("Expanded text...")
        add_btn = QPushButton("➕ Add")

        for w in [self.snip_trigger, self.snip_expansion]:
            w.setStyleSheet("background: #0f172a; color: white; border: 1px solid #334155; border-radius: 4px; padding: 4px; font-size: 11px;")
        add_btn.setStyleSheet("background: #10b981; color: white; border-radius: 4px; padding: 4px 10px; font-weight: bold;")
        add_btn.clicked.connect(self._add_custom_snippet)

        form.addWidget(self.snip_trigger)
        form.addWidget(self.snip_expansion)
        form.addWidget(add_btn)

        layout.addWidget(self.snippet_list)
        layout.addLayout(form)
        self.tabs.addTab(tab, "📑 Snippets")
        self._refresh_snippets_ui()

    def _build_settings_tab(self):
        tab = QWidget()
        layout = QFormLayout(tab)
        layout.setContentsMargins(8, 8, 8, 8)
        layout.setSpacing(6)

        self.dg_key_input = QLineEdit(self.config.get("deepgram_api_key", ""))
        self.dg_key_input.setEchoMode(QLineEdit.Password)
        self.groq_key_input = QLineEdit(self.config.get("groq_api_key", ""))
        self.groq_key_input.setEchoMode(QLineEdit.Password)

        self.engine_combo = QComboBox()
        self.engine_combo.addItems(["Deepgram Nova-2 (WebSocket)", "Groq Whisper-Turbo", "Offline Local Whisper"])

        self.chime_check = QCheckBox("Sound Effects & Chimes")
        self.chime_check.setChecked(self.config.get("sound_effects", True))

        self.vad_timeout_slider = QSlider(Qt.Horizontal)
        self.vad_timeout_slider.setRange(2, 30)
        self.vad_timeout_slider.setValue(self.config.get("auto_sleep_sec", 10))

        # Button 1/2/3 Customization
        self.btn1_action_combo = QComboBox()
        self.btn1_action_combo.addItems(["🎙️ Toggle Mic", "🎙️ Push to Talk", "⚡ AI Polish", "🌐 Switch Lang"])
        
        self.btn2_action_combo = QComboBox()
        self.btn2_action_combo.addItems(["🌐 Switch Lang", "🎙️ Toggle Mic", "⚡ AI Polish"])

        self.btn3_action_combo = QComboBox()
        self.btn3_action_combo.addItems(["⚡ Tools Drawer", "✨ AI Grammar", "📋 Last History"])

        for w in [self.dg_key_input, self.groq_key_input, self.engine_combo, self.btn1_action_combo, self.btn2_action_combo, self.btn3_action_combo]:
            w.setStyleSheet("background: #0f172a; color: white; border: 1px solid #334155; border-radius: 4px; padding: 3px; font-size: 11px;")

        save_btn = QPushButton("💾 Save All Settings")
        save_btn.setStyleSheet("background: #2563eb; color: white; border-radius: 6px; padding: 6px; font-weight: bold;")
        save_btn.clicked.connect(self._save_settings_from_tab)

        layout.addRow("Deepgram Key:", self.dg_key_input)
        layout.addRow("Groq Key:", self.groq_key_input)
        layout.addRow("Engine:", self.engine_combo)
        layout.addRow("Button 1 Action:", self.btn1_action_combo)
        layout.addRow("Button 2 Action:", self.btn2_action_combo)
        layout.addRow("Button 3 Action:", self.btn3_action_combo)
        layout.addRow("Feedback:", self.chime_check)
        layout.addRow(save_btn)

        self.tabs.addTab(tab, "⚙️ Settings")

    def _set_dot_color(self, hex_color: str):
        self.status_dot.setStyleSheet(f"background-color: {hex_color}; border-radius: 5px;")

    def _connect_signals(self):
        self.interim_text_received.connect(self._on_interim_text)
        self.final_text_received.connect(self._on_final_text)
        self.status_changed.connect(self._on_status_changed)
        self.language_switched.connect(self.update_language_display)

    def _on_btn1_clicked(self):
        """Button 1 Click Handler (Customizable)."""
        action = self.btn1_action_combo.currentText() if hasattr(self, 'btn1_action_combo') else "🎙️ Toggle Mic"
        if "Toggle" in action or "Mic" in action:
            if self.on_toggle_callback:
                self.on_toggle_callback()
        elif "Lang" in action:
            self._on_btn2_clicked()

    def _on_btn2_clicked(self):
        """Button 2 Click Handler (Default: Switch Language)."""
        new_lang = "en" if self.current_lang == "ar" else "ar"
        self.current_lang = new_lang
        self.update_language_display(new_lang)
        if self.on_lang_switch_callback:
            self.on_lang_switch_callback(new_lang)

    def _on_btn3_clicked(self):
        """Button 3 Click Handler (Default: Toggle Drawer)."""
        self.drawer_expanded = not self.drawer_expanded
        self.drawer_widget.setVisible(self.drawer_expanded)
        self.adjustSize()

    def _trigger_ai(self, mode: str):
        text = self.ai_input.toPlainText().strip()
        if not text and self.history_mgr and self.history_mgr.entries:
            text = self.history_mgr.entries[0]["text"]
            self.ai_input.setText(text)

        if text and self.on_ai_polish_callback:
            self.on_ai_polish_callback(text, mode, lambda res: self.ai_input.setText(res))

    def _add_custom_snippet(self):
        trig = self.snip_trigger.text().strip()
        exp = self.snip_expansion.text().strip()
        if trig and exp and self.snippet_mgr:
            self.snippet_mgr.add_snippet(trig, exp)
            self.snip_trigger.clear()
            self.snip_expansion.clear()
            self._refresh_snippets_ui()

    def _refresh_snippets_ui(self):
        if not self.snippet_mgr:
            return
        self.snippet_list.clear()
        for trig, exp in self.snippet_mgr.get_all_snippets().items():
            self.snippet_list.addItem(f"'{trig}' -> '{exp}'")

    def _refresh_history_ui(self):
        if not self.history_mgr:
            return
        self.history_list.clear()
        for entry in self.history_mgr.get_history(limit=25):
            self.history_list.addItem(f"[{entry['timestamp']}] ({entry['lang'].upper()}) {entry['text']}")

    def _copy_selected_history(self):
        item = self.history_list.currentItem()
        if item:
            import win32clipboard
            import win32con
            text = item.text().split(") ", 1)[-1]
            win32clipboard.OpenClipboard()
            win32clipboard.EmptyClipboard()
            win32clipboard.SetClipboardText(text, win32con.CF_UNICODETEXT)
            win32clipboard.CloseClipboard()

    def _clear_history(self):
        if self.history_mgr:
            self.history_mgr.clear_history()
            self._refresh_history_ui()

    def _save_settings_from_tab(self):
        new_settings = {
            "deepgram_api_key": self.dg_key_input.text().strip(),
            "groq_api_key": self.groq_key_input.text().strip(),
            "sound_effects": self.chime_check.isChecked(),
            "auto_sleep_sec": self.vad_timeout_slider.value()
        }
        if self.on_settings_save_callback:
            self.on_settings_save_callback(new_settings)
        QMessageBox.information(self, "Settings Saved", "Configuration updated successfully!")

    def _on_interim_text(self, text: str):
        if text:
            self.preview_label.setText(f"<i>... {text}</i>")
            self.preview_label.setStyleSheet("""
                QLabel {
                    color: #38bdf8;
                    font-size: 12px;
                    font-family: 'Segoe UI', Arial, sans-serif;
                    padding: 4px 10px;
                    background: rgba(14, 165, 233, 0.18);
                    border: 1px solid rgba(56, 189, 248, 0.35);
                    border-radius: 10px;
                }
            """)

    def _on_final_text(self, text: str):
        if text:
            self.preview_label.setText(text)
            self.preview_label.setStyleSheet("""
                QLabel {
                    color: #4ade80;
                    font-size: 12px;
                    font-family: 'Segoe UI', Arial, sans-serif;
                    padding: 4px 10px;
                    background: rgba(34, 197, 94, 0.18);
                    border: 1px solid rgba(74, 222, 128, 0.35);
                    border-radius: 10px;
                }
            """)
            self._refresh_history_ui()

    def _on_status_changed(self, text: str, color_hex: str):
        self._set_dot_color(color_hex)

    def update_language_display(self, lang: str):
        self.current_lang = lang
        self.btn2.setText("العربية (AR)" if lang == "ar" else "English (EN)")
        self.btn2.setStyleSheet(self._get_lang_btn_style(lang))

    def set_recording_state(self, is_recording: bool):
        self.is_recording = is_recording
        self.btn1.setText("⏹️ Stop" if is_recording else "🎙️ Start")
        self.btn1.setStyleSheet(self._get_mic_btn_style(is_recording))
        self._on_status_changed("Listening" if is_recording else "Idle", "#ef4444" if is_recording else "#6b7280")

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self._drag_pos = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.LeftButton and not self._drag_pos.isNull():
            self.move(event.globalPosition().toPoint() - self._drag_pos)
            event.accept()
