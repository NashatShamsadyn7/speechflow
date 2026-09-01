"""
Production Standalone Binary Builder & NSIS Packaging Script
Compiles the application into a standalone Windows Executable (.exe) for 250K+ international users.
Zero Python dependency required on end-user machines.
"""

import os
import sys
import subprocess
import shutil

# Ensure UTF-8 output for Windows console
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding='utf-8')

APP_NAME = "SpeechFlow"
MAIN_SCRIPT = "main.py"
ICON_PATH = "ui/icon.ico" if os.path.exists("ui/icon.ico") else None

def build_executable():
    print("=" * 70)
    print(f"📦 COMPILING {APP_NAME} STANDALONE EXECUTABLE FOR 250K DEPLOYMENT")
    print("=" * 70)

    cmd = [
        sys.executable, "-m", "PyInstaller",
        "--name", APP_NAME,
        "--noconsole",
        "--onedir",
        "--clean",
        "--noconfirm",
        "--add-data", "config.json;.",
        "--add-data", "snippets.json;.",
        "--collect-all", "PySide6",
        "--collect-all", "sounddevice",
        "--collect-all", "faster_whisper",
        MAIN_SCRIPT
    ]

    print(f"Running command: {' '.join(cmd)}")
    result = subprocess.run(cmd)

    if result.returncode == 0:
        print("\n" + "=" * 70)
        print(f"🎉 BUILD SUCCESSFUL! Distributable folder: dist/{APP_NAME}/")
        print("=" * 70)
    else:
        print("\n❌ Build failed with exit code:", result.returncode)

def generate_nsis_script():
    """Generates standard NSIS installer script for Windows 10/11."""
    nsis_content = f"""
; NSIS Modern Installer Script for {APP_NAME}
!include "MUI2.nsh"

Name "{APP_NAME}"
OutFile "dist\\{APP_NAME}-Setup.exe"
InstallDir "$PROGRAMFILES64\\{APP_NAME}"
RequestExecutionLevel admin

!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Arabic"

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "dist\\{APP_NAME}\\*.*"
  
  CreateShortcut "$DESKTOP\\{APP_NAME}.lnk" "$INSTDIR\\{APP_NAME}.exe"
  CreateDirectory "$SMPROGRAMS\\{APP_NAME}"
  CreateShortcut "$SMPROGRAMS\\{APP_NAME}\\{APP_NAME}.lnk" "$INSTDIR\\{APP_NAME}.exe"
  CreateShortcut "$SMPROGRAMS\\{APP_NAME}\\Uninstall.lnk" "$INSTDIR\\Uninstall.exe"
  
  WriteUninstaller "$INSTDIR\\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\\{APP_NAME}.lnk"
  RMDir /r "$SMPROGRAMS\\{APP_NAME}"
  RMDir /r "$INSTDIR"
SectionEnd
"""
    with open("installer.nsi", "w", encoding="utf-8") as f:
        f.write(nsis_content.strip())
    print("  -> Generated 'installer.nsi' for NSIS compiler.")

if __name__ == "__main__":
    generate_nsis_script()
    print("Ready to compile. Run with '--build' to trigger PyInstaller compilation.")
    if "--build" in sys.argv:
        build_executable()
