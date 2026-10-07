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

# Build one project at a time, then run without rebuilding: two concurrent `dotnet run` builds race on
# the shared Aurora.Contracts output and one of them fails with a locked file.
$projects = @(
    @{ Path = 'src/Aurora.Api'; Port = 7077; Name = 'api' },
    @{ Path = 'src/Aurora.Client'; Port = 7259; Name = 'client' }
) | Where-Object { -not (Get-NetTCPConnection -LocalPort $_.Port -State Listen -ErrorAction SilentlyContinue) }
foreach ($project in $projects) {
    $buildLog = Join-Path $localRoot "$($project.Name)-build.log"
    dotnet build (Join-Path $repoRoot $project.Path) --nologo *> $buildLog
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $($project.Path). See $buildLog" }
}
foreach ($project in $projects) {
    Start-Process -FilePath 'dotnet' -WorkingDirectory $repoRoot -ArgumentList "run --project $($project.Path) --launch-profile https --no-build" -WindowStyle Hidden -RedirectStandardOutput (Join-Path $localRoot "$($project.Name).log") -RedirectStandardError (Join-Path $localRoot "$($project.Name)-errors.log")
}
Write-Output 'Aurora: https://localhost:7259/aurora/orders'
Write-Output 'API readiness: https://localhost:7077/health/ready'
Write-Output "Allow a few seconds for startup. Logs: $localRoot"
