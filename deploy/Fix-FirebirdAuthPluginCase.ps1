#requires -Version 5.1
#requires -RunAsAdministrator
<#
.SYNOPSIS
Finds the selected local Firebird installation, fixes SRP plugin capitalization,
and backs up changed files. Does not restart or stop any service.
.EXAMPLE
.\Fix-FirebirdAuthPluginCase.ps1 -WhatIf
.EXAMPLE
.\Fix-FirebirdAuthPluginCase.ps1
.EXAMPLE
.\Fix-FirebirdAuthPluginCase.ps1 -ServiceName FirebirdServerDefaultInstance
.NOTES
Run ON THE NOVA SERVER. Arrange a service restart separately when appropriate.
No database, password, plugin order, or encryption setting is changed.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$ServiceName,
    [string]$ConfigDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-AuthPluginCase([string]$Text) {
    # Edit active AuthServer assignments only; retain comments and line endings.
    [regex]::Replace($Text, '(?im)^([\t ]*AuthServer[\t ]*=[\t ]*)([^#\r\n]*)', [System.Text.RegularExpressions.MatchEvaluator]{
        param($line)
        $plugins = [regex]::Replace($line.Groups[2].Value, '(?i)(?<![\w])srp(?:256)?(?![\w])', [System.Text.RegularExpressions.MatchEvaluator]{
            param($plugin)
            if ($plugin.Value -ieq 'srp256') { 'Srp256' } else { 'Srp' }
        })
        $line.Groups[1].Value + $plugins
    })
}

$services = @(Get-CimInstance Win32_Service | Where-Object {
    $_.PathName -match '(?i)(?:firebird|fbserver|fbguard)\.exe(?:"|\s|$)'
})
if ($ServiceName) {
    $candidates = @($services | Where-Object Name -EQ $ServiceName)
} else {
    # Prefer the database server over its optional watchdog service.
    $candidates = @($services | Where-Object { $_.PathName -notmatch '(?i)fbguard\.exe' })
    if ($candidates.Count -eq 0) { $candidates = $services }
}
if ($candidates.Count -ne 1) {
    $services | Select-Object Name, State, PathName | Format-Table -AutoSize | Out-Host
    throw 'Could not select one Firebird service. Run on the Nova server and pass -ServiceName with the correct service name.'
}
$service = $candidates[0]
if ($service.PathName -match '^\s*"([^"]+\.exe)"') { $executable = $Matches[1] }
elseif ($service.PathName -match '^\s*(.+?\.exe)(?:\s|$)') { $executable = $Matches[1] }
else { throw 'Could not determine the Firebird executable path.' }
$executable = [Environment]::ExpandEnvironmentVariables($executable)
$binaryDirectory = Split-Path -Parent $executable
if ($ConfigDirectory) {
    $configRoot = (Resolve-Path -LiteralPath $ConfigDirectory).Path
} else {
    $roots = @($binaryDirectory, (Split-Path -Parent $binaryDirectory)) | Select-Object -Unique
    $roots = @($roots | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'firebird.conf') -PathType Leaf })
    if ($roots.Count -ne 1) { throw 'Could not locate one firebird.conf beside the service executable. Supply -ConfigDirectory for this service installation.' }
    $configRoot = $roots[0]
}
if (!(Test-Path -LiteralPath (Join-Path $configRoot 'firebird.conf') -PathType Leaf)) {
    throw "No firebird.conf found in $configRoot"
}
Write-Host "Server: $env:COMPUTERNAME"
Write-Host "Service: $($service.Name) [$($service.State)]"
Write-Host "Configuration directory: $configRoot"

$changes = @()
foreach ($name in @('firebird.conf', 'databases.conf')) {
    $path = Join-Path $configRoot $name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $bytes = [IO.File]::ReadAllBytes($path)
    # Byte-preserving handling of UTF-8/ANSI files, plus BOM-marked UTF-16/32.
    $encoding = [Text.Encoding]::GetEncoding(28591)
    $offset = 0
    if ($bytes.Length -ge 4 -and $bytes[0] -eq 255 -and $bytes[1] -eq 254 -and $bytes[2] -eq 0 -and $bytes[3] -eq 0) { $encoding = [Text.Encoding]::UTF32; $offset = 4 }
    elseif ($bytes.Length -ge 4 -and $bytes[0] -eq 0 -and $bytes[1] -eq 0 -and $bytes[2] -eq 254 -and $bytes[3] -eq 255) { $encoding = [Text.UTF32Encoding]::new($true,$true); $offset = 4 }
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 255 -and $bytes[1] -eq 254) { $encoding = [Text.Encoding]::Unicode; $offset = 2 }
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 254 -and $bytes[1] -eq 255) { $encoding = [Text.Encoding]::BigEndianUnicode; $offset = 2 }
    elseif ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { $offset = 3 }
    $original = $encoding.GetString($bytes,$offset,$bytes.Length-$offset)
    Write-Host "Checking: $path"
    foreach ($match in [regex]::Matches($original,'(?im)^[\t ]*AuthServer[\t ]*=[^\r\n]*')) { Write-Host "  $($match.Value.Trim())" }
    if ($original -match '(?im)^\s*include\s+') { Write-Warning "$name contains include directives. Any included configuration files must also be checked." }
    $updated = Convert-AuthPluginCase $original
    if ($updated -cne $original) {
        [byte[]]$prefix = @()
        if ($offset -gt 0) { $prefix = $bytes[0..($offset-1)] }
        $changes += [pscustomobject]@{ Path=$path; Original=$bytes; Updated=[byte[]]($prefix + $encoding.GetBytes($updated)); Backup=$null }
    }
}
if ($changes.Count -eq 0) {
    Write-Host 'No incorrect Srp/Srp256 capitalization found in these files. No files changed and no service restarted.'
    return
}
if (!$PSCmdlet.ShouldProcess($configRoot, "Back up and correct $($changes.Count) configuration file(s); leave services running")) { return }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-ffff'
try {
    # Back up every file before changing any file.
    foreach ($change in $changes) {
        $change.Backup = "$($change.Path).$stamp.bak"
        Copy-Item -LiteralPath $change.Path -Destination $change.Backup -ErrorAction Stop
        Write-Host "Backup: $($change.Backup)"
    }
    foreach ($change in $changes) {
        [IO.File]::WriteAllBytes($change.Path,$change.Updated)
        Write-Host "Updated: $($change.Path)"
    }
} catch {
    foreach ($change in $changes) {
        if ($change.Backup -and (Test-Path -LiteralPath $change.Backup)) { [IO.File]::WriteAllBytes($change.Path,$change.Original) }
    }
    throw
}
Write-Host 'Configuration updated. No service was stopped or restarted.' -ForegroundColor Green
Write-Host 'Arrange a Firebird restart separately when ready, then test the Nova connection.'
