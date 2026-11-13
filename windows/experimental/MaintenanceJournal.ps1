# Shared by the isolated remover and the recovery copy kept outside its target.
Set-StrictMode -Version 2
if(-not ('RvMaintenanceFileIdentity' -as [type])) {
 Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class RvMaintenanceFileIdentity {
 [StructLayout(LayoutKind.Sequential)] struct Info { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write; public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow; }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle file,out Info info);
 public static string Read(string path) {
  using(var file=CreateFileW(path,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero)) {
   Info info;if(file.IsInvalid||!GetFileInformationByHandle(file,out info))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   return info.Volume.ToString("X8")+":"+info.IndexHigh.ToString("X8")+info.IndexLow.ToString("X8");
  }
 }
 public static uint Links(string path) {
  using(var file=CreateFileW(path,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero)) {
   Info info;if(file.IsInvalid||!GetFileInformationByHandle(file,out info))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return info.Links;
  }
 }
}
'@
}
function Get-RvCanonical([string]$Path) {
 $full=[IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
 for($p=$full;$p;$p=[IO.Path]::GetDirectoryName($p)) {
  if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Maintenance reparse path refused'}
 }
 return $full
}
function Initialize-RvMaintenance([string]$Target,[string]$StateRoot,[string[]]$Writers) {
 $target=Get-RvCanonical $Target;$stateRoot=Get-RvCanonical $StateRoot
 if($stateRoot.Equals($target,[StringComparison]::OrdinalIgnoreCase) -or $stateRoot.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Recovery must live outside deletion target'}
 if(!(Test-Path -LiteralPath $stateRoot)) {
  $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
  $acl.SetOwner([Security.Principal.SecurityIdentifier]::new($Writers[0]))
  foreach($sid in $Writers){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))}
  $directory=[IO.DirectoryInfo]::new($stateRoot)
  if($directory.PSObject.Methods['Create'].OverloadDefinitions -match 'DirectorySecurity'){$directory.Create($acl)}else{[IO.FileSystemAclExtensions]::Create($directory,$acl)}
 }
 $acl=Get-Acl -LiteralPath $stateRoot
 if($Writers -notcontains $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value){throw 'Recovery owner mismatch'}
 foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
  if($rule.AccessControlType -eq 'Allow' -and $Writers -notcontains $rule.IdentityReference.Value){throw 'Recovery directory is not private'}
 }
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$key=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($target.ToLowerInvariant())))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
 $directory=Join-Path $stateRoot $key
 if(!(Test-Path -LiteralPath $directory)){New-Item -ItemType Directory -Path $directory|Out-Null}
 [void](Get-RvCanonical $directory)
 $lock=[IO.File]::Open((Join-Path $directory operation.lock),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 return @{Target=$target;Directory=$directory;Journal=(Join-Path $directory operation.json);Lock=$lock}
}
function Write-RvMaintenance($Context,$State) {
 $bytes=[Text.Encoding]::UTF8.GetBytes(($State|ConvertTo-Json -Depth 14 -Compress))
 if($bytes.Length -gt 4194304){throw 'Maintenance journal too large'}
 $temp=Join-Path $Context.Directory ([Guid]::NewGuid().ToString('N')+'.tmp')
 try {
  $file=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None,4096,[IO.FileOptions]::WriteThrough)
  try{$file.Write($bytes,0,$bytes.Length);$file.Flush($true)}finally{$file.Dispose()}
  if(Test-Path -LiteralPath $Context.Journal){[IO.File]::Replace($temp,$Context.Journal,(Join-Path $Context.Directory operation.previous.json))}else{[IO.File]::Move($temp,$Context.Journal)}
 }finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Force}}
}
function Read-RvMaintenance($Context) {
 if(!(Test-Path -LiteralPath $Context.Journal)){return $null}
 [void](Get-RvCanonical $Context.Journal)
 if((Get-Item -LiteralPath $Context.Journal).Length -gt 4194304){throw 'Maintenance journal too large'}
 $state=Get-Content -LiteralPath $Context.Journal -Raw|ConvertFrom-Json
 if($state.Schema -ne 1 -or $state.Target -ne $Context.Target -or $state.OperationId -notmatch '^[0-9a-f]{32}$' -or $state.Images.Count -gt 2048){throw 'Maintenance journal binding invalid'}
 # Terminal receipts are history only. A fresh reinstall may reuse the path,
 # but must never receive the previous installation's ACLs.
 if($state.Phase -eq 'completed'){return $state}
 if((Test-Path -LiteralPath $Context.Target) -and [RvMaintenanceFileIdentity]::Read($Context.Target) -ne $state.DirectoryIdentity){throw 'Different installation directory; preserve journal'}
 $receipt=Join-Path $Context.Target NETWORK-LAB-ACCEPTANCE.json
 if((Test-Path -LiteralPath $receipt) -and (Get-FileHash -LiteralPath $receipt).Hash -ne $state.InstallIdentity){throw 'Installation receipt changed; refuse recovery replay'}
 foreach($image in $state.Images) {
  $path=[IO.Path]::GetFullPath($image.Path)
  if(!$path.StartsWith($Context.Target+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($path) -notin '.exe','.dll'){throw 'Invalid maintenance image path'}
  # Per-file identity failures are reported by restoration without stopping the
  # remaining files. A replacement never receives an old access descriptor.
 }
 return $state
}
function New-RvMaintenanceState($Context,$Service,[string]$InstallIdentity) {
 $pending=Read-RvMaintenance $Context
 if($pending -and $pending.Phase -notin 'completed','rolled_back'){throw 'Pending maintenance requires recovery first'}
 if($pending){Copy-Item -LiteralPath $Context.Journal -Destination (Join-Path $Context.Directory ($pending.OperationId+'.json'))}
 $images=@(Get-ChildItem -LiteralPath $Context.Target -File -Recurse -Force|Where-Object {$_.Extension -in '.exe','.dll'}|ForEach-Object {
  [void](Get-RvCanonical $_.FullName)
  if([RvMaintenanceFileIdentity]::Links($_.FullName) -ne 1){throw 'Hard-linked image refused; no external file ACL may be changed'}
  @{Path=$_.FullName;Identity=[RvMaintenanceFileIdentity]::Read($_.FullName);Hash=(Get-FileHash -LiteralPath $_.FullName).Hash;Sddl=(Get-Acl -LiteralPath $_.FullName).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)}
 })
 $state=[ordered]@{Schema=1;OperationId=[Guid]::NewGuid().ToString('N');Target=$Context.Target;DirectoryIdentity=[RvMaintenanceFileIdentity]::Read($Context.Target);InstallIdentity=$InstallIdentity;Service=$Service;ServiceDeletionRequested=$false;Phase='prepared';Images=$images;RestoreErrors=@();ServiceRecoveryError='';UpdatedUtc=[DateTime]::UtcNow.ToString('O')}
 Write-RvMaintenance $Context $state
 return $state
}
function Set-RvMaintenancePhase($Context,$State,[string]$Phase) {$State.Phase=$Phase;$State.UpdatedUtc=[DateTime]::UtcNow.ToString('O');Write-RvMaintenance $Context $State}
function Block-RvMaintenanceImages($Context,$State,[scriptblock]$AfterEach={}) {
 Set-RvMaintenancePhase $Context $State 'blocking_execution'
 foreach($image in $State.Images) {
  [void](Get-RvCanonical $image.Path)
  if([RvMaintenanceFileIdentity]::Read($image.Path) -ne $image.Identity -or [RvMaintenanceFileIdentity]::Links($image.Path) -ne 1 -or (Get-FileHash -LiteralPath $image.Path).Hash -ne $image.Hash){throw 'Image changed before barrier; retain installation'}
  $acl=Get-Acl -LiteralPath $image.Path
  $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-1-0'),[Security.AccessControl.FileSystemRights]::ExecuteFile,[Security.AccessControl.AccessControlType]::Deny))
  Set-Acl -LiteralPath $image.Path -AclObject $acl
  & $AfterEach $image
 }
 Set-RvMaintenancePhase $Context $State 'images_blocked'
}
function Restore-RvMaintenanceImages($Context,$State) {
 $failures=[Collections.Generic.List[object]]::new()
 foreach($image in $State.Images) {
  try {
   [void](Get-RvCanonical $image.Path)
   if(!(Test-Path -LiteralPath $image.Path)){if(!$State.ServiceDeletionRequested){throw 'Image missing before deletion request'};continue}
   if([RvMaintenanceFileIdentity]::Read($image.Path) -ne $image.Identity -or [RvMaintenanceFileIdentity]::Links($image.Path) -ne 1 -or (Get-FileHash -LiteralPath $image.Path).Hash -ne $image.Hash){throw 'Image identity or link ownership changed'}
   $acl=Get-Acl -LiteralPath $image.Path;$acl.SetSecurityDescriptorSddlForm($image.Sddl,[Security.AccessControl.AccessControlSections]::Access)
   Set-Acl -LiteralPath $image.Path -AclObject $acl
  }catch{$failures.Add(@{Path=$image.Path;Error=$_.Exception.Message})}
 }
 $State.RestoreErrors=@($failures.ToArray());Write-RvMaintenance $Context $State
 return @($failures.ToArray())
}
