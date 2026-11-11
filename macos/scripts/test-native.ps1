param([string]$Go='go',[string]$Dotnet='dotnet',[string]$Output='')
$ErrorActionPreference='Stop'
$macRoot=Split-Path $PSScriptRoot -Parent
$repoRoot=Split-Path $macRoot -Parent
if(!$Output){$Output=Join-Path $macRoot 'artifacts/native-tests'}
New-Item -ItemType Directory -Path $Output -Force|Out-Null
$transport=Join-Path $repoRoot 'windows/transports'
$original=Join-Path $transport 'resolver_scan_test.go'
$fixture=(Get-Content -LiteralPath $original -Raw).Replace("`r`n","`n")
# The upstream fixture used an empty root name as its NS RDATA and then required
# the NS validity check to pass. Use a valid ns1.example.invalid wire name.
# Preserve every assertion, timeout, independent TCP/UDP criterion and production file.
$bad='Data: []byte{0}'
$good='Data: []byte{3, 110, 115, 49, 7, 101, 120, 97, 109, 112, 108, 101, 7, 105, 110, 118, 97, 108, 105, 100, 0}'
if(!$fixture.Contains($bad)){throw 'Fixture source changed; review overlay before running'}
$fixture=$fixture.Replace($bad,$good)
$nsPattern='(?s)(if req\.Type == 2 \{\s*reply\.Answer = .*?\n\s*\})'
if([regex]::Matches($fixture,$nsPattern).Count -ne 1){throw 'Review the single NS fixture before applying the A-glue response'}
$glue=@'

            if req.Type == 1 && req.Name.String() == "ns1.example.invalid" {
                reply.Answer = []wire.RR{{Name: req.Name, Class: 1, Type: 1, Data: []byte{127, 0, 0, 1}}}
            }
'@
$fixture=[regex]::Replace($fixture,$nsPattern,('$1'+$glue))
$fixed=Join-Path $Output 'resolver_scan_test.go'
[System.IO.File]::WriteAllText($fixed,$fixture,[System.Text.UTF8Encoding]::new($false))
$mapping=@{Replace=@{}}
$mapping.Replace[$original]=$fixed
$overlay=Join-Path $Output 'overlay.json'
$mapping|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $overlay -Encoding utf8
$env:ROVUNET_DOTNET=$Dotnet
$env:ROVUNET_DOMAIN_TEST_DLL=Join-Path $macRoot 'tests/Rovunet.SharedRegression/bin/Release/net10.0/Rovunet.SharedRegression.dll'
$names=& $Go test -C $transport -mod=vendor -list '^Test' .
if($LASTEXITCODE -ne 0){throw 'Native test discovery failed'}
$excludedIntegration=@('TestGlobalResolverActualCoreOverrideHTTPS','TestWpfDnsPoolVerifiedPublicHTTPS','TestWpfRemainingResolverFamilies')
$selected=$names|Where-Object {$_ -match '^Test\w+$' -and $_ -notin $excludedIntegration}
$pattern='^('+($selected -join '|')+')$'
& $Go test -C $transport -mod=vendor "-overlay=$overlay" -count=1 -json -run $pattern . > (Join-Path $Output 'native-tests.jsonl')
$exit=$LASTEXITCODE
@{exit=$exit;productionSourceModified=$false;fixtureCorrection='Valid NS RDATA and A glue; all original assertions retained';excludedIntegrationFixtures=$excludedIntegration;separateCoreCase='Global resolver integration is a separate suite, not a WPF test. See its independent result and isolated rerun.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Output 'scope.json') -Encoding utf8
if($exit -ne 0){throw 'Native tests failed; inspect native-tests.jsonl'}
