"""
SpeechFlow v2.0 Setup Wizard (Windows 11 Fluent Style)
Self-contained installer with zero external directory dependencies.
Extracts SpeechFlow.exe, creates Desktop & Start Menu shortcuts, registers uninstaller.
Author: Nashat Shamsadyn Ahmed
"""

import sys
import os
import shutil
import subprocess
import winreg
from PySide6.QtCore import Qt, QThread, Signal
from PySide6.QtGui import QColor, QFont, QIcon
from PySide6.QtWidgets import (
    QApplication, QWizard, QWizardPage, QVBoxLayout, QHBoxLayout,
    QLabel, QLineEdit, QPushButton, QProgressBar, QCheckBox,
    QFileDialog, QMessageBox, QFrame
)

APP_NAME = "SpeechFlow"
APP_VERSION = "2.0.0"
APP_PUBLISHER = "Nashat Shamsadyn Ahmed"
APP_DESC = "Ultra-Fast Live STT & Voice Typing Interpreter for Windows"

def get_bundle_dir():
    """Get extraction directory whether running from PyInstaller onefile or source"""
    if getattr(sys, 'frozen', False):
        return getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
    return os.path.dirname(os.path.abspath(__file__))

def get_default_install_dir():
    local_app_data = os.environ.get("LOCALAPPDATA", os.path.expanduser("~\\AppData\\Local"))
    return os.path.join(local_app_data, "Programs", APP_NAME)


class InstallWorker(QThread):
    progress_updated = Signal(int, str)
    finished_successfully = Signal()
    error_occurred = Signal(str)

    def __init__(self, target_dir: str, create_desktop_icon: bool, create_startup_entry: bool):
        super().__init__()
        self.target_dir = target_dir
        self.create_desktop_icon = create_desktop_icon
        self.create_startup_entry = create_startup_entry

    def run(self):
        try:
            bundle_dir = get_bundle_dir()
            self.progress_updated.emit(15, "Preparing installation directories...")
            os.makedirs(self.target_dir, exist_ok=True)

            # 1. Locate SpeechFlow.exe source
            src_exe = os.path.join(bundle_dir, "SpeechFlow.exe")
            if not os.path.exists(src_exe):
                # Fallback check
                src_exe = os.path.join(os.path.dirname(os.path.abspath(__file__)), "SpeechFlow.exe")
            if not os.path.exists(src_exe):
                # Fallback to dist
                src_exe = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dist", "SpeechFlow.exe")

            self.progress_updated.emit(35, "Installing SpeechFlow binaries...")
            dest_exe = os.path.join(self.target_dir, "SpeechFlow.exe")
            
            if os.path.exists(src_exe):
                shutil.copy2(src_exe, dest_exe)
            else:
                # If running directly from setup binary that contains itself
                shutil.copy2(sys.executable, dest_exe)

            # 2. Copy configs and assets if present
            self.progress_updated.emit(60, "Writing default configurations...")
            for fname in ["config.json", "snippets.json"]:
                src_f = os.path.join(bundle_dir, fname)
                if os.path.exists(src_f):
                    shutil.copy2(src_f, os.path.join(self.target_dir, fname))

            # 3. Create Windows Shortcuts
            self.progress_updated.emit(75, "Creating desktop & start menu shortcuts...")
            self._create_shortcuts(dest_exe)

            # 4. Register in Windows Registry
            self.progress_updated.emit(90, "Registering application in Windows...")
            self._register_uninstall(dest_exe)

            if self.create_startup_entry:
                self._add_startup(dest_exe)

            self.progress_updated.emit(100, "Installation complete!")
            self.finished_successfully.emit()

        except Exception as e:
            self.error_occurred.emit(str(e))

    def _create_shortcuts(self, target_exe: str):
        try:
            import win32com.client
            shell = win32com.client.Dispatch("WScript.Shell")

            # Start Menu
            appdata = os.environ.get("APPDATA", os.path.expanduser("~\\AppData\\Roaming"))
            start_menu = os.path.join(appdata, "Microsoft", "Windows", "Start Menu", "Programs")
            shortcut = shell.CreateShortCut(os.path.join(start_menu, f"{APP_NAME}.lnk"))
            shortcut.TargetPath = target_exe
            shortcut.WorkingDirectory = self.target_dir
            shortcut.Description = APP_DESC
            shortcut.save()

            # Desktop Shortcut
            if self.create_desktop_icon:
                desktop = shell.SpecialFolders("Desktop")
                d_shortcut = shell.CreateShortCut(os.path.join(desktop, f"{APP_NAME}.lnk"))
                d_shortcut.TargetPath = target_exe
                d_shortcut.WorkingDirectory = self.target_dir
                d_shortcut.Description = APP_DESC
                d_shortcut.save()
        except Exception:
            pass

    def _add_startup(self, target_exe: str):
        try:
            key = winreg.OpenKey(
                winreg.HKEY_CURRENT_USER,
                r"Software\Microsoft\Windows\CurrentVersion\Run",
                0, winreg.KEY_SET_VALUE
            )
            winreg.SetValueEx(key, APP_NAME, 0, winreg.REG_SZ, f'"{target_exe}"')
            winreg.CloseKey(key)
        except Exception:
            pass

    def _register_uninstall(self, target_exe: str):
        try:
            uninstall_path = rf"Software\Microsoft\Windows\CurrentVersion\Uninstall\{APP_NAME}"
            key = winreg.CreateKey(winreg.HKEY_CURRENT_USER, uninstall_path)
            winreg.SetValueEx(key, "DisplayName", 0, winreg.REG_SZ, f"{APP_NAME} v{APP_VERSION}")
            winreg.SetValueEx(key, "DisplayVersion", 0, winreg.REG_SZ, APP_VERSION)
            winreg.SetValueEx(key, "Publisher", 0, winreg.REG_SZ, APP_PUBLISHER)
            winreg.SetValueEx(key, "DisplayIcon", 0, winreg.REG_SZ, target_exe)
            winreg.SetValueEx(key, "InstallLocation", 0, winreg.REG_SZ, self.target_dir)
            winreg.SetValueEx(key, "UninstallString", 0, winreg.REG_SZ, f'cmd.exe /c rmdir /s /q "{self.target_dir}"')
            winreg.CloseKey(key)
        except Exception:
            pass


