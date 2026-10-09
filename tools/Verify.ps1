$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    dotnet build Sensor3.sln --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet test Sensor3.Tests/Sensor3.Tests.csproj --no-build --logger 'trx;LogFileName=iteration-tests.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & "$PSScriptRoot/Smoke-Test.ps1"
    & "$PSScriptRoot/Distribution-Smoke-Test.ps1"
    Write-Host 'Automatiska kontroller godkända. Manifeststatus ändras inte: manuella native-kontroller krävs också.'
} finally { Pop-Location }
