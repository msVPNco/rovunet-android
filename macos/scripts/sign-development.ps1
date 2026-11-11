param([Parameter(Mandatory=$true)][string]$App,[Parameter(Mandatory=$true)][string]$Rcodesign,[Parameter(Mandatory=$true)][string]$Rid,[string]$Python='python',[string]$Report='')
$ErrorActionPreference='Stop'
$mac=Join-Path $App 'Contents/MacOS';$engineRoot=Join-Path $mac 'engines'
$infoPath=Join-Path $App 'Contents/Resources/BUILD-INFO.json'
$info=Get-Content -LiteralPath $infoPath -Raw|ConvertFrom-Json
$info|Add-Member -NotePropertyName developmentAdhocSigned -NotePropertyValue $true -Force
$info|Add-Member -NotePropertyName signatureTool -NotePropertyValue 'apple-codesign 0.29.0' -Force
$info|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $infoPath -Encoding utf8
$entitlements=Join-Path $App 'Contents/Resources/Rovunet.entitlements'
# Two deterministic passes: sign nested native files, hash their final bytes,
# then seal the resource manifest in the outer app. No certificate or upload.
$agentScope='Contents'+[IO.Path]::DirectorySeparatorChar+'MacOS'+[IO.Path]::DirectorySeparatorChar+'Rovunet.Agent'
$tunScope='Contents'+[IO.Path]::DirectorySeparatorChar+'MacOS'+[IO.Path]::DirectorySeparatorChar+'engines'+[IO.Path]::DirectorySeparatorChar+'rovunet-tun'
& $Rcodesign sign --timestamp-url none --entitlements-xml-file ('@main:'+$entitlements) --entitlements-xml-file ($agentScope+':'+$entitlements) --binary-identifier ($agentScope+':org.rovunet.macos.agent.development') --binary-identifier ($tunScope+':org.rovunet.macos.tun') --code-signature-flags ($tunScope+':runtime') $App
if($LASTEXITCODE -ne 0){throw 'Development signing failed'}
& $Python (Join-Path $PSScriptRoot 'package-tun-service.py') (Join-Path $engineRoot 'rovunet-tun') (Join-Path $App 'Contents/Resources/Rovunet-TUN.pkg')
if($LASTEXITCODE -ne 0){throw 'TUN service package failed'}
$tunVerify=@((Join-Path $App 'Contents/Resources/Rovunet-TUN.pkg'),(Join-Path $engineRoot 'rovunet-tun'));if($Report){$tunVerify+=@('--report',(Join-Path (Split-Path $Report -Parent) "tun-installer-$Rid.json"))}
& $Python (Join-Path $PSScriptRoot 'verify-tun-package.py') @tunVerify
if($LASTEXITCODE -ne 0){throw 'TUN service package structure failed'}
& (Join-Path $PSScriptRoot 'write-engine-manifest.ps1') -EngineRoot $engineRoot -Rid $Rid
$before=Get-Content -LiteralPath (Join-Path $engineRoot 'engine-manifest.json') -Raw|ConvertFrom-Json
& $Python (Join-Path $PSScriptRoot 'seal-managed-resources.py') $App
if($LASTEXITCODE -ne 0){throw 'Managed resource sealing failed'}
& $Rcodesign sign --timestamp-url none --binary-identifier org.rovunet.macos.development --entitlements-xml-file ('@main:'+$entitlements) --info-plist-file ('@main:'+(Join-Path $App 'Contents/Info.plist')) --code-resources-file ('@main:'+(Join-Path $App 'Contents/_CodeSignature/CodeResources')) (Join-Path $mac 'Rovunet')
if($LASTEXITCODE -ne 0){throw 'Main executable resource sealing failed'}
$paths=@{'tor'='tor/tor';'lyrebird'='tor/pluggable_transports/lyrebird'}
foreach($property in $before.executables.PSObject.Properties){$relative=if($paths.ContainsKey($property.Name)){$paths[$property.Name]}else{$property.Name};if((Get-FileHash -LiteralPath (Join-Path $engineRoot $relative) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $property.Value){throw 'Signing changed engine hashes after sealing; do not package this app'}}
foreach($property in $before.files.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $engineRoot $property.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $property.Value){throw 'Signing changed dependency hashes after sealing; do not package this app'}}
$verifyArgs=@($App);if($Report){$verifyArgs+=@('--report',$Report)}
& $Python (Join-Path $PSScriptRoot 'verify-development-bundle.py') @verifyArgs
if($LASTEXITCODE -ne 0){throw 'Independent signature integrity check failed'}
Write-Output 'Development signature integrity checked; Apple codesign, Developer ID, notarization and macOS Gatekeeper acceptance remain unverified.'
