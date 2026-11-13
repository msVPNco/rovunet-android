param(
 [ValidateSet('osx-arm64','osx-x64')][string]$Rid='osx-arm64',
 [Parameter(Mandatory=$true)][string]$Rcodesign,
 [Parameter(Mandatory=$true)][string]$DmgTool,
 [string]$Python='python',[string]$Dotnet='dotnet',[string]$Output=''
)
$ErrorActionPreference='Stop'
$macRoot=Split-Path $PSScriptRoot -Parent
$app=Join-Path $macRoot "artifacts/bundles/$Rid/Rovunet.app"
if(!$Output){$Output=Join-Path $macRoot 'artifacts/delivery'}
New-Item -ItemType Directory -Path $Output -Force|Out-Null
$label=if($Rid -eq 'osx-arm64'){'AppleSilicon-arm64'}else{'Intel-x64'}
$image=Join-Path $Output "Rovunet-macOS-0.1.7-beta.1-$label.dmg"
if(Test-Path -LiteralPath $image){throw 'Output already exists; choose a fresh review directory'}
& (Join-Path $PSScriptRoot 'sign-development.ps1') -App $app -Rid $Rid -Rcodesign $Rcodesign -Python $Python -Report (Join-Path $Output "signature-$Rid.json")
$finder=Join-Path $macRoot "artifacts/finder/$Rid"
New-Item -ItemType Directory -Path $finder -Force|Out-Null
& $Dotnet run --project (Join-Path $macRoot 'tools/Rovunet.Packaging') -c Release -- (Join-Path $finder '.background')
if($LASTEXITCODE -ne 0){throw 'Finder background failed'}
$volume=if($Rid -eq 'osx-arm64'){'Rovunet 0.1.7 Beta 1 Apple Silicon'}else{'Rovunet 0.1.7 Beta 1 Intel'}
& $Python (Join-Path $macRoot 'packaging/finder-layout.py') $finder --volume $volume
if($LASTEXITCODE -ne 0){throw 'Finder metadata failed'}
& $DmgTool --app $app --finder $finder --out $image --volume $volume
if($LASTEXITCODE -ne 0){throw 'DMG build failed'}
& $Python (Join-Path $PSScriptRoot 'verify-udif.py') $image --report (Join-Path $Output "udif-$Rid.json")
if($LASTEXITCODE -ne 0){throw 'UDIF CRC failed'}
Copy-Item -LiteralPath (Join-Path $macRoot 'packaging/INSTALL-FA.txt') -Destination $Output -Force
Get-FileHash -LiteralPath $image -Algorithm SHA256
