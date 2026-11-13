param([Parameter(Mandatory=$true)][string]$LabSource,[Parameter(Mandatory=$true)][string]$ReportRoot,[Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm)
$ErrorActionPreference='Stop'
if(!$ConfirmIsolatedVm -or !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Explicit isolated administrator context required'}
$m=Get-CimInstance Win32_ComputerSystem
if(($m.Manufacturer+' '+$m.Model) -notmatch '(?i)virtual machine|vmware|virtualbox|qemu|kvm|parallels'){throw 'Physical host refused; no auto-elevation or VM resume'}
if(Test-Path -LiteralPath $ReportRoot){throw 'Fresh absolute report directory required'}
$ReportRoot=[IO.Path]::GetFullPath($ReportRoot);New-Item -ItemType Directory $ReportRoot|Out-Null
$target=Join-Path $env:ProgramFiles Rovunet-Network-Lab
if(Test-Path $target){throw 'Use an explicitly prepared fresh isolated installation, preserve existing installation/recovery state'}
$events=@()
function Record($stage,$details){$script:events+=@{stage=$stage;utc=[DateTime]::UtcNow.ToString('O');details=$details};$script:events|ConvertTo-Json -Depth 12|Set-Content (Join-Path $ReportRoot events.json)}
& (Join-Path $LabSource install-service-lab.ps1) -LabSource $LabSource -ConfirmIsolatedVm
$exe=Join-Path $target Rovunet.NetworkLab.exe;$remove=Join-Path $LabSource remove-service-lab.ps1
$hash=(Get-FileHash (Join-Path $target Rovunet.NetworkBroker.dll)).Hash
Record installed @{service=(Get-Service RovunetNetworkLab).Status.ToString();brokerHash=$hash}
# No data-plane session is started in this lifecycle matrix.
$info=[Diagnostics.ProcessStartInfo]::new($exe,'--test-upstream 10.0.2.2 18883 10808 --accept-isolated-lab');$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$helper=[Diagnostics.Process]::Start($info)
try{
 Start-Sleep -Milliseconds 500
 if($helper.HasExited){throw 'Synthetic upstream helper not running; this is not an accepted precheck test'}
 $refused=$false;try{& $remove -ConfirmIsolatedVm -RemoveFiles}catch{$refused=$true;Record precheck_error $_.Exception.Message}
 Record active_helper @{refused=$refused;serviceStillRunning=(Get-Service RovunetNetworkLab).Status -eq 'Running';brokerUnchanged=(Get-FileHash (Join-Path $target Rovunet.NetworkBroker.dll)).Hash -eq $hash}
}finally{if(!$helper.HasExited){$helper.Kill();[void]$helper.WaitForExit(5000)}}
# A copy with one explicit crash hook; the delivered tool itself is immutable.
$cases=Join-Path $ReportRoot child;New-Item -ItemType Directory $cases|Out-Null
foreach($name in 'MaintenanceJournal.ps1','RestartManager.ps1','recover-maintenance.ps1'){Copy-Item (Join-Path $LabSource $name) $cases}
$ready=Join-Path $ReportRoot mutation.ready
$hook='Block-RvMaintenanceImages $context $journal {param($image)Set-Content -LiteralPath '''+$ready.Replace("'","''")+''' -Value $image.Path;while($true){Start-Sleep -Milliseconds 100}}'
$text=(Get-Content $remove -Raw).Replace('Block-RvMaintenanceImages $context $journal',$hook)
$childScript=Join-Path $cases remove-crash.ps1;Set-Content $childScript $text -Encoding UTF8
$child=Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$childScript+'"'),'-ConfirmIsolatedVm','-RemoveFiles') -PassThru -WindowStyle Hidden
$until=[DateTime]::UtcNow.AddSeconds(60)
while(!(Test-Path $ready) -and !$child.HasExited -and [DateTime]::UtcNow -lt $until){Start-Sleep -Milliseconds 100}
if(!(Test-Path $ready)){throw 'No mutation marker; crash case not executed'}
$child.Kill();[void]$child.WaitForExit(5000)
$sha=[Security.Cryptography.SHA256]::Create()
try{$key=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($target).ToLowerInvariant())))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$journalDir=Join-Path (Join-Path $env:ProgramData Rovunet-Network-Maintenance) $key
Record killed_mid_acl @{childScriptHash=(Get-FileHash $childScript).Hash;journal=(Get-Content (Join-Path $journalDir operation.json) -Raw|ConvertFrom-Json)}
& (Join-Path $journalDir recover-maintenance.ps1) -ConfirmIsolatedVm
Record recovered @{service=(Get-Service RovunetNetworkLab).Status.ToString();journal=(Get-Content (Join-Path $journalDir operation.json) -Raw|ConvertFrom-Json)}
& $remove -ConfirmIsolatedVm -RemoveFiles
Record removed @{serviceAbsent=!(Get-Service RovunetNetworkLab -ErrorAction SilentlyContinue);filesAbsent=!(Test-Path $target)}
& (Join-Path $LabSource install-service-lab.ps1) -LabSource $LabSource -ConfirmIsolatedVm
Record reinstalled @{service=(Get-Service RovunetNetworkLab).Status.ToString();state=(& $exe --service-status|ConvertFrom-Json)}
@{executed=$true;events=$events;remaining=@('after-stop helper winner','after-barrier cached late-start','SCM handle-held marked-for-delete timeout','boot and data-plane capture') }|ConvertTo-Json -Depth 14|Set-Content (Join-Path $ReportRoot result.json)
