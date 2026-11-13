param(
    [ValidateSet('osx-arm64','osx-x64')][string]$Rid='osx-arm64',
    [string]$Dotnet='dotnet',
    [string]$Go='go',
    [string]$Python='python',
    [string]$MihomoSource='',
    [string]$CacheRoot='',
    [switch]$SkipNativeBuild
)
$ErrorActionPreference='Stop'
$macRoot=Split-Path $PSScriptRoot -Parent
$repoRoot=Split-Path $macRoot -Parent
$artifacts=Join-Path $macRoot 'artifacts'
if(!$CacheRoot){$CacheRoot=Join-Path $artifacts 'downloads'}
New-Item -ItemType Directory -Path $artifacts,$CacheRoot -Force | Out-Null
$engineRoot=Join-Path $macRoot "engines/$Rid"
New-Item -ItemType Directory -Path $engineRoot -Force | Out-Null
foreach($asset in (Get-Content -LiteralPath (Join-Path $macRoot 'engines/upstream-pins.json') -Raw|ConvertFrom-Json).assets|Where-Object {$_.rid -eq $Rid -and $_.engine -ne 'slipstream-client'}){
    $download=Join-Path $CacheRoot $asset.name
    if(!(Test-Path -LiteralPath $download)){Invoke-WebRequest -Uri $asset.url -OutFile $download}
    if((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.sha256){throw ('Archive hash mismatch: '+$asset.name)}
    $expanded=Join-Path $artifacts "expanded/$Rid-$($asset.engine)"
    New-Item -ItemType Directory -Path $expanded -Force | Out-Null
    if($asset.name.EndsWith('.zip')){Expand-Archive -LiteralPath $download -DestinationPath $expanded -Force}else{& tar -xf $download -C $expanded;if($LASTEXITCODE -ne 0){throw 'Extraction failed'}}
    $executable=Get-ChildItem -LiteralPath $expanded -Recurse -File|Where-Object {$_.Name -eq $asset.engine}|Select-Object -First 1
    if(!$executable){throw ('Engine not found: '+$asset.engine)}
    Copy-Item -LiteralPath $executable.FullName -Destination (Join-Path $engineRoot $asset.engine) -Force
    $license=Join-Path $engineRoot "licenses/$($asset.engine)"
    New-Item -ItemType Directory -Path $license -Force | Out-Null
    Get-ChildItem -LiteralPath $expanded -Recurse -File|Where-Object {$_.Name -eq 'LICENSE' -or $_.Name -eq 'USAGE.txt'}|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $license -Force}
}
if(!$SkipNativeBuild){
    $env:CGO_ENABLED='0';$env:GOOS='darwin';$env:GOARCH=if($Rid -eq 'osx-arm64'){'arm64'}else{'amd64'}
    & $Go build -C (Join-Path $repoRoot 'windows/transports') -mod=vendor -buildvcs=false -trimpath '-ldflags=-s -w' -o (Join-Path $engineRoot 'rovunet-transports') .
    if($LASTEXITCODE -ne 0){throw 'Transport build failed'}
    & $Go build -C (Join-Path $macRoot 'native/supervisor') -buildvcs=false -trimpath '-ldflags=-s -w' -o (Join-Path $engineRoot 'rovunet-supervisor') .
    if($LASTEXITCODE -ne 0){throw 'Supervisor build failed'}
    & $Go build -C (Join-Path $macRoot 'native/slipcert') -mod=vendor -buildvcs=false -trimpath '-ldflags=-s -w' -o (Join-Path $engineRoot 'rovunet-slip-trust') .
    if($LASTEXITCODE -ne 0){throw 'Slipstream certificate discovery build failed'}
    if(!$MihomoSource){
        $MihomoSource=Join-Path $artifacts 'sources/mihomo'
        if(!(Test-Path -LiteralPath (Join-Path $MihomoSource '.git'))){& git clone --filter=blob:none --no-checkout https://github.com/MetaCubeX/mihomo.git $MihomoSource;if($LASTEXITCODE -ne 0){throw 'Mihomo source fetch failed'}}
        & git -C $MihomoSource checkout --detach 3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8
        if($LASTEXITCODE -ne 0){throw 'Pinned Mihomo source checkout failed'}
    }
    $commit=& git -C $MihomoSource rev-parse HEAD
    if($commit.Trim() -ne '3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8'){throw 'Mihomo source must match the Android engine pin'}
    $awgSource=Join-Path $MihomoSource 'cmd/rovunet-awg';New-Item -ItemType Directory -Path $awgSource -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $macRoot 'native/awg/main.go'),(Join-Path $macRoot 'native/awg/socks.go'),(Join-Path $macRoot 'native/awg/runtime_test.go') -Destination $awgSource -Force
    & $Go build -C $MihomoSource -buildvcs=false -trimpath '-ldflags=-s -w -checklinkname=0' -o (Join-Path $engineRoot 'rovunet-awg') ./cmd/rovunet-awg
    if($LASTEXITCODE -ne 0){throw 'AmneziaWG build failed'}
    & $Python (Join-Path $PSScriptRoot 'build-extra.py') --source $MihomoSource --output (Join-Path $engineRoot 'rovunet-extra') --go $Go
    if($LASTEXITCODE -ne 0){throw 'Supplemental protocol engine build failed'}
    $tunSource=Join-Path $MihomoSource 'cmd/rovunet-tun';New-Item -ItemType Directory -Path $tunSource -Force|Out-Null
    Copy-Item -LiteralPath (Get-ChildItem -LiteralPath (Join-Path $macRoot 'native/tun') -File).FullName -Destination $tunSource -Force
    & $Go build -C $MihomoSource -mod=vendor -tags with_gvisor -buildvcs=false -trimpath '-ldflags=-s -w -checklinkname=0' -o (Join-Path $engineRoot 'rovunet-tun') ./cmd/rovunet-tun
    if($LASTEXITCODE -ne 0){throw 'TUN native client/service build failed'}
    & $Python (Join-Path $PSScriptRoot 'prepare-native-extensions.py') --rid $Rid --downloads $CacheRoot
    if($LASTEXITCODE -ne 0){throw 'Native extension preparation failed'}
}
& (Join-Path $PSScriptRoot 'write-engine-manifest.ps1') -EngineRoot $engineRoot -Rid $Rid
Push-Location $macRoot
try {
    foreach($project in @('Rovunet.Desktop','Rovunet.Agent')){
        $publish=Join-Path $artifacts "publish/$Rid/$project"
        & $Dotnet publish "src/$project/$project.csproj" -c Release -r $Rid --self-contained true -p:UseAppHost=true -p:PublishTrimmed=false -p:PublishSingleFile=false -o $publish --nologo
        if($LASTEXITCODE -ne 0){throw ('Publish failed: '+$project)}
    }
}finally{Pop-Location}
$app=Join-Path $artifacts "bundles/$Rid/Rovunet.app"
# Recreate only this generated bundle, so repeated builds cannot retain obsolete
# libraries or nest the copied license/engine directories.
if(Test-Path -LiteralPath $app){
    $resolvedBundle=(Resolve-Path -LiteralPath $app).Path
    $expectedBundle=[IO.Path]::GetFullPath((Join-Path $macRoot "artifacts/bundles/$Rid/Rovunet.app"))
    if($resolvedBundle -ne $expectedBundle){throw 'Refusing to replace a bundle outside this build target'}
    Remove-Item -LiteralPath $resolvedBundle -Recurse -Force
}
$appMac=Join-Path $app 'Contents/MacOS'
$macSourceCommit=(& git -C $repoRoot rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0 -or $macSourceCommit -notmatch '^[0-9a-f]{40}$'){throw 'Cannot record source commit'}
$resources=Join-Path $app 'Contents/Resources'
New-Item -ItemType Directory -Path $appMac,$resources -Force|Out-Null
Get-ChildItem -LiteralPath (Join-Path $artifacts "publish/$Rid/Rovunet.Desktop") -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $appMac -Recurse -Force}
foreach($name in @('Rovunet.Agent','Rovunet.Agent.dll','Rovunet.Agent.deps.json','Rovunet.Agent.runtimeconfig.json')){Copy-Item -LiteralPath (Join-Path $artifacts "publish/$Rid/Rovunet.Agent/$name") -Destination $appMac -Force}
& $Python (Join-Path $PSScriptRoot 'prepare-slip-trust-licenses.py')
if($LASTEXITCODE -ne 0){throw 'Slipstream discovery licenses failed'}
$bundledEngines=Join-Path $appMac 'engines'
New-Item -ItemType Directory -Path $bundledEngines -Force|Out-Null
Get-ChildItem -LiteralPath $engineRoot -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $bundledEngines -Recurse -Force}
Copy-Item -LiteralPath (Join-Path $macRoot 'packaging/Info.plist') -Destination (Join-Path $app 'Contents/Info.plist') -Force
Copy-Item -LiteralPath (Join-Path $macRoot 'packaging/Rovunet.entitlements') -Destination $resources -Force
@{version='0.1.7-beta.1';rid=$Rid;macSourceCommit=$macSourceCommit;windowsSource='562a3d71670ceb7de909146bf68116d5cc614496';androidBinarySource='e6359aae2e2e19164ad7e9ec87267f09a0c1152d';androidSupplementalSource='5e66f167c85aecac26008d930a7a24df6f2a31f2';minimumMacOS='15.0';macRuntimeValidated=$false;appleSiliconFunctionalBaseAcceptedByOwner=$true;acceptedFunctionalBase='0.1.0-dev.6';releaseChange='Updater, About, direct public feeds, status colors and encrypted-store concurrency hardening; native engines unchanged';intelTesting='limited; host checks only';developerIdSigned=$false;notarized=$false;networkMode='system-proxy-optional; system-tun-opt-in; local per-app proxy';nativeEngineBuild='All eleven native engines reused byte-for-byte from the same-architecture dev.6 package';tunServiceVersion='0.1.0-dev.3';tunServiceCompatibility='Exact native client and policy retained for installed dev.3 service';ownerReportedDev1Launch=$true;ownerReportedDev1OwnProfileConnect=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $resources 'BUILD-INFO.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'NOTICE.md') -Destination $resources -Force
$licenses=Join-Path $resources 'licenses'
New-Item -ItemType Directory -Path $licenses -Force|Out-Null
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'windows/licenses') -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $licenses -Recurse -Force}
Get-ChildItem -LiteralPath (Join-Path $macRoot 'licenses') -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $licenses -Recurse -Force}
if(Test-Path -LiteralPath (Join-Path $macRoot 'packaging/rovunet.icns')){Copy-Item -LiteralPath (Join-Path $macRoot 'packaging/rovunet.icns') -Destination $resources -Force}
Write-Output "Cross-built $app; macOS execution/signing/notarization have NOT been performed."
