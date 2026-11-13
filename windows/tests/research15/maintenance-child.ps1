param([string]$Module,[string]$Target,[string]$Store,[string]$Mode)
$ErrorActionPreference='Stop'
. $Module
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$c=Initialize-RvMaintenance $Target $Store @($sid)
try {
 if($Mode -eq 'crash') {
  $s=New-RvMaintenanceState $c @{Name='synthetic';Image='synthetic';WasRunning=$false} (Get-FileHash (Join-Path $Target NETWORK-LAB-ACCEPTANCE.json)).Hash
  Block-RvMaintenanceImages $c $s {param($image)Set-Content (Join-Path $c.Directory ready.txt) $image.Path;while($true){Start-Sleep -Milliseconds 100}}
 } else {
  $s=Read-RvMaintenance $c;$errors=@(Restore-RvMaintenanceImages $c $s)
  if(!$errors.Count){Set-RvMaintenancePhase $c $s rolled_back}
  @{errors=$errors;phase=$s.Phase;operationId=$s.OperationId}|ConvertTo-Json -Depth 7|Set-Content (Join-Path $c.Directory result.json)
 }
}finally{$c.Lock.Dispose()}
