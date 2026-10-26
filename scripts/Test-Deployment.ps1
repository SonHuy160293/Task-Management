[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Config,
    [switch]$SkipHostMode,
    [switch]$SkipFailureInjection
)
# Only explicitly disposable verification stacks are accepted. No volumes are deleted.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [IO.Path]::IsPathRooted($Config)) { $Config = Join-Path $repoRoot $Config }
$Config = [IO.Path]::GetFullPath($Config)
$values = @{}
foreach ($line in [IO.File]::ReadAllLines($Config)) {
    if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim() }
}
if ($values.DEVFLOW_STACK -notmatch '^devflow-verify-[a-z0-9-]+$' -or $values.DEVFLOW_ENVIRONMENT -ne 'Development' -or $values.DEVFLOW_EMAIL_MODE -ne 'Mailpit') {
    throw 'Use a devflow-verify-* Development/Mailpit stack with disposable data and unique ports.'
}
$secretRoot = $values.DEVFLOW_SECRETS_DIR
if (-not [IO.Path]::IsPathRooted($secretRoot)) { $secretRoot = Join-Path $repoRoot $secretRoot }
$stack = $values.DEVFLOW_STACK
$log = Join-Path ([IO.Path]::GetTempPath()) ($stack + '-acceptance.log')
$negativeConfig = [IO.Path]::ChangeExtension($Config, 'negative.env')
$originalTestConfig = [Environment]::GetEnvironmentVariable('DEVFLOW_DEPLOYMENT_TEST_CONFIG','Process')
$secretStores = @{}
$hostProcess = $null

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Run-Cli([string[]]$CliArguments, [bool]$ShouldFail = $false) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'devflow.ps1') @CliArguments -Config $Config *> $log
        $result = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    Assert-True (($result -ne 0) -eq $ShouldFail) "Unexpected onboarding result; inspect $log (secret contents are not printed)."
}
function Outbox-Count {
    $result = 'SELECT count(*) FROM reliability."OutboxMessages";' | & docker exec -i ($stack + '-identity-postgres-1') psql -U identity_admin -d DevFlowIdentity -At
    Assert-True ($LASTEXITCODE -eq 0) 'Cannot count retained verification outbox records.'
    return [long]$result
}
function Check-Http([string]$Address) {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Address -TimeoutSec 5
    Assert-True ([int]$response.StatusCode -eq 200) ('Readiness failed: ' + $Address)
}
try {
    Run-Cli @('init')
    $before = @{}
    Get-ChildItem -LiteralPath $secretRoot -Recurse -File | ForEach-Object { $before[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    Run-Cli @('init')
    foreach ($path in $before.Keys) { Assert-True ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $before[$path]) 'Repeated init changed an existing secret/key.' }
    Run-Cli @('check')
    Run-Cli @('up')
    Check-Http ('http://127.0.0.1:' + $values.DEVFLOW_IDENTITY_API_PORT + '/health/ready')
    Check-Http ('http://127.0.0.1:' + $values.DEVFLOW_NOTIFICATION_API_PORT + '/health/ready')
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-GatewayDeployment.ps1') -Config $Config
    Assert-True ($LASTEXITCODE -eq 0) 'Gateway deployment, outage, and production forwarding checks failed.'
    [Environment]::SetEnvironmentVariable('DEVFLOW_DEPLOYMENT_TEST_CONFIG', $Config, 'Process')
    & dotnet test (Join-Path $repoRoot 'tests/DevFlow.Notification.Tests/DevFlow.Notification.Tests.csproj') --no-restore --filter FullyQualifiedName~DeploymentSmokeTests -m:1 -nr:false --verbosity quiet
    Assert-True ($LASTEXITCODE -eq 0) 'Deployed delivery/receipt/deduplication or role-isolation checks failed.'
    $count = Outbox-Count
    Assert-True ($count -gt 0) 'Synthetic verification did not create durable outbox data.'
    $notificationId = & docker inspect --format '{{.Id}}' ($stack + '-notification-api-1')
    $gatewayId = & docker inspect --format '{{.Id}}' ($stack + '-gateway-gateway-1')
    Run-Cli @('up','-Module','identity')
    $afterNotificationId = & docker inspect --format '{{.Id}}' ($stack + '-notification-api-1')
    Assert-True ($notificationId -eq $afterNotificationId) 'Redeploying Identity replaced Notification.'
    Assert-True ($gatewayId -eq (& docker inspect --format '{{.Id}}' ($stack + '-gateway-gateway-1'))) 'Redeploying Identity replaced Gateway.'
    Assert-True ((Outbox-Count) -ge $count) 'Repeat startup lost durable outbox data.'
    Write-Output 'PASS: repeat init/start, independent module redeploy, delivery, receipts, deduplication, and role isolation.'

    # Invalid configuration must fail without starting or replacing containers.
    $badText = [IO.File]::ReadAllText($Config) + "`nDEVFLOW_STACK=devflow-verify-duplicate`n"
    [IO.File]::WriteAllText($negativeConfig, $badText)
    $previousPreference = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'devflow.ps1') check -Config $negativeConfig *> $log; $negativeExit = $LASTEXITCODE } finally { $ErrorActionPreference = $previousPreference }
    Assert-True ($negativeExit -ne 0) 'Duplicate settings were accepted.'
    $occupied = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
    $occupied.Start()
    try {
        $port = $occupied.LocalEndpoint.Port
        $badText = [IO.File]::ReadAllText($Config) -replace '(?m)^DEVFLOW_IDENTITY_API_PORT=.*$', ('DEVFLOW_IDENTITY_API_PORT=' + $port)
        [IO.File]::WriteAllText($negativeConfig, $badText)
        $previousPreference = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        try { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'devflow.ps1') check -Module identity -Config $negativeConfig *> $log; $negativeExit = $LASTEXITCODE } finally { $ErrorActionPreference = $previousPreference }
        Assert-True ($negativeExit -ne 0) 'A port occupied by an unrelated process was accepted.'
    } finally { $occupied.Stop() }
    Write-Output 'PASS: duplicate-key and occupied-port rejection.'

    if (-not $SkipFailureInjection) {
        foreach ($relative in @('platform/broker-admin-password.secret','identity/migrator-password.secret')) {
            $path = Join-Path $secretRoot $relative
            $original = [IO.File]::ReadAllBytes($path)
            $apiId = & docker inspect --format '{{.Id}}' ($stack + '-identity-api-1')
            try {
                [IO.File]::WriteAllText($path, ('f' * 64))
                Run-Cli @('up','-Module','identity') $true
                $afterId = & docker inspect --format '{{.Id}}' ($stack + '-identity-api-1')
                Assert-True ($apiId -eq $afterId) 'A failed bootstrap/migration replaced the previously running HTTP host.'
            } finally { [IO.File]::WriteAllBytes($path,$original) }
            Run-Cli @('up','-Module','identity')
        }
        Write-Output 'PASS: broker bootstrap and migration failures block runtime replacement; original credentials recover.'
    }

    if (-not $SkipHostMode) {
        foreach ($name in @('identity-api','identity-worker','identity-migrationrunner','notification-api','notification-worker','notification-migrationrunner')) {
            $path = Join-Path ([Environment]::GetFolderPath('ApplicationData')) ('Microsoft/UserSecrets/devflow-' + $name + '/secrets.json')
            $secretStores[$path] = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
        }
        Run-Cli @('down','-Module','identity')
        Run-Cli @('up','-Module','identity','-Mode','host')
        $apiProject = Join-Path $repoRoot 'src/Modules/DevFlow.Identity/DevFlow.Identity.API'
        & dotnet build (Join-Path $apiProject 'DevFlow.Identity.API.csproj') --no-restore -m:1 -nr:false --verbosity quiet *> $log
        Assert-True ($LASTEXITCODE -eq 0) 'Host-mode application build failed.'
        $oldAsp = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','Process')
        $oldDotnet = [Environment]::GetEnvironmentVariable('DOTNET_ENVIRONMENT','Process')
        try {
            [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','Development','Process')
            [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT','Development','Process')
            $dll = Join-Path $apiProject 'bin/Debug/net10.0/DevFlow.Identity.API.dll'
            $hostProcess = Start-Process -FilePath dotnet -ArgumentList ('"' + $dll + '"') -WorkingDirectory $apiProject -PassThru -WindowStyle Hidden -RedirectStandardOutput ($log + '.host.out') -RedirectStandardError ($log + '.host.err')
            $ready = $false
            for ($attempt = 0; $attempt -lt 60; $attempt++) {
                if ($hostProcess.HasExited) { throw 'Host-mode HTTP process stopped before readiness; inspect the private verification logs.' }
                try { Check-Http ('http://127.0.0.1:' + $values.DEVFLOW_IDENTITY_API_PORT + '/health/ready'); $ready = $true; break } catch { Start-Sleep -Milliseconds 500 }
            }
            Assert-True $ready 'Host-mode HTTP readiness timed out.'
        } finally {
            if ($hostProcess -and -not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id }
            $hostProcess = $null
            [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT',$oldAsp,'Process')
            [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT',$oldDotnet,'Process')
        }
        Run-Cli @('up','-Module','identity')
        Write-Output 'PASS: IDE settings use host endpoints and the real HTTP host becomes ready.'
    }
    Run-Cli @('down')
    $remaining = @()
    foreach ($project in @('platform','identity','notification','gateway','telemetry')) {
        $remaining += @(& docker ps --all --filter ('label=com.docker.compose.project=' + $stack + '-' + $project) --format '{{.Names}}')
        Assert-True ($LASTEXITCODE -eq 0) 'Cannot inspect verification project containers.'
    }
    Assert-True ($remaining.Count -eq 0) 'Full shutdown left verification containers behind (including optional profiles).'
    Run-Cli @('up')
    Assert-True ((Outbox-Count) -ge $count) 'Stopping/restarting the deployment lost retained data.'
    Write-Output 'PASS: retained-volume restart. Disposable verification deployment is left running for inspection; down retains its volumes.'
} finally {
    if ($hostProcess -and -not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id }
    foreach ($path in $secretStores.Keys) {
        if ($null -eq $secretStores[$path]) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
        else { [IO.File]::WriteAllBytes($path,$secretStores[$path]) }
    }
    [Environment]::SetEnvironmentVariable('DEVFLOW_DEPLOYMENT_TEST_CONFIG',$originalTestConfig,'Process')
    if (Test-Path -LiteralPath $negativeConfig) { Remove-Item -LiteralPath $negativeConfig }
}
