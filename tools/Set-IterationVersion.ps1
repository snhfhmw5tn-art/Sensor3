param([Parameter(Mandatory)][ValidateRange(1,18)][int]$Iteration)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$version = "0.$Iteration.0"
$previousVersion = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
foreach ($relative in @('Directory.Build.props','Sensor3.Mobile/Sensor3.Mobile.csproj','Sensor3.Mobile/Platforms/Windows/Package.appxmanifest','Sensor3.Tests/BuildMetadataTests.cs','tools/Smoke-Test.ps1','tools/Distribution-Smoke-Test.ps1')) {
    $file = Join-Path $root $relative
    $text = [IO.File]::ReadAllText($file)
    $text = [regex]::Replace($text,('(?<![\d.])' + [regex]::Escape($previousVersion) + '(?!\d)'),$version)
    if ($relative -like '*Package.appxmanifest') { $text = [regex]::Replace($text,'Version="0\.\d+\.0\.\d+"',"Version=`"$version.$Iteration`"") }
    if ($relative -like '*.csproj') { $text = [regex]::Replace($text,'>\d+</ApplicationVersion>',">$Iteration</ApplicationVersion>") }
    if ($relative -like '*Distribution-Smoke-Test.ps1') {
        $text = [regex]::Replace($text,'buildNumber=\d+; iterationNumber=',"buildNumber=$Iteration; iterationNumber=")
        $text = [regex]::Replace($text,'\$wrongVersion.buildNumber = \d+',('$wrongVersion.buildNumber = ' + ($Iteration+1)))
    }
    [IO.File]::WriteAllText($file,$text)
}
$file = Join-Path $root 'Directory.Build.targets'
[IO.File]::WriteAllText($file,([regex]::Replace([IO.File]::ReadAllText($file),'>\d+</Sensor3BuildNumber>',">$Iteration</Sensor3BuildNumber>")))
