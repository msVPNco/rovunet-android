param([string]$Module,[string]$Child,[string]$Root,[string]$Report)
$ErrorActionPreference='Stop'
. $Module
function Rules($path){((Get-Acl -LiteralPath $path).GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])|ForEach-Object {'{0}|{1}|{2}|{3}|{4}|{5}' -f $_.IdentityReference.Value,[int]$_.FileSystemRights,$_.AccessControlType,$_.IsInherited,$_.InheritanceFlags,$_.PropagationFlags}) -join ';'}
New-Item -ItemType Directory -Path $Root|Out-Null
$target=Join-Path $Root images;$store=Join-Path $Root recovery
New-Item -ItemType Directory $target|Out-Null
Copy-Item $env:ComSpec (Join-Path $target a.exe);Copy-Item $env:ComSpec (Join-Path $target b.exe)
Set-Content (Join-Path $target NETWORK-LAB-ACCEPTANCE.json) 'synthetic-maintenance-only'
$before=@{};foreach($f in Get-ChildItem $target -Filter '*.exe'){$before[$f.Name]=Rules $f.FullName}
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$c=Initialize-RvMaintenance $target $store @($sid);$contextDir=$c.Directory;$journalPath=$c.Journal;$c.Lock.Dispose()
function Launch($mode){
 $info=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 foreach($arg in @('-NoProfile','-File',$Child,'-Module',$Module,'-Target',$target,'-Store',$store,'-Mode',$mode)){$info.ArgumentList.Add($arg)}
 [Diagnostics.Process]::Start($info)
}
$checks=[ordered]@{}
$p=Launch crash
$until=[DateTime]::UtcNow.AddSeconds(20)
while(!(Test-Path (Join-Path $contextDir ready.txt)) -and [DateTime]::UtcNow -lt $until -and !$p.HasExited){Start-Sleep -Milliseconds 50}
if(!(Test-Path (Join-Path $contextDir ready.txt))){throw 'Child never reached mutation'}
$persisted=Get-Content $journalPath -Raw|ConvertFrom-Json
$checks.full_acl_preimages_durable_before_first_mutation=$persisted.Images.Count -eq 2 -and $persisted.Phase -eq 'blocking_execution'
$checks.journal_outside_target=!$journalPath.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)
$blockPath=Get-Content (Join-Path $contextDir ready.txt) -Raw
$checks.first_acl_changed=(Rules $blockPath.Trim()) -ne $before[[IO.Path]::GetFileName($blockPath.Trim())]
$p.Kill();[void]$p.WaitForExit(5000)
$checks.child_terminated_without_catch=$p.HasExited
$recover=Launch recover
$checks.independent_recovery_exits=$recover.WaitForExit(20000) -and $recover.ExitCode -eq 0
$r=Get-Content (Join-Path $contextDir result.json) -Raw|ConvertFrom-Json
$checks.recovery_reports_no_file_failure=@($r.errors).Count -eq 0
$checks.original_access_rules_restored=@(Get-ChildItem $target -Filter '*.exe'|Where-Object {(Rules $_.FullName) -ne $before[$_.Name]}).Count -eq 0
$checks.operation_rolled_back=$r.phase -eq 'rolled_back'
$c=Initialize-RvMaintenance $target $store @($sid)
try {
 $s=New-RvMaintenanceState $c @{Name='synthetic';Image='synthetic';WasRunning=$false} (Get-FileHash (Join-Path $target NETWORK-LAB-ACCEPTANCE.json)).Hash
 Block-RvMaintenanceImages $c $s
 $originalIdentity=$s.Images[0].Identity;$s.Images[0].Identity='different-file'
 Write-RvMaintenance $c $s
 $s=Read-RvMaintenance $c;$errors=@(Restore-RvMaintenanceImages $c $s)
 $checks.replaced_file_not_given_old_acl=$errors.Count -eq 1 -and $errors[0].Path -eq $s.Images[0].Path
 $checks.other_files_restored_despite_one_error=(Rules $s.Images[1].Path) -eq $before[[IO.Path]::GetFileName($s.Images[1].Path)]
 $s.Images[0].Identity=$originalIdentity;[void](Restore-RvMaintenanceImages $c $s);Set-RvMaintenancePhase $c $s rolled_back
 $hash=(Get-FileHash $c.Journal).Hash
 $saved=Get-Content (Join-Path $target NETWORK-LAB-ACCEPTANCE.json) -Raw
 Set-Content (Join-Path $target NETWORK-LAB-ACCEPTANCE.json) 'different-installation'
 $refused=$false;try{Read-RvMaintenance $c|Out-Null}catch{$refused=$true}
 $checks.other_installation_receipt_refused=$refused -and (Get-FileHash $c.Journal).Hash -eq $hash
 [IO.File]::WriteAllText((Join-Path $target NETWORK-LAB-ACCEPTANCE.json),$saved)
 $foreign=Join-Path $Root foreign.exe;Copy-Item $env:ComSpec $foreign;$foreignRules=Rules $foreign
 $link=Join-Path $target linked.exe
 New-Item -ItemType HardLink -Path $link -Target $foreign|Out-Null
 $refused=$false;try{New-RvMaintenanceState $c @{Name='synthetic';Image='synthetic';WasRunning=$false} (Get-FileHash (Join-Path $target NETWORK-LAB-ACCEPTANCE.json)).Hash|Out-Null}catch{$refused=$true}
 $checks.hard_link_to_external_image_refused=$refused
 $checks.external_link_acl_not_changed=(Rules $foreign) -eq $foreignRules
 if(![IO.Path]::GetFullPath($link).StartsWith([IO.Path]::GetFullPath($Root)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected fixture link'}
 Remove-Item -LiteralPath $link -Force
 $checks.private_store_has_only_expected_writer=@((Get-Acl $c.Directory).GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])|Where-Object {$_.AccessControlType -eq 'Allow' -and $_.IdentityReference.Value -ne $sid}).Count -eq 0
}finally{$c.Lock.Dispose()}
@{passed=!($checks.Values -contains $false);checks=$checks;scope='actual host files, child killed between ACL mutations, separate recovery process; no SCM/route/DNS/WFP changes';root=$Root;identity=$sid;os=[Environment]::OSVersion.VersionString}|ConvertTo-Json -Depth 7|Set-Content $Report
$checks|ConvertTo-Json

