param([Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm,[switch]$RecoverOwnedSession,[switch]$RemoveFiles)
$ErrorActionPreference='Stop'
if(!$ConfirmIsolatedVm -or !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Explicit isolated-VM administrator operation required'}
$machine=Get-CimInstance Win32_ComputerSystem
if(($machine.Manufacturer+' '+$machine.Model) -notmatch '(?i)virtual machine|vmware|virtualbox|qemu|kvm|parallels'){throw 'Physical host refused'}
. (Join-Path $PSScriptRoot MaintenanceJournal.ps1)
. (Join-Path $PSScriptRoot RestartManager.ps1)
$target=Get-RvCanonical (Join-Path $env:ProgramFiles Rovunet-Network-Lab)
$context=Initialize-RvMaintenance $target (Join-Path $env:ProgramData Rovunet-Network-Maintenance) @('S-1-5-32-544','S-1-5-18')
$journal=$null;$handles=[Collections.Generic.List[IO.FileStream]]::new()
try {
 # The independent recovery executable is Windows PowerShell, not a denied image.
 foreach($name in 'MaintenanceJournal.ps1','recover-maintenance.ps1'){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $context.Directory $name) -Force}
 $pending=Read-RvMaintenance $context
 if($pending -and $pending.Phase -notin 'completed','rolled_back'){throw ('Pending maintenance. Run the external recovery script first: '+(Join-Path $context.Directory recover-maintenance.ps1))}
 $service=Get-CimInstance Win32_Service -Filter "Name='RovunetNetworkLab'"
 $image='"'+(Join-Path $target Rovunet.NetworkBroker.exe)+'" --service'
 if(!$service -or $service.PathName -ne $image){throw 'Service ownership mismatch'}
 $client=Join-Path $target Rovunet.NetworkLab.exe
 $reply=if($RecoverOwnedSession){& $client --service-recover}else{& $client --service-status}
 if($LASTEXITCODE -ne 0){throw 'Status/recovery failed; keep all recovery files'}
 $state=$reply|ConvertFrom-Json
 if($state.State -ne 'inactive' -or $state.Error -or $state.ProtectionInstalled -or $state.Session -ne '00000000-0000-0000-0000-000000000000'){throw 'Active/orphaned protection must be recovered before removal'}
 if(Test-Path -LiteralPath (Join-Path $env:ProgramData Rovunet-Network-Lab/session.json)){throw 'Network recovery journal remains'}
 $prefix=$target+'\'
 if($RemoveFiles -and (Get-CimInstance Win32_Process|Where-Object {$_.ProcessId -ne $service.ProcessId -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)})){throw 'Helper is running; stop its own fixture and retry'}
 $identity=(Get-FileHash -LiteralPath (Join-Path $target NETWORK-LAB-ACCEPTANCE.json)).Hash
 $journal=New-RvMaintenanceState $context @{Name='RovunetNetworkLab';Image=$image;Account=$service.StartName;StartMode=$service.StartMode;OriginalState=$service.State;WasRunning=$service.State -eq 'Running'} $identity
 Set-RvMaintenancePhase $context $journal stopping_service
 Stop-Service RovunetNetworkLab
 (Get-Service RovunetNetworkLab).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
 Set-RvMaintenancePhase $context $journal service_stopped
 # TEST_INSERT_AFTER_STOP
 if($RemoveFiles) {
  Block-RvMaintenanceImages $context $journal
  if(Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)}){throw 'Helper won before the execution barrier; retain installation'}
  $owners=@([RvRestartManager]::Owners([string[]]@($journal.Images|ForEach-Object {$_.Path})))
  if($owners.Count){throw ('Owned images still used by process IDs: '+($owners -join ',')+'. No process was terminated.')}
  foreach($file in $journal.Images){$handles.Add([IO.File]::Open($file.Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Delete))}
  # TEST_INSERT_AFTER_LOCKS
 }
 if(Test-Path -LiteralPath (Join-Path $env:ProgramData Rovunet-Network-Lab/session.json)){throw 'Network recovery journal appeared; retain installation'}
 $journal.ServiceDeletionRequested=$true
 Set-RvMaintenancePhase $context $journal delete_requested
 & "$env:WINDIR\System32\sc.exe" delete RovunetNetworkLab|Out-Null
 if($LASTEXITCODE -ne 0){throw 'DeleteService failed or is pending; retain journal/files'}
 Set-RvMaintenancePhase $context $journal deletion_pending
 $until=[DateTime]::UtcNow.AddSeconds(30)
 while((Get-Service RovunetNetworkLab -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $until){Start-Sleep -Milliseconds 100}
 if(Get-Service RovunetNetworkLab -ErrorAction SilentlyContinue){throw 'Service remains marked for deletion; retry recovery after external handles close'}
 Set-RvMaintenancePhase $context $journal scm_removed
 if($RemoveFiles) {
  if((Get-RvCanonical $target) -ne (Get-RvCanonical (Join-Path $env:ProgramFiles Rovunet-Network-Lab)) -or [RvMaintenanceFileIdentity]::Read($target) -ne $journal.DirectoryIdentity){throw 'Deletion target identity changed'}
  if(Get-ChildItem -LiteralPath $target -Recurse -Force|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}){throw 'Reparse entry appeared; refuse deletion'}
  Set-RvMaintenancePhase $context $journal removing_files
  Remove-Item -LiteralPath $target -Recurse -Force
 }
 Set-RvMaintenancePhase $context $journal completed
 Write-Output 'Isolated service fully removed. Durable maintenance receipt retained outside target.'
}catch{
 $failure=$_
 foreach($handle in $handles){$handle.Dispose()};$handles.Clear()
 if($journal) {
  $irreversible=$journal.ServiceDeletionRequested
  $errors=@(Restore-RvMaintenanceImages $context $journal)
  if($errors.Count){Set-RvMaintenancePhase $context $journal recovery_incomplete}
  elseif($irreversible){Set-RvMaintenancePhase $context $journal reinstall_required}
  else{
   try {
    $now=Get-CimInstance Win32_Service -Filter "Name='RovunetNetworkLab'"
    if(!$now -or $now.PathName -ne $journal.Service.Image -or $now.StartName -ne $journal.Service.Account){throw 'Service identity changed'}
    if($journal.Service.WasRunning){Start-Service RovunetNetworkLab;(Get-Service RovunetNetworkLab).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))}
    Set-RvMaintenancePhase $context $journal rolled_back
   }catch{$journal.ServiceRecoveryError=$_.Exception.Message;Set-RvMaintenancePhase $context $journal recovery_incomplete}
  }
 }
 throw $failure
}finally{foreach($handle in $handles){$handle.Dispose()};$context.Lock.Dispose()}
