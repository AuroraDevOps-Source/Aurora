$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$localRoot = Join-Path $repoRoot 'deploy\private\local-test'
$pgData = Join-Path $localRoot 'pgdata'
$pgBin = 'C:\Program Files\PostgreSQL\18\bin'
if (-not (Test-Path -LiteralPath (Join-Path $pgData 'PG_VERSION'))) {
    throw 'The local test database has not been restored.'
}

& (Join-Path $pgBin 'pg_ctl.exe') -D $pgData status *> $null
if ($LASTEXITCODE -ne 0) {
    $pgLog = Join-Path $localRoot 'postgres.log'
    Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList "-D `"$pgData`" -l `"$pgLog`" -o `"-h 127.0.0.1 -p 55432`" -w start" -WindowStyle Hidden
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        & (Join-Path $pgBin 'pg_isready.exe') -h 127.0.0.1 -p 55432 *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL did not start. See $pgLog" }
}

# Local work runs only against the restored copy; refuse to start if user secrets point anywhere else.
foreach ($project in 'src/Aurora.Api', 'src/Aurora.Migrations') {
    $setting = dotnet user-secrets list --project (Join-Path $repoRoot $project) | Where-Object { $_ -like 'ConnectionStrings:Aurora = *' }
    if (-not ($setting -match '(?i)Host=127\.0\.0\.1;' -and $setting -match '(?i)Port=55432;' -and $setting -match '(?i)Database=aurora_test;')) {
        throw "$project is not configured for the local test copy (127.0.0.1:55432/aurora_test). Run deploy\private\local-test\configure_local.py."
    }
}

function Start-AuroraProject($project, $port, $name) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { return }
    Start-Process -FilePath 'dotnet' -WorkingDirectory $repoRoot -ArgumentList "run --project $project --launch-profile https" -WindowStyle Hidden -RedirectStandardOutput (Join-Path $localRoot "$name.log") -RedirectStandardError (Join-Path $localRoot "$name-errors.log")
}
Start-AuroraProject 'src/Aurora.Api' 7077 'api'
Start-AuroraProject 'src/Aurora.Client' 7259 'client'
Write-Output 'Aurora: https://localhost:7259/aurora/orders'
Write-Output 'API readiness: https://localhost:7077/health/ready'
Write-Output "Allow a few seconds for startup. Logs: $localRoot"
