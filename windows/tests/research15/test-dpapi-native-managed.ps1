param([string]$Dotnet,[string]$Diagnostic,[string]$Root,[string]$Report)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Root){throw 'Fresh synthetic root required'}
New-Item -ItemType Directory -Path $Root|Out-Null
$acl=Get-Acl $Root;$acl.SetAccessRuleProtection($true,$false);$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl.SetOwner($sid);$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow));Set-Acl $Root $acl
$results=@()
foreach($writer in 'native','dotnet'){foreach($reader in 'native','dotnet'){
 $id=$writer+'_'+$reader
 & $Dotnet $Diagnostic protect $writer $Root $id
 if($LASTEXITCODE -ne 0){throw 'Synthetic protect failed'}
 & $Dotnet $Diagnostic unprotect $reader $Root $id
 $decodeExit=$LASTEXITCODE
 $protect=Get-Content (Join-Path $Root "$id.protect.$writer.result.json") -Raw|ConvertFrom-Json
 $unprotect=Get-Content (Join-Path $Root "$id.unprotect.$reader.result.json") -Raw|ConvertFrom-Json
 $results+=@{id=$id;protect=$protect;unprotect=$unprotect;exit=$decodeExit}
}}
$checks=@{
 all_four_pairs_decrypt=@($results|Where-Object {!$_.protect.passed -or !$_.unprotect.passed -or $_.exit -ne 0}).Count -eq 0
 different_random_plaintext_each_case=@($results|ForEach-Object {$_.protect.expectedHash}|Sort-Object -Unique).Count -eq 4
 protected_blobs_unchanged=@($results|Where-Object {$_.unprotect.blobHashBefore -ne $_.unprotect.blobHashAfter}).Count -eq 0
 process_and_effective_identity_match=@($results|Where-Object {$_.protect.before.processSid -ne $_.unprotect.before.processSid -or $_.protect.before.effectiveSid -ne $_.unprotect.before.effectiveSid}).Count -eq 0
 profiles_loaded=@($results|Where-Object {!$_.protect.before.profileLoaded -or !$_.unprotect.before.profileLoaded}).Count -eq 0
 direct_native_flags_are_ui_forbidden=@($results|Where-Object {($_.protect.backend -eq 'native' -and $_.protect.nativeFlags -ne 1) -or ($_.unprotect.backend -eq 'native' -and $_.unprotect.nativeFlags -ne 1)}).Count -eq 0
 native_calls_succeeded_with_immediate_error_capture=@($results|Where-Object {($_.protect.backend -eq 'native' -and !$_.protect.nativeSucceeded) -or ($_.unprotect.backend -eq 'native' -and !$_.unprotect.nativeSucceeded)}).Count -eq 0
}
@{passed=!($checks.Values -contains $false);checks=$checks;results=$results;scope='same actual host user, Win32/ProtectedData cross-decrypt only; SYSTEM/LocalService execution not repeated';root=$Root}|ConvertTo-Json -Depth 14|Set-Content $Report
$checks|ConvertTo-Json
