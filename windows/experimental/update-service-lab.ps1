param([Parameter(Mandatory=$true)][string]$LabSource,[Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm,[switch]$InjectFailureAfterSwap)
$ErrorActionPreference='Stop'
if(!$ConfirmIsolatedVm -or !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Explicit isolated-VM administrator operation required'}
$machine=Get-CimInstance Win32_ComputerSystem
if(($machine.Manufacturer+' '+$machine.Model) -notmatch '(?i)virtual machine|vmware|virtualbox|qemu|kvm|parallels'){throw 'Physical host refused'}
$target=[IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Rovunet-Network-Lab'))
if((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse root refused'}
$source=[IO.Path]::GetFullPath($LabSource)
if($source.Equals($target,[StringComparison]::OrdinalIgnoreCase) -or $source.StartsWith($target+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Fresh external source is required'}
$manifest=Get-Content -LiteralPath (Join-Path $source 'NETWORK-LAB-MANIFEST.json') -Raw|ConvertFrom-Json
foreach($item in $manifest.PSObject.Properties){$path=[IO.Path]::GetFullPath((Join-Path $source $item.Name));if(!$path.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path).Hash -ne $item.Value){throw 'New payload manifest mismatch'}}
$service=Get-CimInstance Win32_Service -Filter "Name='RovunetNetworkLab'"
if(!$service -or $service.PathName -ne ('"'+(Join-Path $target 'Rovunet.NetworkBroker.exe')+'" --service')){throw 'Service ownership mismatch'}
$status=(& (Join-Path $target 'Rovunet.NetworkLab.exe') --service-status)|ConvertFrom-Json
if($LASTEXITCODE -ne 0 -or $status.Error -or $status.ProtectionInstalled -or $status.Session -ne '00000000-0000-0000-0000-000000000000'){throw 'Recover an active or orphaned session before upgrade'}
if(Test-Path -LiteralPath (Join-Path $env:ProgramData 'Rovunet-Network-Lab/session.json')){throw 'A recovery journal remains'}
$policy=Get-Content -LiteralPath (Join-Path $target 'NETWORK-SERVICE-POLICY.json') -Raw
Stop-Service -Name RovunetNetworkLab;(Get-Service -Name RovunetNetworkLab).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
$backup=[IO.Path]::GetFullPath($target+'-backup-'+[Guid]::NewGuid().ToString('N'))
$failed=[IO.Path]::GetFullPath($target+'-failed-'+[Guid]::NewGuid().ToString('N'))
foreach($path in @($backup,$failed)){if(!$path.StartsWith($target+'-',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $path)){throw 'Archive target invalid'}}
if([IO.Path]::GetFullPath($target) -ne [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Rovunet-Network-Lab'))){throw 'Move target invalid'}
try{
 if($service.ProcessId){try{$process=Get-Process -Id $service.ProcessId -ErrorAction Stop;if(!$process.WaitForExit(30000)){throw 'Previous service process did not exit'}}catch [Microsoft.PowerShell.Commands.ProcessCommandException]{}}
 Move-Item -LiteralPath $target -Destination $backup
 & (Join-Path $source 'install-lab.ps1') -LabSource $source -ConfirmIsolatedVm
 [IO.File]::WriteAllText((Join-Path $target 'NETWORK-SERVICE-POLICY.json'),$policy,[Text.UTF8Encoding]::new($false))
 if($InjectFailureAfterSwap){throw 'Controlled test failure after swap'}
 Start-Service -Name RovunetNetworkLab;(Get-Service -Name RovunetNetworkLab).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
 $state=(& (Join-Path $target 'Rovunet.NetworkLab.exe') --service-status)|ConvertFrom-Json
 if($LASTEXITCODE -ne 0 -or $state.Error -or $state.ProtectionInstalled){throw 'New service acceptance failed'}
 Write-Output 'Isolated service upgrade verified; original protected backup retained.'
}catch{
 try{Stop-Service -Name RovunetNetworkLab -ErrorAction SilentlyContinue;(Get-Service -Name RovunetNetworkLab).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))}catch{}
 # All three absolute paths are checked above and remain under the exact
 # product prefix. Preserve failed bytes for diagnosis instead of deleting.
 if(Test-Path -LiteralPath $backup){if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination $failed};Move-Item -LiteralPath $backup -Destination $target}
 Start-Service -Name RovunetNetworkLab;(Get-Service -Name RovunetNetworkLab).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
 $state=(& (Join-Path $target 'Rovunet.NetworkLab.exe') --service-status)|ConvertFrom-Json
 if($LASTEXITCODE -ne 0 -or $state.Error -or $state.ProtectionInstalled){throw 'Rollback needs explicit recovery; retain all files'}
 if($InjectFailureAfterSwap){Write-Output 'Controlled failure rolled back; previous service verified.'}else{throw 'Upgrade failed; previous service was restored and verified'}
}
