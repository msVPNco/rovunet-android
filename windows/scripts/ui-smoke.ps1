param([Parameter(Mandatory=$true)][string]$RovunetExe,[string]$Report=(Join-Path (Get-Location) "ui-smoke-result.json"))
$ErrorActionPreference = "Stop"
if(Test-Path -LiteralPath $Report){throw "Choose a new report path; existing reports are preserved."}
$process=Start-Process -FilePath $RovunetExe -ArgumentList @("--ui-smoke",('"'+$Report+'"')) -PassThru -Wait
if($process.ExitCode -ne 0){throw "UI fixture failed. Check the report if present."}
Get-Content -LiteralPath $Report
