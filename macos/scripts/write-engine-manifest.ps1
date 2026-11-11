param([Parameter(Mandatory=$true)][string]$EngineRoot,[Parameter(Mandatory=$true)][string]$Rid)
$ErrorActionPreference='Stop'
$executables=@{}
$names=@{'rovunet-extra'='rovunet-extra';'rovunet-slip-trust'='rovunet-slip-trust';'xray'='xray';'naive'='naive';'rovunet-transports'='rovunet-transports';'rovunet-supervisor'='rovunet-supervisor';'slipstream-client'='slipstream-client';'rovunet-awg'='rovunet-awg';'rovunet-tun'='rovunet-tun';'tor'='tor/tor';'lyrebird'='tor/pluggable_transports/lyrebird'}
foreach($name in $names.Keys){$file=Join-Path $EngineRoot $names[$name];if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw ('Engine missing: '+$name)};$executables[$name]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}
$files=@{}
foreach($name in @('lib/libssl.3.dylib','lib/libcrypto.3.dylib','tor/libevent-2.1.7.dylib','data/geoip','data/geoip6','data/pt_config.json')){ $file=Join-Path $EngineRoot $name;if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw ('Engine dependency missing: '+$name)};$files[$name]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
@{rid=$Rid;executables=$executables;files=$files}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $EngineRoot 'engine-manifest.json') -Encoding utf8