class WelcomePage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle(f"Welcome to {APP_NAME} Setup")
        layout = QVBoxLayout(self)
        layout.setSpacing(14)

        lbl = QLabel(
            f"<h2>Install {APP_NAME} v{APP_VERSION}</h2>"
            f"<p><b>Developed by {APP_PUBLISHER}</b></p>"
            f"<p>This wizard will install the ultra-fast Windows 11 Win+H speech-to-text typing interpreter onto your computer.</p>"
            f"<p>• Sub-4ms local response latency<br>"
            f"• Native Win32 Unicode injection (Zero layout bugs)<br>"
            f"• Arabic, Kurdish & English voice punctuation commands</p>"
            f"<p>Click <b>Next</b> to continue.</p>"
        )
        lbl.setWordWrap(True)
        layout.addWidget(lbl)


class DirectoryPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Select Installation Folder")
        layout = QVBoxLayout(self)
        layout.setSpacing(12)

        lbl = QLabel("Setup will install SpeechFlow into the following folder:")
        layout.addWidget(lbl)

        row = QHBoxLayout()
        self.dir_input = QLineEdit(get_default_install_dir())
        self.browse_btn = QPushButton("Browse...")
        self.browse_btn.clicked.connect(self._browse)
        row.addWidget(self.dir_input)
        row.addWidget(self.browse_btn)
        layout.addLayout(row)

        self.desktop_cb = QCheckBox("Create a Desktop Shortcut")
        self.desktop_cb.setChecked(True)
        layout.addWidget(self.desktop_cb)

        self.startup_cb = QCheckBox("Start SpeechFlow automatically with Windows")
        self.startup_cb.setChecked(False)
        layout.addWidget(self.startup_cb)

    def _browse(self):
        path = QFileDialog.getExistingDirectory(self, "Select Install Directory", self.dir_input.text())
        if path:
            self.dir_input.setText(os.path.join(path, APP_NAME))


class ProgressPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Installing SpeechFlow")
        self.setSubTitle("Please wait while setup copies files...")
        self.is_installed = False

        layout = QVBoxLayout(self)
        layout.setSpacing(16)

        self.status_lbl = QLabel("Starting installation...")
        self.progress_bar = QProgressBar()
        self.progress_bar.setRange(0, 100)
        self.progress_bar.setValue(0)

        layout.addWidget(self.status_lbl)
        layout.addWidget(self.progress_bar)

    def initializePage(self):
        wizard = self.wizard()
        target_dir = wizard.dir_page.dir_input.text()
        create_desktop = wizard.dir_page.desktop_cb.isChecked()
        create_startup = wizard.dir_page.startup_cb.isChecked()

        self.worker = InstallWorker(target_dir, create_desktop, create_startup)
        self.worker.progress_updated.connect(self._update_progress)
        self.worker.finished_successfully.connect(self._on_finish)
        self.worker.error_occurred.connect(self._on_error)
        self.worker.start()

    def _update_progress(self, val: int, msg: str):
        self.progress_bar.setValue(val)
        self.status_lbl.setText(msg)

    def _on_finish(self):
        self.is_installed = True
        self.completeChanged.emit()
        self.wizard().next()

    def _on_error(self, err: str):
        QMessageBox.critical(self, "Installation Error", f"Failed to complete setup:\n{err}")

    def isComplete(self):
        return self.is_installed


class FinishPage(QWizardPage):
    def __init__(self):
        super().__init__()
        self.setTitle("Setup Completed Successfully!")
        layout = QVBoxLayout(self)
        layout.setSpacing(14)

        lbl = QLabel(
            f"<h3>SpeechFlow is ready to use!</h3>"
            f"<p>Press <b>Ctrl + Shift + Space</b> anywhere to open the 1-line Win+H bar and start speaking.</p>"
        )
        lbl.setWordWrap(True)
        layout.addWidget(lbl)

        self.launch_cb = QCheckBox("Launch SpeechFlow now")
        self.launch_cb.setChecked(True)
        layout.addWidget(self.launch_cb)


class SpeechFlowInstallerWizard(QWizard):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(f"{APP_NAME} v{APP_VERSION} Setup Wizard")
        self.resize(540, 380)

        self.welcome_page = WelcomePage()
        self.dir_page = DirectoryPage()
        self.progress_page = ProgressPage()
        self.finish_page = FinishPage()

        self.addPage(self.welcome_page)
        self.addPage(self.dir_page)
        self.addPage(self.progress_page)
        self.addPage(self.finish_page)

        self.setStyleSheet("""
            QWizard { background: #0f172a; color: #f8fafc; }
            QLabel { color: #e2e8f0; font-size: 13px; }
            QLineEdit { background: #1e293b; color: white; border: 1px solid #475569; border-radius: 6px; padding: 6px; }
            QPushButton { background: #2563eb; color: white; border-radius: 6px; padding: 6px 16px; font-weight: bold; }
            QPushButton:hover { background: #3b82f6; }
            QProgressBar { background: #1e293b; border: 1px solid #475569; border-radius: 6px; height: 18px; text-align: center; color: white; }
            QProgressBar::chunk { background: #10b981; border-radius: 5px; }
            QCheckBox { color: #cbd5e1; }
        """)

    def accept(self):
        if self.finish_page.launch_cb.isChecked():
            target_exe = os.path.join(self.dir_page.dir_input.text(), "SpeechFlow.exe")
            if os.path.exists(target_exe):
                subprocess.Popen([target_exe], cwd=self.dir_page.dir_input.text())
        super().accept()


if __name__ == "__main__":
    app = QApplication(sys.argv)
    wizard = SpeechFlowInstallerWizard()
    wizard.show()
    sys.exit(app.exec())
