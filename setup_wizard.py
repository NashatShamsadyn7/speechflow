"""
SpeechFlow Windows 11 Setup Wizard (Installer GUI)
A modern, fluent installer wizard for installing and registering SpeechFlow on Windows.
"""

import sys
import os
import shutil
import subprocess
import winreg
from PySide6.QtCore import Qt, QThread, Signal
from PySide6.QtGui import QFont, QColor, QIcon, QPixmap, QPainter
from PySide6.QtWidgets import (
    QApplication, QWizard, QWizardPage, QVBoxLayout, QHBoxLayout,
    QLabel, QLineEdit, QPushButton, QFileDialog, QCheckBox,
    QProgressBar, QMessageBox
)

APP_NAME = "SpeechFlow"
APP_VERSION = "2.0.0"
PUBLISHER = "Nashat Shamsadyn Ahmed"
DEFAULT_INSTALL_DIR = os.path.join(os.environ.get("LOCALAPPDATA", "C:\\"), APP_NAME)


class InstallWorker(QThread):
    progress = Signal(int, str)
    finished = Signal(bool, str)

    def __init__(self, source_dir: str, target_dir: str, create_desktop: bool, create_startmenu: bool, start_on_boot: bool):
        super().__init__()
        self.source_dir = source_dir
        self.target_dir = target_dir
        self.create_desktop = create_desktop
        self.create_startmenu = create_startmenu
        self.start_on_boot = start_on_boot

    def run(self):
        try:
            self.progress.emit(10, "Creating destination directory...")
            os.makedirs(self.target_dir, exist_ok=True)

            # Copy files
            self.progress.emit(30, "Copying application binaries and libraries...")
            if os.path.exists(self.source_dir):
                for item in os.listdir(self.source_dir):
                    s = os.path.join(self.source_dir, item)
                    d = os.path.join(self.target_dir, item)
                    if os.path.isdir(s):
                        if os.path.exists(d):
                            shutil.rmtree(d)
                        shutil.copytree(s, d)
                    else:
                        shutil.copy2(s, d)

            exe_path = os.path.join(self.target_dir, f"{APP_NAME}.exe")

            # Shortcuts
            self.progress.emit(70, "Creating Windows shortcuts...")
            if self.create_desktop:
                desktop = os.path.join(os.environ["USERPROFILE"], "Desktop")
                self._create_shortcut(exe_path, os.path.join(desktop, f"{APP_NAME}.lnk"))

            if self.create_startmenu:
                start_menu = os.path.join(os.environ["APPDATA"], "Microsoft", "Windows", "Start Menu", "Programs", APP_NAME)
                os.makedirs(start_menu, exist_ok=True)
                self._create_shortcut(exe_path, os.path.join(start_menu, f"{APP_NAME}.lnk"))

            # Register in Windows Installed Apps Registry
            self.progress.emit(90, "Registering in Windows Settings...")
            self._register_uninstall(exe_path)

            self.progress.emit(100, "Installation completed successfully!")
            self.finished.emit(True, "Installation complete.")
        except Exception as e:
            self.finished.emit(False, str(e))

    def _create_shortcut(self, target_exe: str, shortcut_path: str):
        ps_cmd = f'$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut("{shortcut_path}"); $s.TargetPath = "{target_exe}"; $s.WorkingDirectory = "{os.path.dirname(target_exe)}"; $s.Save()'
        subprocess.run(["powershell", "-NoProfile", "-Command", ps_cmd], capture_output=True)

    def _register_uninstall(self, exe_path: str):
        try:
            key_path = rf"Software\Microsoft\Windows\CurrentVersion\Uninstall\{APP_NAME}"
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, key_path) as key:
                winreg.SetValueEx(key, "DisplayName", 0, winreg.REG_SZ, f"{APP_NAME} (Win+H Edition)")
                winreg.SetValueEx(key, "DisplayVersion", 0, winreg.REG_SZ, APP_VERSION)
                winreg.SetValueEx(key, "Publisher", 0, winreg.REG_SZ, PUBLISHER)
                winreg.SetValueEx(key, "InstallLocation", 0, winreg.REG_SZ, self.target_dir)
                winreg.SetValueEx(key, "DisplayIcon", 0, winreg.REG_SZ, exe_path)
                winreg.SetValueEx(key, "UninstallString", 0, winreg.REG_SZ, f'powershell -Command "Remove-Item -Recurse -Force \'{self.target_dir}\'"')
        except Exception:
            pass


class WelcomePage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Welcome to SpeechFlow Setup")
        self.setSubTitle("Ultra-Fast Live Speech-to-Text & Typing Interpreter for Windows 10/11")

        layout = QVBoxLayout(self)
        layout.setSpacing(14)

        info = QLabel(
            "This wizard will install <b>SpeechFlow v2.0 (Win+H Edition)</b> on your computer.<br><br>"
            "✨ <b>Features included:</b><br>"
            "• Microsoft Win+H Acrylic Floating Bar with 3 customizable buttons<br>"
            "• Arabic & English Live Typing with Voice Punctuation<br>"
            "• Sub-4ms local keystroke injection with zero layout corruption<br>"
            "• AI Text Polisher, Voice Snippets, and Local Offline Mode<br><br>"
            "Click <b>Next</b> to continue."
        )
        info.setWordWrap(True)
        info.setStyleSheet("font-size: 13px; line-height: 1.5; color: #1e293b;")
        layout.addWidget(info)


class DirectoryPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Select Destination Location")
        self.setSubTitle("Where should SpeechFlow be installed?")

        layout = QVBoxLayout(self)
        layout.setSpacing(12)

        lbl = QLabel("Setup will install SpeechFlow into the following folder:")
        self.dir_input = QLineEdit(DEFAULT_INSTALL_DIR)
        self.dir_input.setStyleSheet("padding: 6px; font-size: 12px;")

        browse_btn = QPushButton("Browse...")
        browse_btn.clicked.connect(self._browse)

        h = QHBoxLayout()
        h.addWidget(self.dir_input)
        h.addWidget(browse_btn)

        layout.addWidget(lbl)
        layout.addLayout(h)

        self.registerField("install_dir*", self.dir_input)

    def _browse(self):
        d = QFileDialog.getExistingDirectory(self, "Select Install Directory", self.dir_input.text())
        if d:
            self.dir_input.setText(os.path.join(d, APP_NAME))


class OptionsPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Select Additional Tasks")
        self.setSubTitle("Which additional shortcuts should be created?")

        layout = QVBoxLayout(self)
        layout.setSpacing(10)

        self.cb_desktop = QCheckBox("Create a &Desktop shortcut")
        self.cb_desktop.setChecked(True)

        self.cb_startmenu = QCheckBox("Create a &Start Menu shortcut")
        self.cb_startmenu.setChecked(True)

        self.cb_startup = QCheckBox("Start SpeechFlow when Windows starts")
        self.cb_startup.setChecked(False)

        layout.addWidget(self.cb_desktop)
        layout.addWidget(self.cb_startmenu)
        layout.addWidget(self.cb_startup)


class ProgressPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Installing")
        self.setSubTitle("Please wait while SpeechFlow is being installed on your computer.")

        layout = QVBoxLayout(self)
        layout.setSpacing(12)

        self.status_lbl = QLabel("Preparing installation...")
        self.pbar = QProgressBar()
        self.pbar.setRange(0, 100)
        self.pbar.setValue(0)
        self.pbar.setStyleSheet("""
            QProgressBar {
                border: 1px solid #cbd5e1;
                border-radius: 6px;
                text-align: center;
                height: 22px;
            }
            QProgressBar::chunk {
                background-color: #2563eb;
                border-radius: 5px;
            }
        """)

        layout.addWidget(self.status_lbl)
        layout.addWidget(self.pbar)
        self.is_complete = False

    def initializePage(self):
        wizard = self.wizard()
        install_dir = wizard.dir_page.dir_input.text()
        source_dir = os.path.join(os.path.dirname(__file__), "dist", APP_NAME)

        self.worker = InstallWorker(
            source_dir=source_dir,
            target_dir=install_dir,
            create_desktop=wizard.opts_page.cb_desktop.isChecked(),
            create_startmenu=wizard.opts_page.cb_startmenu.isChecked(),
            start_on_boot=wizard.opts_page.cb_startup.isChecked()
        )

        self.worker.progress.connect(self._on_progress)
        self.worker.finished.connect(self._on_finished)
        self.worker.start()

    def _on_progress(self, val: int, msg: str):
        self.pbar.setValue(val)
        self.status_lbl.setText(msg)

    def _on_finished(self, success: bool, msg: str):
        self.is_complete = success
        self.completeChanged.emit()
        if not success:
            QMessageBox.critical(self, "Installation Error", f"Error during install: {msg}")

    def isComplete(self):
        return self.is_complete


class FinishedPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Completing SpeechFlow Setup")
        self.setSubTitle("Setup has finished installing SpeechFlow on your computer.")

        layout = QVBoxLayout(self)
        layout.setSpacing(12)

        msg = QLabel(
            "SpeechFlow is now ready to use.<br><br>"
            "<b>Quick Reminder:</b><br>"
            "• Press <b>Ctrl + Shift + Space</b> anywhere in Windows to start dictating.<br>"
            "• Press <b>Ctrl + Shift + L</b> to switch between Arabic and English."
        )
        msg.setWordWrap(True)
        msg.setStyleSheet("font-size: 13px; line-height: 1.5; color: #1e293b;")

        self.launch_cb = QCheckBox("Launch SpeechFlow now")
        self.launch_cb.setChecked(True)

        layout.addWidget(msg)
        layout.addWidget(self.launch_cb)


class SpeechFlowWizard(QWizard):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(f"{APP_NAME} Setup Wizard")
        self.setWizardStyle(QWizard.ModernStyle)
        self.resize(540, 380)

        self.welcome_page = WelcomePage()
        self.dir_page = DirectoryPage()
        self.opts_page = OptionsPage()
        self.prog_page = ProgressPage()
        self.finish_page = FinishedPage()

        self.addPage(self.welcome_page)
        self.addPage(self.dir_page)
        self.addPage(self.opts_page)
        self.addPage(self.prog_page)
        self.addPage(self.finish_page)

    def accept(self):
        if self.finish_page.launch_cb.isChecked():
            exe_path = os.path.join(self.dir_page.dir_input.text(), f"{APP_NAME}.exe")
            if os.path.exists(exe_path):
                subprocess.Popen([exe_path], cwd=os.path.dirname(exe_path))
        super().accept()


def main():
    app = QApplication(sys.argv)
    wizard = SpeechFlowWizard()
    wizard.show()
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
