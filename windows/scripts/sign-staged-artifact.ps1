param(
 [Parameter(Mandatory=$true)][string]$Stage,
 [Parameter(Mandatory=$true)][string]$SignTool,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$ApprovedCertificateThumbprint,
 [Parameter(Mandatory=$true)][string]$ApprovedPublisherSubject,
 [Parameter(Mandatory=$true)][uri]$TimestampServer
)
$ErrorActionPreference='Stop'
if($TimestampServer.Scheme -ne 'https' -or $TimestampServer.UserInfo -or $TimestampServer.Fragment){throw 'An explicitly approved HTTPS RFC3161 timestamp service is required'}
$root=[IO.Path]::GetFullPath($Stage)
$request=Get-Content -LiteralPath (Join-Path $root 'SIGNING-REQUEST.json') -Raw|ConvertFrom-Json
if($request.schema -ne 1 -or [IO.Path]::GetFileName($request.artifact) -ne $request.artifact){throw 'Invalid signing request'}
$artifact=Join-Path $root $request.artifact
if((Get-FileHash -LiteralPath $artifact).Hash -ne $request.unsignedSha256){throw 'Stage has changed; obtain a fresh signing request'}
$cert=Get-Item -LiteralPath ('Cert:/CurrentUser/My/'+$ApprovedCertificateThumbprint)
if(!$cert.HasPrivateKey -or $cert.Subject -ne $ApprovedPublisherSubject -or $cert.NotAfter -le (Get-Date) -or $cert.NotBefore -gt (Get-Date)){throw 'Approved publisher certificate/private key is unavailable or expired'}
if(!@($cert.EnhancedKeyUsageList|Where-Object {$_.ObjectId.Value -eq '1.3.6.1.5.5.7.3.3'}).Count){throw 'Certificate is not authorized for code signing'}
if(!$cert.Verify()){throw 'Publisher certificate chain is not trusted on this machine; do not install a test root to bypass this check'}
& $SignTool sign /sha1 $ApprovedCertificateThumbprint /fd SHA256 /tr $TimestampServer.AbsoluteUri /td SHA256 $artifact
if($LASTEXITCODE -ne 0){throw 'Signing failed; original delivered artifact remains unchanged'}
& $SignTool verify /pa /all /v $artifact
if($LASTEXITCODE -ne 0){throw 'Authenticode verification failed'}
$signature=Get-AuthenticodeSignature -LiteralPath $artifact
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $ApprovedCertificateThumbprint -or !$signature.TimeStamperCertificate){throw 'Expected publisher identity and timestamp were not both verified'}
@{artifact=$request.artifact;unsignedSha256=$request.unsignedSha256;signedSha256=(Get-FileHash -LiteralPath $artifact).Hash.ToLowerInvariant();publisher=$signature.SignerCertificate.Subject;publisherThumbprint=$signature.SignerCertificate.Thumbprint;timestampAuthority=$signature.TimeStamperCertificate.Subject;status=$signature.Status.ToString();crossMachinePublicTrustStillRequiresIndependentAcceptance=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'SIGNING-RESULT.json') -Encoding utf8
