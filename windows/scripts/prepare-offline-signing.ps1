param(
 [Parameter(Mandatory=$true)][string]$Artifact,
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSha256,
 [Parameter(Mandatory=$true)][string]$FreshStage
)
$ErrorActionPreference='Stop'
$inputFile=Get-Item -LiteralPath $Artifact
if($inputFile.Extension -ne '.exe' -or $inputFile.Name -notmatch '^Rovunet[.\-]'){throw 'Only explicit Rovunet executable artifacts are accepted'}
if((Get-FileHash -LiteralPath $inputFile.FullName).Hash -ne $ExpectedSha256){throw 'Unsigned artifact hash mismatch'}
$stage=[IO.Path]::GetFullPath($FreshStage)
if(Test-Path -LiteralPath $stage){throw 'A fresh signing stage is required; never overwrite a delivered build'}
[IO.Directory]::CreateDirectory($stage)|Out-Null
Copy-Item -LiteralPath $inputFile.FullName -Destination $stage
@{schema=1;artifact=$inputFile.Name;unsignedSha256=$ExpectedSha256.ToLowerInvariant();bytes=$inputFile.Length;signatureVerified=$false;publisherIdentityRequired=$true;timestampRequired=$true;repackageInnerSignedBinariesBeforeSigningOuterInstaller=$true;originalArtifactChanged=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $stage 'SIGNING-REQUEST.json') -Encoding utf8
Write-Output $stage
