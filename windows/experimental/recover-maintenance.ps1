param([Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm)
$ErrorActionPreference='Stop'
if(!$ConfirmIsolatedVm -or !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Isolated administrator recovery required'}
$machine=Get-CimInstance Win32_ComputerSystem
if(($machine.Manufacturer+' '+$machine.Model) -notmatch '(?i)virtual machine|vmware|virtualbox|qemu|kvm|parallels'){throw 'Physical host refused'}
. (Join-Path $PSScriptRoot MaintenanceJournal.ps1)
$target=Join-Path $env:ProgramFiles Rovunet-Network-Lab
$context=Initialize-RvMaintenance $target (Join-Path $env:ProgramData Rovunet-Network-Maintenance) @('S-1-5-32-544','S-1-5-18')
try {
 $state=Read-RvMaintenance $context
 if(!$state){throw 'No maintenance journal'}
 if($state.Service.Name -ne 'RovunetNetworkLab' -or $state.Service.Image -ne ('"'+(Join-Path $context.Target Rovunet.NetworkBroker.exe)+'" --service')){throw 'Recovery service binding mismatch'}
 if($state.Phase -in 'completed','rolled_back'){Write-Output $state.Phase;return}
 $irreversible=$state.ServiceDeletionRequested
 $errors=@(Restore-RvMaintenanceImages $context $state)
 if($errors.Count){Set-RvMaintenancePhase $context $state recovery_incomplete;throw 'ACL restoration incomplete; inspect per-file journal errors'}
 if($irreversible){Set-RvMaintenancePhase $context $state reinstall_required;throw 'ACL restored; service deletion may be pending or complete. Keep files and inspect SCM/reinstall; automatic restart refused.'}
 $service=Get-CimInstance Win32_Service -Filter "Name='RovunetNetworkLab'"
 if(!$service -or $service.PathName -ne $state.Service.Image -or $service.StartName -ne $state.Service.Account){$state.ServiceRecoveryError='Service identity changed; ACL restored, service not touched';Set-RvMaintenancePhase $context $state recovery_incomplete;throw $state.ServiceRecoveryError}
 try{if($state.Service.WasRunning){Start-Service RovunetNetworkLab;(Get-Service RovunetNetworkLab).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))}}
 catch{$state.ServiceRecoveryError=$_.Exception.Message;Set-RvMaintenancePhase $context $state recovery_incomplete;throw}
 Set-RvMaintenancePhase $context $state rolled_back
 Write-Output 'Original access rules and owned service restored. Retry removal explicitly.'
}finally{$context.Lock.Dispose()}
