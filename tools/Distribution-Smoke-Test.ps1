# Real debug-signed APK and ephemeral credentials/manifest key; never publishes to the real catalogue.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('sensor3-http-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
New-Item -ItemType Directory -Path "$root/artifacts" -Force | Out-Null
$settings = @('ASPNETCORE_ENVIRONMENT','Distribution__StorageRoot','Distribution__AdminUsername','Distribution__AdminPasswordHash','Distribution__AndroidApkSignerPath','Distribution__AndroidAaptPath','Distribution__AndroidCertificateSha256','Distribution__JavaPath','Distribution__ManifestSigningKeyPath')
$previous = @{}
foreach ($name in $settings) { $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
$api = $null; $dashboard = $null; $checks = 0
function Check($condition, $name) { if (!$condition) { throw "FAIL: $name" }; $script:checks++ }
function Request($path, $method = 'GET', $session = $null, $body = $null, $form = $null) {
    $parameters = @{ Uri = "http://127.0.0.1:5304$path"; Method = $method; SkipHttpErrorCheck = $true; MaximumRedirection = 0; ErrorAction = 'SilentlyContinue' }
    if ($session) { $parameters.WebSession = $session }
    if ($null -ne $body) { $parameters.Body = $body }
    if ($null -ne $form) { $parameters.Form = $form }
    Invoke-WebRequest @parameters
}
function Token($html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (!$match.Success) { throw 'CSRF token saknas' }
    [Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Distribution__StorageRoot = Join-Path $testRoot 'store'
    $env:Distribution__AdminUsername = 'temporary-test-admin'
    $sdkRoot = if ($env:Sensor3TestAndroidSdk) { $env:Sensor3TestAndroidSdk } else { Join-Path $env:LOCALAPPDATA 'Android/Sdk' }
    $sdk = Join-Path $sdkRoot 'build-tools/36.0.0'
    $env:Distribution__AndroidApkSignerPath = Join-Path $sdk 'lib/apksigner.jar'
    $env:Distribution__AndroidAaptPath = Join-Path $sdk 'aapt.exe'
    $javaRoot = if ($env:Sensor3TestJava) { $env:Sensor3TestJava } else { Join-Path $env:LOCALAPPDATA 'Microsoft/Jdk' }
    $env:Distribution__JavaPath = Join-Path $javaRoot 'bin/java.exe'
    $package = Join-Path $root 'Sensor3.Mobile/bin/Debug/net10.0-android/se.qsys.sensor3-Signed.apk'
    if (!(Test-Path -LiteralPath $package)) { throw 'Build Android debug APK before running this check.' }
    $signature = & $env:Distribution__JavaPath -jar $env:Distribution__AndroidApkSignerPath verify --print-certs $package
    if ($LASTEXITCODE -ne 0) { throw 'Debug APK signature failed verification.' }
    $env:Distribution__AndroidCertificateSha256 = [regex]::Match(($signature -join "`n"),'Signer #1 certificate SHA-256 digest: ([0-9a-fA-F]{64})').Groups[1].Value
    $signer = [Security.Cryptography.RSA]::Create(2048)
    $env:Distribution__ManifestSigningKeyPath = Join-Path $testRoot 'manifest-key.pem'
    [IO.File]::WriteAllText($env:Distribution__ManifestSigningKeyPath,$signer.ExportPkcs8PrivateKeyPem())
    $password = [Guid]::NewGuid().ToString('N')
    $salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $derived = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($password, $salt, 210000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    $env:Distribution__AdminPasswordHash = 'pbkdf2-sha256:210000:' + [Convert]::ToBase64String($salt) + ':' + [Convert]::ToBase64String($derived)
    $api = Start-Process dotnet -ArgumentList 'Sensor3.Api/bin/Debug/net10.0/Sensor3.Api.dll','--urls','http://127.0.0.1:5303' -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$root/artifacts/distribution-api.log" -RedirectStandardError "$root/artifacts/distribution-api-error.log"
    $dashboard = Start-Process dotnet -ArgumentList 'Sensor3.Dashboard/bin/Debug/net10.0/Sensor3.Dashboard.dll','--urls','http://127.0.0.1:5304' -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$root/artifacts/distribution-dashboard.log" -RedirectStandardError "$root/artifacts/distribution-dashboard-error.log"
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { Invoke-WebRequest http://127.0.0.1:5303/health | Out-Null; Invoke-WebRequest http://127.0.0.1:5304/health | Out-Null; break }
        catch { if ($attempt -eq 29) { throw }; Start-Sleep -Milliseconds 500 }
    }
    Check ((Request '/download?platform=Android&channel=Stable').Content -match 'Ingen kompatibel version') 'Empty Android portal'
    Check ((Request '/download?platform=Windows&channel=Beta').Content -match 'Windows-klient') 'Manual Windows selection'
    Check ((Request '/api/releases/latest?platform=Android&channel=Stable').StatusCode -eq 404) 'No invented release'
    Check ((Request '/api/releases?platform=Invalid').StatusCode -eq 400) 'Invalid platform'
    Check ((Request '/api/admin/releases').StatusCode -eq 401) 'Anonymous admin API denied'
    Check ((Request '/admin/releases').StatusCode -eq 302) 'Anonymous admin page denied'
    Check ((Request '/admin/releases/publish' 'POST').StatusCode -ne 200) 'Anonymous publication denied'
    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $login = Request '/admin/login' 'GET' $session
    $csrf = Token $login.Content
    Check ((Request '/admin/login' 'POST' $session @{username=$env:Distribution__AdminUsername;password=$password}).StatusCode -eq 400) 'Login requires CSRF'
    Check ((Request '/admin/login' 'POST' $session @{username=$env:Distribution__AdminUsername;password='wrong';__RequestVerificationToken=$csrf}).StatusCode -eq 401) 'Wrong password denied'
    Check ((Request '/admin/login' 'POST' $session @{username=$env:Distribution__AdminUsername;password=$password;__RequestVerificationToken=$csrf}).StatusCode -eq 302) 'Admin login'
    $admin = Request '/admin/releases' 'GET' $session
    Check ($admin.StatusCode -eq 200 -and $admin.Content -match 'Release-manifest') 'Authenticated administration'
    $csrf = Token $admin.Content
    Check ((Request '/admin/releases/publish' 'POST' $session).StatusCode -eq 400) 'Publication requires CSRF'
    $manifest = @{
        platform='Android'; channel='Development'; version='0.4.0'; buildNumber=4; iterationNumber=1
        gitCommitHash=(& git -C $root rev-parse HEAD).Trim(); gitCommitDateUtc=(& git -C $root show -s --format=%cI HEAD).Trim()
        releaseNotes='Debug-signed HTTP test fixture; not a production release'; expectedSha256=(Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant()
        compatibility=@{minimumServerVersion='0.2.0'}; updatePolicy=@{mandatory=$true}
    } | ConvertTo-Json -Depth 4
    $form = @{manifest=$manifest;artifact=Get-Item $package;__RequestVerificationToken=$csrf}
    $wrongVersion = $manifest | ConvertFrom-Json
    $wrongVersion.buildNumber = 5
    Check ((Request '/admin/releases/publish' 'POST' $session $null @{manifest=($wrongVersion | ConvertTo-Json -Depth 4);artifact=Get-Item $package;__RequestVerificationToken=$csrf}).StatusCode -eq 400) 'Embedded version mismatch denied'
    $unsigned = Join-Path $testRoot 'unsigned.apk'
    $archive = [IO.Compression.ZipFile]::Open($unsigned,[IO.Compression.ZipArchiveMode]::Create)
    try { $writer=[IO.StreamWriter]::new($archive.CreateEntry('AndroidManifest.xml').Open()); $writer.Write('Unsigned negative fixture'); $writer.Dispose() } finally { $archive.Dispose() }
    $unsignedManifest = $manifest | ConvertFrom-Json
    $unsignedManifest.expectedSha256 = (Get-FileHash $unsigned -Algorithm SHA256).Hash.ToLowerInvariant()
    Check ((Request '/admin/releases/publish' 'POST' $session $null @{manifest=($unsignedManifest | ConvertTo-Json -Depth 4);artifact=Get-Item $unsigned;__RequestVerificationToken=$csrf}).StatusCode -eq 400) 'Unsigned APK denied'
    Check ((Request '/admin/releases/publish' 'POST' $session $null $form).StatusCode -eq 302) 'Publish signature-verified debug APK to temporary catalogue'
    $latest = Invoke-RestMethod 'http://127.0.0.1:5303/api/releases/latest?platform=Android&channel=Development'
    Check ($latest.manifest.version -eq '0.4.0') 'Shared API catalogue'
    $nonce = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $signed = Invoke-RestMethod "http://127.0.0.1:5303/api/updates/authenticated?platform=Android&channel=Development&version=0.1.0&buildNumber=1&nonce=$nonce"
    $payload = [Convert]::FromBase64String($signed.payload)
    Check ($signer.VerifyData($payload,[Convert]::FromBase64String($signed.signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)) 'Authenticated update signature'
    $authenticated = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    Check ($authenticated.nonce -eq $nonce -and $authenticated.result.release.id -eq $latest.id) 'Authenticated selection bound to nonce'
    Check ((Request '/api/updates/authenticated?nonce=bad').StatusCode -eq 400) 'Invalid nonce denied'
    Check ((Request '/api/releases/Sensor3.appinstaller?channel=Stable').StatusCode -eq 404) 'No invented App Installer package'
    Check ((Request '/download?platform=Android&channel=Development').Content -match $latest.artifact.sha256) 'Portal shows real checksum'
    $download = Request "/api/releases/$($latest.id)/download"
    Check ($download.StatusCode -eq 200 -and $download.Headers['X-Content-Type-Options'] -contains 'nosniff') 'Download attachment'
    $storedFile = Join-Path $env:Distribution__StorageRoot (([Guid]$latest.id).ToString('N') + '.package')
    [IO.File]::WriteAllText($storedFile, 'tampered test fixture')
    Check ((Request "/api/releases/$($latest.id)/download").StatusCode -eq 503) 'Corrupt download fails closed'
    Copy-Item -LiteralPath $package -Destination $storedFile -Force
    $update = Invoke-RestMethod 'http://127.0.0.1:5303/api/updates/check?platform=Android&channel=Development&version=0.1.0&buildNumber=1'
    Check ($update.updateAvailable -and $update.mandatory) 'Mandatory compatible update'
    Check ((Request '/admin/releases/publish' 'POST' $session $null $form).StatusCode -eq 400) 'Duplicate release denied'
    $unsafe = Join-Path $testRoot 'unsafe.exe'; Copy-Item -LiteralPath $package -Destination $unsafe
    $form.artifact = Get-Item $unsafe
    Check ((Request '/admin/releases/publish' 'POST' $session $null $form).StatusCode -eq 400) 'Executable upload denied'
    Check ((Request "/admin/releases/$($latest.id)/revoke" 'POST' $session @{__RequestVerificationToken=$csrf}).StatusCode -eq 302) 'Revoke release'
    Check ((Request "/api/releases/$($latest.id)/download").StatusCode -eq 404) 'Revoked download denied'
    Check ((Request '/api/releases/latest?platform=Android&channel=Development').StatusCode -eq 404) 'Revoked release not selected'
    Check ((Request '/admin/logout' 'POST' $session @{__RequestVerificationToken=$csrf}).StatusCode -eq 302) 'Logout'
    Check ((Request '/api/admin/releases' 'GET' $session).StatusCode -eq 401) 'Logged-out admin denied'
    $csrf = Token (Request '/admin/login' 'GET' $session).Content
    for ($attempt=0; $attempt -lt 3; $attempt++) { $limited = Request '/admin/login' 'POST' $session @{username=$env:Distribution__AdminUsername;password='wrong';__RequestVerificationToken=$csrf} }
    Check ($limited.StatusCode -eq 429) 'Login rate limit'
    Write-Host "PASS: $checks HTTP distribution/security checks; debug-signed APK used only in temporary catalogue."
} finally {
    if ($api -and !$api.HasExited) { Stop-Process -Id $api.Id }
    if ($dashboard -and !$dashboard.HasExited) { Stop-Process -Id $dashboard.Id }
    foreach ($name in $settings) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $allowedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (!$resolvedTestRoot.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase) -or !(Split-Path $resolvedTestRoot -Leaf).StartsWith('sensor3-http-')) { throw 'Unsafe cleanup target' }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    $password = $null
    if ($signer) { $signer.Dispose() }
}
