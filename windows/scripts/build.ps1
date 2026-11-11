param([string]$Output='', [string]$Dotnet='', [string]$MakeNsis='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace=[IO.Path]::GetFullPath((Join-Path $root '../..'))
if(!$Output){$Output=Join-Path $root ('artifacts/private-review17-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
if(!$Dotnet){$command=Get-Command dotnet -ErrorAction SilentlyContinue;$Dotnet=if($command){$command.Source}else{Join-Path $workspace 'tools/dotnet/dotnet.exe'}}
if(!$MakeNsis){$command=Get-Command makensis -ErrorAction SilentlyContinue;$MakeNsis=if($command){$command.Source}else{Join-Path $workspace 'tools/nsis-tauri/nsis-3.09/makensis.exe'}}
# Preserve the modern WPF installer and transactional Program Files/UAC upgrade backend.
& (Join-Path $PSScriptRoot 'package-review.ps1') -Dotnet $Dotnet -MakeNsis $MakeNsis -EngineDirectory (Join-Path $root 'engines/windows-x64') -OutputDirectory $Output
