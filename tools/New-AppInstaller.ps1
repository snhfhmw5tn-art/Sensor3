param(
    [Parameter(Mandatory)][string]$MsixPath,
    [Parameter(Mandatory)][uri]$PackageUrl,
    [Parameter(Mandatory)][uri]$AppInstallerUrl,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
if ($PackageUrl.Scheme -ne 'https' -or $AppInstallerUrl.Scheme -ne 'https') { throw 'App Installer requires HTTPS URLs.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $MsixPath))
try {
    $entry = $archive.GetEntry('AppxManifest.xml')
    if (!$entry) { throw 'MSIX manifest missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $identity = $manifest.Package.Identity
    if ($identity.Name -ne 'se.qsys.sensor3' -or $identity.Publisher -ne 'CN=Sensor3') { throw 'Unexpected package identity/publisher.' }
    $document = [xml]::new()
    $ns = 'http://schemas.microsoft.com/appx/appinstaller/2018'
    $root = $document.CreateElement('AppInstaller', $ns)
    $root.SetAttribute('Uri', $AppInstallerUrl.AbsoluteUri)
    $root.SetAttribute('Version', $identity.Version)
    [void]$document.AppendChild($root)
    $package = $document.CreateElement('MainPackage', $ns)
    foreach ($name in @('Name','Publisher','Version','ProcessorArchitecture')) { $package.SetAttribute($name, $identity.GetAttribute($name)) }
    $package.SetAttribute('Uri', $PackageUrl.AbsoluteUri)
    [void]$root.AppendChild($package)
    $settings = $document.CreateElement('UpdateSettings', $ns)
    $launch = $document.CreateElement('OnLaunch', $ns)
    $launch.SetAttribute('HoursBetweenUpdateChecks','0')
    $launch.SetAttribute('ShowPrompt','true')
    $launch.SetAttribute('UpdateBlocksActivation','false')
    [void]$settings.AppendChild($launch)
    [void]$root.AppendChild($settings)
    $document.Save([IO.Path]::GetFullPath($OutputPath))
} finally { $archive.Dispose() }
