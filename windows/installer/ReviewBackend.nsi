Unicode True
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
Name "Rovunet"
!ifndef REVIEW_VERSION
!define REVIEW_VERSION "0.1.1-preview.1"
!endif
OutFile "${OUTPUT}"
RequestExecutionLevel user
SilentInstall silent
SetCompressor /SOLID lzma
Var RegisterOnly
Var DesktopChoice
Var AllUsers
Var TestInstall
Var AppKey
Var AppTitle
Function .onInit
  ${IfNot} ${RunningX64}
    SetErrorLevel 30
    Quit
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    SetErrorLevel 30
    Quit
  ${EndIf}
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
  IntCmp $0 19045 supported unsupported supported
  unsupported:
    SetErrorLevel 30
    Quit
  supported:
  SetRegView 64
  ${GetParameters} $0
  ${GetOptions} $0 "/REGISTER=" $RegisterOnly
  ${GetOptions} $0 "/DESKTOP=" $DesktopChoice
  ${GetOptions} $0 "/ALLUSERS=" $AllUsers
  ${GetOptions} $0 "/TEST=" $TestInstall
  StrCpy $AppKey "Rovunet"
  StrCpy $AppTitle "Rovunet"
  ${If} $TestInstall == "1"
    StrCpy $AppKey "RovunetInstallerTest"
    StrCpy $AppTitle "Rovunet Installer Test"
  ${EndIf}
  ${If} $RegisterOnly != "1"
    IfFileExists "$INSTDIR\*.*" 0 fresh
    SetErrorLevel 31
    Quit
  ${EndIf}
  fresh:
FunctionEnd
Section
  ${If} $RegisterOnly != "1"
    SetOutPath "$INSTDIR"
    ClearErrors
    File /r "${STAGING}\*"
    IfErrors failed
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    IfErrors failed
    SetErrorLevel 0
    Goto done
  ${EndIf}
  FileOpen $0 "$INSTDIR\review-install.marker" r
  IfErrors failed
  FileRead $0 $1
  FileClose $0
  StrCmp $1 "Rovunet-Windows" 0 failed
  ${If} $AllUsers == "1"
    SetShellVarContext all
  ${Else}
    SetShellVarContext current
  ${EndIf}
  ClearErrors
  CreateDirectory "$SMPROGRAMS\$AppTitle"
  CreateShortcut "$SMPROGRAMS\$AppTitle\Rovunet.lnk" "$INSTDIR\Rovunet.exe"
  CreateShortcut "$SMPROGRAMS\$AppTitle\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  ${If} $DesktopChoice == "1"
    CreateShortcut "$DESKTOP\$AppTitle.lnk" "$INSTDIR\Rovunet.exe"
  ${EndIf}
  ${If} $AllUsers == "1"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "DisplayName" "$AppTitle"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "DisplayVersion" "${REVIEW_VERSION}"
  ${Else}
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "DisplayName" "$AppTitle"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "DisplayVersion" "${REVIEW_VERSION}"
  ${EndIf}
  IfErrors failed
  SetErrorLevel 0
  Goto done
  failed:
  SetErrorLevel 32
  done:
SectionEnd
Function un.onInit
  SetRegView 64
  FileOpen $0 "$INSTDIR\review-install.marker" r
  IfErrors refused
  FileRead $0 $1
  FileClose $0
  StrCmp $1 "Rovunet-Windows" 0 refused
  StrCpy $AppKey "Rovunet"
  StrCpy $AppTitle "Rovunet"
  FileOpen $0 "$INSTDIR\.install-id" r
  IfErrors scope
  FileRead $0 $1
  FileClose $0
  ${If} $1 == "1"
    StrCpy $AppKey "RovunetInstallerTest"
    StrCpy $AppTitle "Rovunet Installer Test"
  ${EndIf}
  scope:
  StrCpy $AllUsers "0"
  FileOpen $0 "$INSTDIR\.install-scope" r
  IfErrors scope_ready
  FileRead $0 $AllUsers
  FileClose $0
  scope_ready:
  ${If} $AllUsers == "1"
    System::Call 'shell32::IsUserAnAdmin()i.r0'
    ${If} $0 == 0
      ClearErrors
      ExecShell "runas" "$EXEPATH" "/S _?=$INSTDIR"
      IfErrors refused
      SetErrorLevel 0
      Quit
    ${EndIf}
    SetShellVarContext all
  ${Else}
    SetShellVarContext current
  ${EndIf}
  Return
  refused:
  SetErrorLevel 33
  Quit
FunctionEnd
Section "Uninstall"
  ; Do not run a network application elevated or delete an active installation.
  !include "${PREFLIGHT_MANIFEST}"
  IfFileExists "$LOCALAPPDATA\Rovunet\proxy-recovery.dpapi" 0 recovery_done
    ; This read-only command never starts an Agent or changes the current proxy.
    ; A journal belonging to another installation must remain untouched.
    ExecWait '$\"$INSTDIR\Rovunet.exe$\" --uninstall-recovery-scope' $0
    ${If} $0 == 0
      Goto recovery_done
    ${EndIf}
    ${If} $0 != 10
      Goto refused
    ${EndIf}
    System::Call 'shell32::IsUserAnAdmin()i.r0'
    ${If} $0 != 0
      Goto refused
    ${EndIf}
    ExecWait '$\"$INSTDIR\Rovunet.exe$\" --recover-silent' $0
    ${If} $0 != 0
      Goto refused
    ${EndIf}
  recovery_done:
  ClearErrors
  Delete "$INSTDIR\Rovunet.exe"
  IfErrors refused
  !include "${DELETE_MANIFEST}"
  Delete "$INSTDIR\.install-scope"
  Delete "$INSTDIR\.install-id"
  Delete "$INSTDIR\review-install.marker"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\$AppTitle\Rovunet.lnk"
  Delete "$SMPROGRAMS\$AppTitle\Uninstall.lnk"
  RMDir "$SMPROGRAMS\$AppTitle"
  Delete "$DESKTOP\$AppTitle.lnk"
  ${If} $AllUsers == "1"
    ReadRegStr $0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "InstallLocation"
    ${If} $0 == $INSTDIR
      DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey"
    ${EndIf}
  ${Else}
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey" "InstallLocation"
    ${If} $0 == $INSTDIR
      DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppKey"
    ${EndIf}
  ${EndIf}
  SetErrorLevel 0
  Goto done
  refused:
  IfSilent +2
    MessageBox MB_OK "Close Rovunet and use its proxy recovery before uninstalling."
  SetErrorLevel 33
  done:
SectionEnd

