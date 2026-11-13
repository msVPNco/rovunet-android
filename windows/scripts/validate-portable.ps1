param([Parameter(Mandatory=$true)][string]$Dotnet,[Parameter(Mandatory=$true)][string]$FreshEvidence)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));$evidence=[IO.Path]::GetFullPath($FreshEvidence)
if(Test-Path -LiteralPath $evidence){throw 'Use fresh evidence; do not mix binaries/reports from previous runs'}
[IO.Directory]::CreateDirectory($evidence)|Out-Null
Push-Location $root
try{
 foreach($project in @('tests/Rovunet.Tests','tests/Rovunet.AmneziaTests','tests/Rovunet.Protocol25Tests','prototypes/NetworkBroker28')){
  $name=Split-Path $project -Leaf
  & $Dotnet restore $project --locked-mode *> (Join-Path $evidence ($name+'-restore.txt'))
  if($LASTEXITCODE -ne 0){throw ('Locked restore failed: '+$name)}
  & $Dotnet run --project $project -c Release --no-restore *> (Join-Path $evidence ($name+'.txt'))
  if($LASTEXITCODE -ne 0){throw ('Validation failed: '+$name)}
  Get-Content -LiteralPath (Join-Path $evidence ($name+'.txt')) -Tail 1
 }
}finally{Pop-Location}
