#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$PublishPath = (Join-Path $PSScriptRoot '..\artifacts\iis-publish'),
    [string]$SitePath = 'C:\inetpub\sensor3'
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
$source = (Resolve-Path -LiteralPath $PublishPath).Path
if (!(Test-Path -LiteralPath (Join-Path $source 'Sensor3.Dashboard.dll')) -or
    !(Test-Path -LiteralPath (Join-Path $source 'web.config'))) {
    throw 'Publicera Sensor3.Dashboard innan installationen.'
}
if (!(Test-Path 'C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll')) {
    throw 'Installera .NET 10 Hosting Bundle innan installationen.'
}
if (Test-Path 'IIS:\Sites\sensor3') {
    throw 'Webbplatsen sensor3 finns redan. Ingen befintlig webbplats ändras.'
}
if (Test-Path 'IIS:\AppPools\sensor3') {
    throw 'App pool sensor3 finns redan. Kontrollera den innan installationen.'
}
foreach ($site in Get-ChildItem 'IIS:\Sites') {
    foreach ($binding in $site.Bindings.Collection) {
        if ($binding.protocol -eq 'http' -and $binding.bindingInformation -match ':80:prototyp\.qsys\.se$') {
            throw "Bindningen används redan av $($site.Name). Ingen befintlig webbplats ändras."
        }
    }
}
if ((Test-Path -LiteralPath $SitePath) -and (Get-ChildItem -LiteralPath $SitePath -Force | Select-Object -First 1)) {
    throw 'Målkatalogen innehåller redan filer. Välj en tom katalog.'
}
New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $SitePath -Recurse
$dataPath = Join-Path $env:ProgramData 'Sensor3\distribution'
New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
$configPath = Join-Path $SitePath 'web.config'
[xml]$config = Get-Content -LiteralPath $configPath
$aspnet = $config.configuration.location.'system.webServer'.aspNetCore
$environment = $config.CreateElement('environmentVariables')
$storage = $config.CreateElement('environmentVariable')
$storage.SetAttribute('name', 'Distribution__StorageRoot')
$storage.SetAttribute('value', $dataPath)
$environment.AppendChild($storage) | Out-Null
$aspnet.AppendChild($environment) | Out-Null
$config.Save($configPath)
New-WebAppPool -Name 'sensor3' | Out-Null
Set-ItemProperty 'IIS:\AppPools\sensor3' -Name managedRuntimeVersion -Value ''
Set-ItemProperty 'IIS:\AppPools\sensor3' -Name processModel.identityType -Value ApplicationPoolIdentity
New-Website -Name 'sensor3' -Port 80 -IPAddress '*' -HostHeader 'prototyp.qsys.se' -PhysicalPath $SitePath -ApplicationPool 'sensor3' | Out-Null
$acl = Get-Acl -LiteralPath $SitePath
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule('IIS AppPool\sensor3', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $SitePath -AclObject $acl
$dataAcl = Get-Acl -LiteralPath $dataPath
$dataRule = New-Object System.Security.AccessControl.FileSystemAccessRule('IIS AppPool\sensor3', 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$dataAcl.AddAccessRule($dataRule)
Set-Acl -LiteralPath $dataPath -AclObject $dataAcl
Start-Service W3SVC
Start-Website 'sensor3'
Get-WebBinding -Name 'sensor3'
Write-Host 'Webbplatsen är skapad. Kontrollera http://prototyp.qsys.se/health lokalt. HTTPS och proxykonfiguration görs separat.'
