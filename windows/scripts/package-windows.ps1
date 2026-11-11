param(
  [Parameter(Mandatory=$true)][string]$Dotnet,
  [Parameter(Mandatory=$true)][string]$MakeNsis,
  [Parameter(Mandatory=$true)][string]$EngineDirectory,
  [Parameter(Mandatory=$true)][string]$OutputDirectory,
  [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceRevision
)
$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw 'Choose a fresh output folder.' }
$Dotnet = [IO.Path]::GetFullPath($Dotnet)
$MakeNsis = [IO.Path]::GetFullPath($MakeNsis)
$EngineDirectory = [IO.Path]::GetFullPath($EngineDirectory)
if ((& $MakeNsis /VERSION).Trim() -ne 'v3.09') { throw 'Pinned NSIS 3.09 is required.' }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
# Single-file setup publishing adds ILLink dependencies. Restore with the exact
# publish properties before freezing the source/lock files, then never restore
# during publish. A source archive must describe the package that was built.
Push-Location $sourceRoot
try {
  foreach($project in @('src/Rovunet.Desktop','src/Rovunet.Agent','tests/Rovunet.WindowsSmoke')) {
    & $Dotnet restore $project -r win-x64 -p:SelfContained=true
    if($LASTEXITCODE -ne 0){throw 'Pre-snapshot restore failed.'}
  }
  & $Dotnet restore installer/Rovunet.Setup -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  if($LASTEXITCODE -ne 0){throw 'Pre-snapshot setup restore failed.'}
} finally {Pop-Location}
$commit = $SourceRevision
$revision = $commit
$sourceFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
  $relative = [IO.Path]::GetRelativePath($sourceRoot,$_.FullName)
  $relative -notmatch '(^|\\)(bin|obj|\.git|artifacts)(\\|$)' -and ($relative -notmatch '^engines\\' -or $relative -in @('engines\pins.json','engines\transports-pins.json')) -and $relative -notmatch '\.bak-' -and $relative -ne 'SOURCE-SHA256.json'
})
$sourceManifest = @(foreach ($file in $sourceFiles) { @{ path=[IO.Path]::GetRelativePath($sourceRoot,$file.FullName).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant(); bytes=$file.Length } })
$sourceManifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $outputRoot 'SOURCE-SHA256.json') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression
$sourceStream = [IO.File]::Create((Join-Path $outputRoot 'Rovunet-Windows-0.1.0-beta-source.zip'))
$sourceZip = [IO.Compression.ZipArchive]::new($sourceStream,[IO.Compression.ZipArchiveMode]::Create)
try { foreach ($file in $sourceFiles) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip,$file.FullName,'source/'+[IO.Path]::GetRelativePath($sourceRoot,$file.FullName).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal) | Out-Null } }
finally { $sourceZip.Dispose(); $sourceStream.Dispose() }
Push-Location $sourceRoot
try {
  foreach ($entry in @(@('src/Rovunet.Desktop','desktop'),@('src/Rovunet.Agent','agent'),@('tests/Rovunet.WindowsSmoke','smoke'))) {
    & $Dotnet publish $entry[0] -c Release -r win-x64 --self-contained true --no-restore -m:1 "-p:SourceRevisionId=$revision" -o (Join-Path $outputRoot $entry[1])
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
  }
  $stage = Join-Path $outputRoot 'payload'
  New-Item -ItemType Directory -Path $stage | Out-Null
  Copy-Item -Path (Join-Path $outputRoot 'desktop/*') -Destination $stage -Recurse
  foreach ($entry in @(@('agent','Rovunet.Agent'),@('smoke','Rovunet.WindowsSmoke'))) {
    foreach ($shared in @('Rovunet.Core.dll','Rovunet.Platform.dll')) {
      if ((Get-FileHash (Join-Path $outputRoot "$($entry[0])/$shared")).Hash -ne (Get-FileHash (Join-Path $stage $shared)).Hash) { throw 'Shared assembly mismatch.' }
    }
    foreach ($ext in @('.exe','.dll','.deps.json','.runtimeconfig.json')) { Copy-Item -LiteralPath (Join-Path $outputRoot "$($entry[0])/$($entry[1])$ext") -Destination $stage }
  }
  $pins = @{
    'xray.exe'='d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e'
    'rovunet-transports.exe'='891a70e6fbb044e4f61070f0583d0d30a1ef445893bf3d9b406afebd9679f7e0'
    'naive.exe'='ecf6f56a834a3e945e703ddb766b32c8c474e9767d472bb0b45177c33300aa79'
    'slipstream-client.exe'='206b43eacdae8c035176e458005117b7874d22ad82d896f01524f247bae6f43c'
  }
  New-Item -ItemType Directory -Path (Join-Path $stage 'engines') | Out-Null
  foreach ($name in $pins.Keys) {
    $engine = Join-Path $EngineDirectory $name
    if ((Get-FileHash -LiteralPath $engine).Hash -ne $pins[$name]) { throw "Engine hash mismatch: $name" }
    Copy-Item -LiteralPath $engine -Destination (Join-Path $stage 'engines')
  }
  foreach ($folder in @('docs','licenses')) { Copy-Item -LiteralPath $folder -Destination $stage -Recurse }
  $bankStage = Join-Path $stage 'data/dns'
  New-Item -ItemType Directory -Path $bankStage -Force | Out-Null
  $catalog = @(foreach($name in @('resolvers.txt','ir_dns_lite.cidr','ir_dns.cidr','ir.cidr','ru.cidr','cn.cidr')) {
    $file=Join-Path $sourceRoot "transports/resolverdata/$name"
    $packaged=Join-Path $bankStage $name
    if($name.EndsWith('.cidr')) {
      $canonical=@(foreach($line in Get-Content -LiteralPath $file) {
        $value=$line.Split('#')[0].Trim()
        if(!$value){$line;continue}
        $parts=$value.Split('/');$ip=[Net.IPAddress]::Parse($parts[0]);$prefix=[int]$parts[1]
        if($ip.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $prefix -lt 0 -or $prefix -gt 32){throw 'Invalid packaged bank prefix'}
        $octets=$ip.GetAddressBytes();$number=[uint64]$octets[0]*16777216+[uint64]$octets[1]*65536+[uint64]$octets[2]*256+$octets[3]
        $network=$number -band (4294967295-[uint64]([math]::Pow(2,32-$prefix)-1))
        $address=[Net.IPAddress]::new([byte[]]@((($network -shr 24)-band 255),(($network -shr 16)-band 255),(($network -shr 8)-band 255),($network-band 255)))
        $address.ToString()+'/'+$prefix
      })
      $canonical|Set-Content -LiteralPath $packaged -Encoding utf8
    } else { Copy-Item -LiteralPath $file -Destination $bankStage }
    @{ file=$name;source_sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant();sha256=(Get-FileHash -LiteralPath $packaged).Hash.ToLowerInvariant();bytes=(Get-Item -LiteralPath $packaged).Length;records=@(Get-Content -LiteralPath $packaged | Where-Object { $_.Trim().Length -gt 0 -and !$_.TrimStart().StartsWith('#') }).Count;canonical_cidrs=$name.EndsWith('.cidr') }
  })
  $catalog | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $bankStage 'CATALOG-SHA256.json') -Encoding utf8
  Copy-Item -LiteralPath 'LICENSE','NOTICE.md' -Destination $stage
  Set-Content -LiteralPath (Join-Path $stage 'review-install.marker') -Value 'Rovunet-Windows' -NoNewline
  Copy-Item -LiteralPath (Join-Path $outputRoot 'SOURCE-SHA256.json') -Destination $stage
  $provenance = @{ version='0.1.0-beta'; platform='windows-x64'; source_commit=$commit; source_origin='git-windows-source'; source_manifest_sha256=(Get-FileHash (Join-Path $outputRoot 'SOURCE-SHA256.json')).Hash.ToLowerInvariant(); signed=$false; published=$false; rebuilt_native_engines=@(); reused_pinned_engines=@('rovunet-transports.exe','xray.exe','naive.exe','slipstream-client.exe'); native_engine_rebuild_stage=20; native_engine_rebuild_review=20; native_go_version='go1.27.3 windows/amd64'; sdk=(& $Dotnet --version); compiler_sha256=(Get-FileHash $MakeNsis).Hash }
  $provenance | ConvertTo-Json | Set-Content (Join-Path $stage 'BUILD-PROVENANCE.json')
  $manifest = Join-Path $outputRoot 'delete-manifest.nsh'
  $lines = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Where-Object { $_.FullName -notin @((Join-Path $stage 'Rovunet.exe'), (Join-Path $stage 'review-install.marker')) } | ForEach-Object {
    $relative = $_.FullName.Substring($stage.Length+1)
    'IfFileExists "$INSTDIR\' + $relative + '" 0 +3'
    'Delete "$INSTDIR\' + $relative + '"'
    'IfErrors refused'
  })
  $lines += @(Get-ChildItem -LiteralPath $stage -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { 'RMDir "$INSTDIR\' + $_.FullName.Substring($stage.Length+1) + '"' })
  $lines | Set-Content -LiteralPath $manifest -Encoding utf8
  $backend = Join-Path $outputRoot 'review-backend.exe'
  $preflight = Join-Path $outputRoot 'preflight-manifest.nsh'
  $index = 0
  $guards = @(Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
    $index++
    $relative = $_.FullName.Substring($stage.Length+1)
    'IfFileExists "$INSTDIR\' + $relative + '" 0 preflight_' + $index
    'System::Call ''kernel32::CreateFileW(w "$INSTDIR\' + $relative + '", i 0x40000000, i 0, p 0, i 3, i 128, p 0)p.r0'''
    'StrCmp $0 -1 refused'
    'System::Call ''kernel32::CloseHandle(p r0)'''
    'preflight_' + $index + ':'
  })
  $guards | Set-Content -LiteralPath $preflight -Encoding utf8
  & $MakeNsis /V2 "/DSTAGING=$stage" "/DOUTPUT=$backend" "/DDELETE_MANIFEST=$manifest" "/DPREFLIGHT_MANIFEST=$preflight" 'installer/ReviewBackend.nsi'
  if ($LASTEXITCODE -ne 0) { throw 'NSIS failed.' }
  & $Dotnet publish installer/Rovunet.Setup -c Release -r win-x64 --self-contained true --no-restore -m:1 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:SourceRevisionId=$revision" "-p:BackendPath=$backend" -o (Join-Path $outputRoot 'setup')
  if ($LASTEXITCODE -ne 0) { throw 'Setup publish failed.' }
  Get-FileHash -LiteralPath (Join-Path $outputRoot 'setup/Rovunet.Setup.exe') | Select-Object Algorithm,Hash | ConvertTo-Json | Set-Content (Join-Path $outputRoot 'setup-sha256.json')
  foreach($file in $sourceManifest){if((Get-FileHash -LiteralPath (Join-Path $sourceRoot $file.path)).Hash.ToLowerInvariant() -ne $file.sha256){throw ('Source changed after snapshot: '+$file.path)}}
} finally { Pop-Location }


