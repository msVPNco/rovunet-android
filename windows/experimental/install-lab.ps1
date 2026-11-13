param([Parameter(Mandatory=$true)][string]$LabSource,[Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm)
$ErrorActionPreference='Stop'
if(!$ConfirmIsolatedVm){throw 'An explicitly confirmed isolated Windows VM is required'}
if(!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run this script explicitly as administrator in the isolated test VM'}
$machine=Get-CimInstance Win32_ComputerSystem
if(($machine.Manufacturer+' '+$machine.Model) -notmatch '(?i)virtual machine|vmware|virtualbox|qemu|kvm|parallels'){throw 'This test installer refuses an unrecognized physical host'}
$source=[IO.Path]::GetFullPath($LabSource)
$parsedManifest=Get-Content -LiteralPath (Join-Path $source 'NETWORK-LAB-MANIFEST.json') -Raw|ConvertFrom-Json
$manifest=@{};foreach($entry in $parsedManifest.PSObject.Properties){$manifest[$entry.Name]=[string]$entry.Value}
foreach($relative in $manifest.Keys){$file=[IO.Path]::GetFullPath((Join-Path $source $relative));if(!$file.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $file).Hash -ne $manifest[$relative]){throw 'Lab source manifest mismatch'}}
$target=Join-Path $env:ProgramFiles 'Rovunet-Network-Lab'
if(Test-Path -LiteralPath $target){throw 'Use a fresh VM install; do not overwrite an active network lab or its recovery tools'}
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'));$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'),'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'))
$directory=[IO.DirectoryInfo]::new($target)
if($PSVersionTable.PSEdition -eq 'Desktop'){$directory.Create($acl)}else{[IO.FileSystemAclExtensions]::Create($directory,$acl)}
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $target -Recurse
$adminOwner=[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
foreach($item in Get-ChildItem -LiteralPath $target -Recurse -Force){if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse points are not allowed in the lab installation'};$permissions=Get-Acl -LiteralPath $item.FullName;$permissions.SetOwner($adminOwner);$permissions.SetAccessRuleProtection($false,$true);Set-Acl -LiteralPath $item.FullName -AclObject $permissions}
foreach($relative in $manifest.Keys){if((Get-FileHash -LiteralPath (Join-Path $target $relative)).Hash -ne $manifest[$relative]){throw 'Copied lab files do not match the approved source manifest'}}
$acceptance=@{purpose='isolated-windows-network-test';testOnly=$true;installId=[Guid]::NewGuid().ToString();machine=(Get-ItemProperty 'HKLM:/SOFTWARE/Microsoft/Cryptography').MachineGuid;sourceManifestSha256=(Get-FileHash -LiteralPath (Join-Path $target 'NETWORK-LAB-MANIFEST.json')).Hash.ToLowerInvariant()}|ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $target 'NETWORK-LAB-ACCEPTANCE.json'),$acceptance,[Text.UTF8Encoding]::new($false))
Write-Output "Installed isolated lab at $target. No service, TUN, route, DNS or firewall was started. Keep this directory until successful recovery."
