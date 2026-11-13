param(
  [Parameter(Mandatory=$true)][string]$Dotnet,
  [Parameter(Mandatory=$true)][string]$MakeNsis,
  [Parameter(Mandatory=$true)][string]$EngineDirectory,
  [Parameter(Mandatory=$true)][string]$OutputDirectory,
  [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceRevision
)
# Kept for existing developer bookmarks; use the current pinned packager.
& (Join-Path $PSScriptRoot 'package-windows.ps1') @PSBoundParameters
