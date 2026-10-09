$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $root artifacts) | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$api = $null
$dashboard = $null
try {
    $api = Start-Process dotnet -ArgumentList 'Sensor3.Api/bin/Debug/net10.0/Sensor3.Api.dll','--urls','http://127.0.0.1:5301' -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$root/artifacts/api.log" -RedirectStandardError "$root/artifacts/api-error.log"
    $dashboard = Start-Process dotnet -ArgumentList 'Sensor3.Dashboard/bin/Debug/net10.0/Sensor3.Dashboard.dll','--urls','http://127.0.0.1:5302' -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$root/artifacts/dashboard.log" -RedirectStandardError "$root/artifacts/dashboard-error.log"
    for ($attempt=0; $attempt -lt 20; $attempt++) {
        try { Invoke-WebRequest http://127.0.0.1:5301/health | Out-Null; Invoke-WebRequest http://127.0.0.1:5302/health | Out-Null; break }
        catch { if ($attempt -eq 19) { throw }; Start-Sleep -Milliseconds 500 }
    }
    $info = Invoke-RestMethod http://127.0.0.1:5301/api/system/build-info
    if ($info.applicationVersion -ne '0.8.0') { throw 'API version mismatch' }
    $entries = Invoke-RestMethod http://127.0.0.1:5301/api/system/iterations
    if ($entries.Count -ne 18) { throw 'Manifest mismatch' }
    $about = Invoke-WebRequest http://127.0.0.1:5302/about
    if ($about.StatusCode -ne 200 -or $about.Content -notmatch 'Om Sensor 3' -or $about.Content -notmatch '0.8.0') { throw 'About failed' }
    $sensors = Invoke-WebRequest http://127.0.0.1:5302/sensors
    if ($sensors.StatusCode -ne 200 -or $sensors.Content -notmatch 'Webbläsaren samlar inga rörelsesensorer' -or $sensors.Content -match 'Önskad frekvens') { throw 'Browser sensor boundary failed' }
    $diagnostics = Invoke-WebRequest http://127.0.0.1:5302/diagnostics
    if ($diagnostics.StatusCode -ne 200 -or $diagnostics.Content -notmatch 'Sensordiagnostik' -or $diagnostics.Content -notmatch 'Klientversion: Unknown' -or $diagnostics.Content -match '<svg') { throw 'Diagnostics receiver boundary failed' }
    if ($info.gitCommitHash -ne 'Unknown') {
        $expected = (& git -C $root rev-parse HEAD).Trim()
        if ($expected -ne $info.gitCommitHash) { throw 'Git hash mismatch' }
        $expectedDate = [DateTimeOffset]::Parse((& git -C $root show -s --format=%cI HEAD).Trim())
        if ($expectedDate -ne [DateTimeOffset]::Parse($info.gitCommitDateUtc)) { throw 'Commit date mismatch' }
    }
    Write-Host "PASS: API + dashboard health, build-info, 18 iterations, About, Git metadata. Commit=$($info.gitCommitShortHash); dirty=$($info.isDirtyBuild)"
} finally {
    if ($api -and !$api.HasExited) { Stop-Process -Id $api.Id }
    if ($dashboard -and !$dashboard.HasExited) { Stop-Process -Id $dashboard.Id }
}

