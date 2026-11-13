param([Parameter(Mandatory=$true)][string]$LabSource,[Parameter(Mandatory=$true)][switch]$ConfirmIsolatedVm,[string]$OwnerSid='')
$ErrorActionPreference='Stop'
if(Get-Service -Name 'RovunetNetworkLab' -ErrorAction SilentlyContinue){throw 'Service already exists; inspect status and use the controlled upgrade path'}
if(!$OwnerSid){$OwnerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value}
$owner=[Security.Principal.SecurityIdentifier]::new($OwnerSid)
if(!$owner.IsAccountSid() -and $OwnerSid -ne 'S-1-5-18'){throw 'One explicit local test account SID is required'}
& (Join-Path $PSScriptRoot 'install-lab.ps1') -LabSource $LabSource -ConfirmIsolatedVm:$ConfirmIsolatedVm
$target=Join-Path $env:ProgramFiles 'Rovunet-Network-Lab'
$policy=@{Version=1;OwnerSid=$OwnerSid}|ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $target 'NETWORK-SERVICE-POLICY.json'),$policy,[Text.UTF8Encoding]::new($false))
$image='"'+(Join-Path $target 'Rovunet.NetworkBroker.exe')+'" --service'
New-Service -Name 'RovunetNetworkLab' -BinaryPathName $image -DisplayName 'Rovunet isolated network service' -StartupType Automatic -DependsOn BFE|Out-Null
try{
 & "$env:WINDIR\System32\sc.exe" sidtype RovunetNetworkLab unrestricted|Out-Null;if($LASTEXITCODE -ne 0){throw 'Service SID configuration failed'}
 & "$env:WINDIR\System32\sc.exe" privs RovunetNetworkLab 'SeChangeNotifyPrivilege/SeLoadDriverPrivilege/SeImpersonatePrivilege'|Out-Null;if($LASTEXITCODE -ne 0){throw 'Service privilege configuration failed'}
 & "$env:WINDIR\System32\sc.exe" sdset RovunetNetworkLab ('D:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCRPLORC;;;'+$OwnerSid+')')|Out-Null;if($LASTEXITCODE -ne 0){throw 'Service ACL configuration failed'}
 & "$env:WINDIR\System32\sc.exe" failure RovunetNetworkLab reset= 86400 actions= 'restart/1000/restart/5000'|Out-Null;if($LASTEXITCODE -ne 0){throw 'Service recovery configuration failed'}
 Start-Service -Name RovunetNetworkLab
 (Get-Service -Name RovunetNetworkLab).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
 Write-Output 'Isolated service installed/running with a protected test-account policy. No TUN/route/DNS/filter session was started.'
}catch{throw 'Service setup needs inspection. Keep its protected directory and recovery tools; do not blindly remove journal/filter state.'}
