param(
    [Parameter(Mandatory)][ValidateSet('Android','Windows')][string]$Platform,
    [Parameter(Mandatory)][ValidateRange(3,65535)][int]$BuildNumber,
    [Parameter(Mandatory)][string]$UpdateSettingsPath,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ReleaseNotes,
    [string]$AndroidKeyStore,
    [string]$AndroidKeyAlias,
    [string]$AndroidPasswordFile,
    [string]$WindowsCertificateThumbprint,
    [uri]$PackageUrl,
    [uri]$AppInstallerUrl
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
if (& git -C $repo status --porcelain --untracked-files=normal) { throw 'Publish from a clean committed checkout for accurate build metadata.' }
$settings = Get-Content -LiteralPath $UpdateSettingsPath -Raw | ConvertFrom-Json
if (([uri]$settings.serverUrl).Scheme -ne 'https' -or !$settings.manifestPublicKeyPem) { throw 'Configure HTTPS and pinned manifest public key first.' }
$rsa = [Security.Cryptography.RSA]::Create()
try { $rsa.ImportFromPem($settings.manifestPublicKeyPem); if ($rsa.KeySize -lt 2048) { throw 'Manifest public key is too weak.' } } finally { $rsa.Dispose() }
if ($settings.channel -notin @('Development','Beta','Stable')) { throw 'Select a valid release channel.' }
$settingsPath = (Resolve-Path -LiteralPath $UpdateSettingsPath).Path
$output = Join-Path $repo "artifacts/releases/$Platform/$BuildNumber"
if (Test-Path -LiteralPath $output) { throw 'Output build already exists. Choose a new monotonically increasing build number.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
    $arguments = @('publish',(Join-Path $repo 'Sensor3.Mobile/Sensor3.Mobile.csproj'),'-c','Release',"-p:ApplicationVersion=$BuildNumber", "-p:Sensor3BuildNumber=$BuildNumber", "-p:Sensor3ReleaseChannel=$($settings.channel)", "-p:Sensor3UpdateSettingsPath=$settingsPath",'-o',$output)
    if ($Platform -eq 'Android') {
        foreach ($path in @($AndroidKeyStore,$AndroidPasswordFile)) {
            if (!$path -or !(Test-Path -LiteralPath $path)) { throw 'Existing external keystore/password file required.' }
            $resolved = [IO.Path]::GetFullPath($path)
            if ($resolved.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Signing secrets must be outside the repository.' }
        }
        if (!$AndroidKeyAlias) { throw 'Signing alias required.' }
        $arguments += @('-f','net10.0-android','-p:AndroidPackageFormats=apk','-p:AndroidKeyStore=true',"-p:AndroidSigningKeyStore=$AndroidKeyStore", "-p:AndroidSigningKeyAlias=$AndroidKeyAlias", "-p:AndroidSigningKeyPass=file:$AndroidPasswordFile", "-p:AndroidSigningStorePass=file:$AndroidPasswordFile")
    } else {
        if (!$WindowsCertificateThumbprint -or !$PackageUrl -or !$AppInstallerUrl) { throw 'Certificate and HTTPS distribution URLs required.' }
        $certificate = Get-Item -LiteralPath "Cert:/CurrentUser/My/$WindowsCertificateThumbprint"
        if (!$certificate.HasPrivateKey -or $certificate.Subject -ne 'CN=Sensor3' -or $certificate.NotAfter -le [DateTime]::Now -or
            '1.3.6.1.5.5.7.3.3' -notin $certificate.EnhancedKeyUsageList.ObjectId.Value) { throw 'A valid Code Signing certificate matching CN=Sensor3 is required.' }
        $arguments += @('-f','net10.0-windows10.0.19041.0','-p:Sensor3WindowsOnly=true','-p:RuntimeIdentifierOverride=win-x64','-p:WindowsPackageType=MSIX','-p:GenerateAppxPackageOnBuild=true','-p:AppxPackageSigningEnabled=true',"-p:PackageCertificateThumbprint=$WindowsCertificateThumbprint",'-p:AppxBundle=Never',"-p:AppxPackageDir=$output/")
    }
    $restoreProperties = @($arguments | Where-Object { $_ -like '-p:*' })
    & dotnet restore (Join-Path $repo 'Sensor3.Mobile/Sensor3.Mobile.csproj') @restoreProperties
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    $arguments += '--no-restore'
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if ($Platform -eq 'Windows') {
        $packages = @(Get-ChildItem -LiteralPath $output -Recurse -Filter '*.msix')
        if ($packages.Count -ne 1) { throw 'Expected one MSIX package.' }
        & "$PSScriptRoot/New-AppInstaller.ps1" -MsixPath $packages[0].FullName -PackageUrl $PackageUrl -AppInstallerUrl $AppInstallerUrl -OutputPath (Join-Path $output 'Sensor3.appinstaller')
    } else {
        $packages = @(Get-ChildItem -LiteralPath $output -Recurse -Filter '*-Signed.apk')
        if ($packages.Count -ne 1) { throw 'Expected one signed APK.' }
    }
    [xml]$properties = Get-Content -LiteralPath (Join-Path $repo 'Directory.Build.props') -Raw
    $iterations = Get-Content -LiteralPath (Join-Path $repo 'iterations.json') -Raw | ConvertFrom-Json
    $manifest = @{
        platform=$Platform; channel=$settings.channel; version=[string]$properties.Project.PropertyGroup.Version; buildNumber=$BuildNumber
        gitCommitHash=(& git -C $repo rev-parse HEAD).Trim()
        gitCommitDateUtc=([DateTimeOffset]::Parse((& git -C $repo show -s --format=%cI HEAD).Trim())).ToUniversalTime().ToString('O')
        iterationNumber=($iterations | Where-Object status -in @('Implemented','Verified','Released') | Measure-Object number -Maximum).Maximum
        releaseNotes=$ReleaseNotes; expectedSha256=(Get-FileHash -LiteralPath $packages[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        compatibility=@{minimumServerVersion='0.3.0'}; updatePolicy=@{mandatory=$false}
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'release-manifest.json') -Encoding utf8
    Write-Host "Packages created at $output. Verify package signature and upgrade on both platforms before publication."
