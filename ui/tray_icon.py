"""
Windows System Tray Controller
Manages background operation, tray icon, and quick context actions.
"""

from PySide6.QtGui import QIcon, QPixmap, QPainter, QColor, QBrush
from PySide6.QtWidgets import QSystemTrayIcon, QMenu
from PySide6.QtCore import Qt

class TrayIconController:
    def __init__(self, app, on_toggle=None, on_switch_lang=None, on_show_hud=None, on_quit=None):
        self.app = app
        self.on_toggle = on_toggle
        self.on_switch_lang = on_switch_lang
        self.on_show_hud = on_show_hud
        self.on_quit = on_quit

        self.tray = QSystemTrayIcon()
        self.tray.setIcon(self._create_icon(is_active=False))
        self.tray.setToolTip("Ultra-Fast STT Live Interpreter (Arabic/English)")

        self._build_menu()
        self.tray.show()

    def _create_icon(self, is_active=False) -> QIcon:
        pixmap = QPixmap(32, 32)
        pixmap.fill(Qt.transparent)
        painter = QPainter(pixmap)
        painter.setRenderHint(QPainter.Antialiasing)

        # Outer ring
        painter.setBrush(QBrush(QColor(16, 185, 129) if is_active else QColor(59, 130, 246)))
        painter.setPen(Qt.NoPen)
        painter.drawRoundedRect(4, 4, 24, 24, 6, 6)

        # Mic symbol circle
        painter.setBrush(QBrush(QColor(255, 255, 255)))
        painter.drawEllipse(12, 10, 8, 10)
        painter.drawRect(15, 18, 2, 4)
        painter.drawRect(12, 22, 8, 2)

        painter.end()
        return QIcon(pixmap)

    def _build_menu(self):
        menu = QMenu()
        menu.setStyleSheet("""
            QMenu {
                background: #1e293b;
                color: #f8fafc;
                border: 1px solid #334155;
                padding: 4px;
            }
            QMenu::item {
                padding: 6px 20px;
                border-radius: 4px;
            }
            QMenu::item:selected {
                background: #3b82f6;
            }
        """)

        toggle_action = menu.addAction("🎙️ Toggle Dictation (Ctrl+Shift+Space)")
        if self.on_toggle:
            toggle_action.triggered.connect(self.on_toggle)

        lang_action = menu.addAction("🌐 Switch Language (AR / EN)")
        if self.on_switch_lang:
            lang_action.triggered.connect(self.on_switch_lang)

        hud_action = menu.addAction("👁️ Show/Hide Floating HUD")
        if self.on_show_hud:
            hud_action.triggered.connect(self.on_show_hud)

        menu.addSeparator()

        quit_action = menu.addAction("❌ Quit")
        if self.on_quit:
            quit_action.triggered.connect(self.on_quit)

        self.tray.setContextMenu(menu)

    def update_state(self, is_recording: bool):
        self.tray.setIcon(self._create_icon(is_active=is_recording))
