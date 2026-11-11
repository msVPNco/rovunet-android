Unicode True
!include "MUI2.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!ifndef STAGING
  !error "Define STAGING and OUTPUT"
!endif
Name "Rovunet 0.1.0-preview.16"
Icon "../src/Rovunet.Desktop/Assets/rovunet.ico"
OutFile "${OUTPUT}"
InstallDir "$LOCALAPPDATA\Programs\Rovunet"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "0.1.0.0"
VIAddVersionKey "ProductName" "Rovunet"
VIAddVersionKey "FileDescription" "Rovunet Windows private preview installer"
VIAddVersionKey "FileVersion" "0.1.0-preview.6"
VIAddVersionKey "LegalCopyright" "2026 Rovunet contributors"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\Rovunet.exe"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "../LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Farsi"
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "Russian"
LangString NeedX64 ${LANG_FARSI} "Windows x64 لازم است."
LangString NeedOS ${LANG_FARSI} "Windows 10 22H2 یا Windows 11 لازم است."
LangString NeedBuild ${LANG_FARSI} "حداقل نسخه: Windows 10 22H2 (19045)."
LangString NeedRecovery ${LANG_FARSI} "ابتدا Rovunet را ببندید و پراکسی را بازیابی کنید."
LangString NeedX64 ${LANG_ENGLISH} "Windows x64 is required."
LangString NeedOS ${LANG_ENGLISH} "Windows 10 22H2 or Windows 11 is required."
LangString NeedBuild ${LANG_ENGLISH} "Minimum supported build: Windows 10 22H2 (19045)."
LangString NeedRecovery ${LANG_ENGLISH} "Close Rovunet and restore the proxy before uninstalling."
LangString NeedX64 ${LANG_SIMPCHINESE} "需要 Windows x64。"
LangString NeedOS ${LANG_SIMPCHINESE} "需要 Windows 10 22H2 或 Windows 11。"
LangString NeedBuild ${LANG_SIMPCHINESE} "最低版本：Windows 10 22H2 (19045)。"
LangString NeedRecovery ${LANG_SIMPCHINESE} "卸载前请关闭 Rovunet 并恢复代理。"
LangString NeedX64 ${LANG_RUSSIAN} "Требуется Windows x64."
LangString NeedOS ${LANG_RUSSIAN} "Требуется Windows 10 22H2 или Windows 11."
LangString NeedBuild ${LANG_RUSSIAN} "Минимальная сборка: Windows 10 22H2 (19045)."
LangString NeedRecovery ${LANG_RUSSIAN} "Перед удалением закройте Rovunet и восстановите прокси."
Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "$(NeedX64)"
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "$(NeedOS)"
    Abort
  ${EndIf}
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
  IntCmp $0 19045 supported unsupported supported
  unsupported:
    MessageBox MB_ICONSTOP "$(NeedBuild)"
    Abort
  supported:
FunctionEnd
Section "Rovunet"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${STAGING}\*"
  CreateDirectory "$SMPROGRAMS\Rovunet"
  CreateShortcut "$SMPROGRAMS\Rovunet\Rovunet.lnk" "$INSTDIR\Rovunet.exe"
  CreateShortcut "$SMPROGRAMS\Rovunet\Restore proxy.lnk" "$INSTDIR\Rovunet.exe" "--recover"
  CreateShortcut "$DESKTOP\Rovunet.lnk" "$INSTDIR\Rovunet.exe"
  WriteRegStr HKCU "Software\Rovunet" "InstallLanguage" "$LANGUAGE"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortcut "$SMPROGRAMS\Rovunet\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet" "DisplayName" "Rovunet Windows Preview"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet" "DisplayVersion" "0.1.0-preview.6"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet" "DisplayIcon" "$INSTDIR\Rovunet.exe"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet" "NoRepair" 1
SectionEnd
Section "Uninstall"
  SetShellVarContext current
  ExecWait '$\"$INSTDIR\Rovunet.exe$\" --recover-silent' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "$(NeedRecovery)"
    Abort
  ${EndIf}
  Delete "$DESKTOP\Rovunet.lnk"
  RMDir /r "$SMPROGRAMS\Rovunet"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Rovunet"
  RMDir /r "$INSTDIR"
  ; Preserve profiles.dpapi in LocalAppData\Rovunet. Never delete user data here.
SectionEnd
