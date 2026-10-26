param([switch]$KeepContainers)
$ErrorActionPreference = 'Stop'
$deliveryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRunId = [Guid]::NewGuid().ToString('N').Substring(0,8)
$postgresName = 'devflow-delivery-tests-' + $testRunId + '-postgres'
$rabbitName = 'devflow-delivery-tests-' + $testRunId + '-rabbitmq'
$mailpitName = 'devflow-delivery-tests-' + $testRunId + '-mailpit'
$createdContainers = @()
$generator = [Security.Cryptography.RandomNumberGenerator]::Create()
$names = @('DEVFLOW_DELIVERY_TEST_POSTGRES','DEVFLOW_DELIVERY_TEST_POSTGRES_CONTAINER','DEVFLOW_DELIVERY_TEST_RABBIT_PORT','DEVFLOW_DELIVERY_TEST_RABBIT_PASSWORD','DEVFLOW_DELIVERY_TEST_RABBIT_CONTAINER','DEVFLOW_DELIVERY_TEST_SMTP_PORT','DEVFLOW_DELIVERY_TEST_MAILPIT_URL')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
function New-TestPassword {
    $bytes = New-Object byte[] 24
    $generator.GetBytes($bytes)
    return -join ($bytes | ForEach-Object { $_.ToString('x2') })
}
function Wait-TestService([string]$container, [string[]]$command) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        docker exec $container @command *> $null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 1
    }
    throw 'Disposable integration service failed to become ready.'
}
try {
    $postgresPassword = New-TestPassword
    $rabbitPassword = New-TestPassword
    $id = docker run --detach --rm --name $postgresName -e POSTGRES_USER=devflow_test -e POSTGRES_PASSWORD=$postgresPassword -p 127.0.0.1::5432 postgres:18-alpine
    if ($LASTEXITCODE -ne 0) { throw 'Test PostgreSQL startup failed.' }
    $createdContainers += $id.Trim()
    $id = docker run --detach --rm --name $rabbitName -e RABBITMQ_DEFAULT_USER=devflow_test -e RABBITMQ_DEFAULT_PASS=$rabbitPassword -p 127.0.0.1::5672 rabbitmq:4.3.6-management@sha256:54cb013d76cd1fd6c0a0708f34062ad61c452bb6f016b434c6c33f1a765d2b58
    if ($LASTEXITCODE -ne 0) { throw 'Test broker startup failed.' }
    $createdContainers += $id.Trim()
    Wait-TestService $postgresName @('pg_isready','-U','devflow_test','-d','postgres')
    $id = docker run --detach --rm --name $mailpitName -p 127.0.0.1::1025 -p 127.0.0.1::8025 axllent/mailpit:v1.27@sha256:e22dce5b36f93c77082e204a3942fb6b283b7896e057458400a4c88344c3df68
    if ($LASTEXITCODE -ne 0) { throw 'Test Mailpit startup failed.' }
    $createdContainers += $id.Trim()
    Wait-TestService $rabbitName @('rabbitmq-diagnostics','-q','ping')
    $postgresPort = (docker port $postgresName 5432/tcp).Trim().Split(':')[-1]
    $rabbitPort = (docker port $rabbitName 5672/tcp).Trim().Split(':')[-1]
    $env:DEVFLOW_DELIVERY_TEST_POSTGRES = 'Host=127.0.0.1;Port=' + $postgresPort + ';Database=postgres;Username=devflow_test;Password=' + $postgresPassword
    $env:DEVFLOW_DELIVERY_TEST_POSTGRES_CONTAINER = $postgresName
    $env:DEVFLOW_DELIVERY_TEST_RABBIT_PORT = $rabbitPort
    $env:DEVFLOW_DELIVERY_TEST_RABBIT_PASSWORD = $rabbitPassword
    $env:DEVFLOW_DELIVERY_TEST_RABBIT_CONTAINER = $rabbitName
    $env:DEVFLOW_DELIVERY_TEST_SMTP_PORT = (docker port $mailpitName 1025/tcp).Trim().Split(':')[-1]
    $env:DEVFLOW_DELIVERY_TEST_MAILPIT_URL = 'http://127.0.0.1:' + (docker port $mailpitName 8025/tcp).Trim().Split(':')[-1] + '/'
    dotnet test (Join-Path $deliveryRoot 'tests/DevFlow.Notification.Tests/DevFlow.Notification.Tests.csproj') --nologo -m:1 --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Email delivery verification failed.' }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if (-not $KeepContainers) {
        foreach ($id in $createdContainers) { docker stop $id | Out-Null }
    }
    $generator.Dispose()
    $postgresPassword = $null
    $rabbitPassword = $null
}
