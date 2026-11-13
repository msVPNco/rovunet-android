param(
 [Parameter(Mandatory=$true)][string]$Dotnet,
 [Parameter(Mandatory=$true)][string]$VerifiedPayload,
 [Parameter(Mandatory=$true)][string]$WintunDll,
 [Parameter(Mandatory=$true)][string]$WintunLicense,
 [Parameter(Mandatory=$true)][string]$FreshOutput,
 [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceRevision
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));$output=[IO.Path]::GetFullPath($FreshOutput)
if(Test-Path -LiteralPath $output){throw 'Choose a fresh lab output; previous evidence must remain unchanged'}
$provenance=Get-Content -LiteralPath (Join-Path $VerifiedPayload 'BUILD-PROVENANCE.json') -Raw|ConvertFrom-Json
if($provenance.source_commit -ne $SourceRevision){throw 'Normal payload source identity differs'}
if((Get-FileHash -LiteralPath $WintunDll).Hash.ToLowerInvariant() -ne 'e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce'){throw 'Official unmodified amd64 Wintun pin mismatch'}
$signature=Get-AuthenticodeSignature -LiteralPath $WintunDll
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'WireGuard LLC'){throw 'Official Wintun signature verification failed'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$stage=Join-Path $output 'lab';[IO.Directory]::CreateDirectory($stage)|Out-Null
Get-ChildItem -LiteralPath $VerifiedPayload|Copy-Item -Destination $stage -Recurse
Push-Location $root
try{
 foreach($name in @('Rovunet.NetworkBroker','Rovunet.NetworkLab')){
  $project='experimental/'+$name;$publish=Join-Path $output $name
  & $Dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -m:1 "-p:SourceRevisionId=$SourceRevision" -o $publish
  if($LASTEXITCODE -ne 0){throw 'Network lab publish failed'}
  foreach($shared in @('Rovunet.Core.dll','Rovunet.Platform.dll')){if((Get-FileHash -LiteralPath (Join-Path $publish $shared)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $stage $shared)).Hash){throw 'Lab does not share delivered product assemblies'}}
  foreach($ext in @('.exe','.dll','.deps.json','.runtimeconfig.json')){Copy-Item -LiteralPath (Join-Path $publish ($name+$ext)) -Destination $stage}
 }
}finally{Pop-Location}
Copy-Item -LiteralPath $WintunDll -Destination (Join-Path $stage 'engines/wintun.dll')
Copy-Item -LiteralPath $WintunLicense -Destination (Join-Path $stage 'licenses/wintun-binaries.txt')
Copy-Item -LiteralPath (Join-Path $root 'experimental/README.md') -Destination (Join-Path $stage 'NETWORK-LAB-README.md')
Copy-Item -LiteralPath (Join-Path $root 'experimental/install-lab.ps1') -Destination $stage
foreach($script in @('install-service-lab.ps1','remove-service-lab.ps1','update-service-lab.ps1')){if(Test-Path -LiteralPath (Join-Path $root ('experimental/'+$script))){Copy-Item -LiteralPath (Join-Path $root ('experimental/'+$script)) -Destination $stage}}
@{schema=2;sourceCommit=$SourceRevision;experimental=$true;consumerAppEnabled=$false;isolatedVmRequired=$true;trustedRootRequired=$true;serviceImplemented=$true;udpForwardingImplemented=$true;udpDefaultEnabled=$false;udpRequiresCompatibleSelectedUpstream=$true;nativeAcceptanceReportRequired=$true;preBfeBootProtectionClaimed=$false;wintunVendorSignature=$signature.Status.ToString();publisher=$signature.SignerCertificate.Subject;productAuthenticodeSigned=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $stage 'NETWORK-LAB-BUILD.json') -Encoding utf8
$manifest=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $stage -File -Recurse|Sort-Object FullName){$relative=[IO.Path]::GetRelativePath($stage,$file.FullName).Replace('\','/');$manifest[$relative]=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()}
$manifest|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $stage 'NETWORK-LAB-MANIFEST.json') -Encoding utf8
& (Join-Path $stage 'Rovunet.NetworkBroker.exe') --self-test > (Join-Path $output 'readonly-self-test.json')
if($LASTEXITCODE -ne 0){throw 'Final lab read-only checks failed'}
& (Join-Path $stage 'Rovunet.NetworkBroker.exe') --config-test > (Join-Path $output 'pinned-config-test.json')
if($LASTEXITCODE -ne 0){throw 'Final pinned Xray TUN config validation failed'}
Write-Output 'Experimental lab built; only read-only/config checks executed, no install/elevation/network mutation.'
