; NSIS Modern Installer Script for SpeechFlow
!include "MUI2.nsh"

Name "SpeechFlow"
OutFile "dist\SpeechFlow-Setup.exe"
InstallDir "$PROGRAMFILES64\SpeechFlow"
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
  File /r "dist\SpeechFlow\*.*"
  
  CreateShortcut "$DESKTOP\SpeechFlow.lnk" "$INSTDIR\SpeechFlow.exe"
  CreateDirectory "$SMPROGRAMS\SpeechFlow"
  CreateShortcut "$SMPROGRAMS\SpeechFlow\SpeechFlow.lnk" "$INSTDIR\SpeechFlow.exe"
  CreateShortcut "$SMPROGRAMS\SpeechFlow\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\SpeechFlow.lnk"
  RMDir /r "$SMPROGRAMS\SpeechFlow"
  RMDir /r "$INSTDIR"
SectionEnd