param([Parameter(Mandatory=$true)][string]$Go,[Parameter(Mandatory=$true)][string]$Git,[Parameter(Mandatory=$true)][string]$BuildRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$build=[IO.Path]::GetFullPath($BuildRoot)
if(Test-Path -LiteralPath $build){throw 'Choose a fresh AmneziaWG build folder'}
if((& $Go version) -notmatch 'go1\.27\.3 windows/amd64'){throw 'Pinned Go 1.27.3 Windows amd64 is required'}
New-Item -ItemType Directory -Path $build | Out-Null
$pin='3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8'
$source=Join-Path $build 'mihomo'
& $Git clone --config core.autocrlf=false --filter=blob:none --no-checkout https://github.com/MetaCubeX/mihomo.git $source
if($LASTEXITCODE -ne 0){throw 'Pinned engine fetch failed'}
& $Git -C $source checkout --detach $pin
if($LASTEXITCODE -ne 0 -or (& $Git -C $source rev-parse HEAD).Trim() -ne $pin){throw 'Engine source identity mismatch'}
& $Git -C $source apply --check (Join-Path $root 'native-patches/mihomo/6780eeeb.patch')
if($LASTEXITCODE -ne 0){throw 'Reviewed SOCKS backport does not match pinned source'}
& $Git -C $source apply (Join-Path $root 'native-patches/mihomo/6780eeeb.patch')
if($LASTEXITCODE -ne 0){throw 'Reviewed SOCKS backport failed'}
$adapter=Join-Path $source 'cmd/rovunet-awg'
New-Item -ItemType Directory -Path $adapter -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'native/awg') -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $adapter}
$env:CGO_ENABLED='0';$env:GOOS='windows';$env:GOARCH='amd64'
$binary=Join-Path $build 'rovunet-awg.exe'
& $Go build -C $source -buildvcs=false -trimpath '-ldflags=-s -w' -o $binary ./cmd/rovunet-awg
if($LASTEXITCODE -ne 0){throw 'AmneziaWG build failed'}
$hash=(Get-FileHash -LiteralPath $binary).Hash.ToLowerInvariant()
$runtime=Get-Content -LiteralPath (Join-Path $root 'src/Rovunet.Agent/Runtime.cs') -Raw
$expected=[regex]::Match($runtime,'AmneziaEngineSha256 = "([a-f0-9]{64})"').Groups[1].Value
if($hash -ne $expected){throw 'Native output differs from reviewed runtime pin; review source/toolchain before updating pin'}
@{source=$pin;upstreamBackport='6780eeeb27c7eda5e79a6310a4f7f91ba7cb725d';go=(& $Go version);sha256=$hash;adapter='native/awg';cgo=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $build 'PROVENANCE.json') -Encoding utf8
Write-Output $binary
