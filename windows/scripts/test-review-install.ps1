param([Parameter(Mandatory=$true)][string]$Backend,[Parameter(Mandatory=$true)][string]$EvidenceDirectory,[Parameter(Mandatory=$true)][string]$OriginalExecutable)
$ErrorActionPreference='Stop'
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../artifacts/evidence'))+[IO.Path]::DirectorySeparatorChar
if(!$evidence.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $evidence)){throw 'Fresh workspace evidence directory required'}
New-Item -ItemType Directory -Path $evidence|Out-Null
$target=Join-Path $evidence 'normal uninstall with spaces'
$registry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RovunetInstallerTest'
if(Test-Path $registry){throw 'Existing installer test registration must be left intact'}
$checks=[ordered]@{}
function ProxyDigest {
 $key=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
 $text=@('ProxyEnable','ProxyServer','ProxyOverride','AutoConfigURL')|ForEach-Object {"$_=$($key.$_)"}
 [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($text -join "`n"))))
}
function Execute([string]$file,[string]$arguments){$p=Start-Process -FilePath $file -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden;return $p.ExitCode}
$original=[IO.Path]::GetFullPath($OriginalExecutable)
$originalHash=(Get-FileHash -LiteralPath $original).Hash
$proxyBefore=ProxyDigest
$desktop=Join-Path ([Environment]::GetFolderPath('Desktop')) 'Rovunet Installer Test.lnk'
$menu=Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\Rovunet Installer Test'
try {
  $checks.payload_extracted=(Execute $Backend "/S /D=$target") -eq 0
 [IO.File]::WriteAllText((Join-Path $target '.install-id'),'1')
 [IO.File]::WriteAllText((Join-Path $target '.install-scope'),'0')
 $checks.registration_completed=(Execute $Backend "/S /REGISTER=1 /TEST=1 /ALLUSERS=0 /DESKTOP=1 /D=$target") -eq 0
 $checks.identity_correct=[IO.File]::ReadAllText((Join-Path $target 'review-install.marker')) -eq 'Rovunet-Windows'
 $checks.registry_and_shortcuts=(Test-Path $registry) -and (Test-Path -LiteralPath $desktop) -and (Test-Path -LiteralPath (Join-Path $menu 'Rovunet.lnk'))
 $checks.new_native_pin=(Get-FileHash -LiteralPath (Join-Path $target 'engines\rovunet-transports.exe')).Hash -eq 'c8c6bcff1e414dcd9f7a3f2723028627653ca5865b6519e8a4a2a94ab36ce499'
 $checks.overwrite_refused=(Execute $Backend "/S /D=$target") -eq 31
 $marker=Join-Path $target 'review-install.marker';$uninstall=Join-Path $target 'Uninstall.exe'
 [IO.File]::WriteAllText($marker,'wrong-fixture-marker')
 $checks.wrong_marker_refused=(Execute $uninstall "/S _?=$target") -eq 33
 $checks.dependencies_preserved_on_refusal=Test-Path -LiteralPath (Join-Path $target 'Rovunet.Agent.dll')
 [IO.File]::WriteAllText($marker,'Rovunet-Windows')
 $lock=[IO.File]::Open((Join-Path $target 'Rovunet.exe'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
 try { $checks.locked_executable_refused=(Execute $uninstall "/S _?=$target") -eq 33; $checks.dependencies_preserved_when_locked=Test-Path -LiteralPath (Join-Path $target 'Rovunet.Agent.dll') } finally {$lock.Dispose()}
 [IO.File]::WriteAllText((Join-Path $target 'user-note.txt'),'keep-fixture')
 $checks.normal_uninstall_started=(Execute $uninstall '/S') -eq 0
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while(((Test-Path $registry) -or (Test-Path -LiteralPath $uninstall)) -and $watch.Elapsed.TotalSeconds -lt 25){Start-Sleep -Milliseconds 200}
 $checks.normal_uninstall_completed=!(Test-Path -LiteralPath (Join-Path $target 'Rovunet.dll')) -and !(Test-Path -LiteralPath $uninstall)
 $checks.registration_removed=!(Test-Path $registry)
 $checks.shortcuts_removed=!(Test-Path -LiteralPath $desktop) -and !(Test-Path -LiteralPath $menu)
 $checks.user_file_preserved=[IO.File]::ReadAllText((Join-Path $target 'user-note.txt')) -eq 'keep-fixture'
 $checks.original_executable_unchanged=(Get-FileHash -LiteralPath $original).Hash -eq $originalHash
 $checks.proxy_unchanged=(ProxyDigest) -eq $proxyBefore
} catch {$checks["failure_$($_.Exception.GetType().Name)"]=$false}
$passed=@($checks.Values|Where-Object{!$_}).Count -eq 0
@{scope='Actual Windows trusted extraction, test registration, guards and normal self-copying uninstall; no normal app launch';checks=$checks;passed=$passed}|ConvertTo-Json -Depth 4|Set-Content (Join-Path $evidence 'report.json') -Encoding utf8
if(!$passed){throw 'Installer checks failed; inspect report.json'}

