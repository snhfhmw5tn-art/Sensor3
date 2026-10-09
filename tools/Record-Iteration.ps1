param([Parameter(Mandatory)][ValidateRange(1,18)][int]$Iteration,
    [Parameter(Mandatory)][string]$TestResult, [Parameter(Mandatory)][string[]]$Blockers,
    [switch]$RecordCommit)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$path = Join-Path $root 'iterations.json'
$entries = Get-Content $path -Raw | ConvertFrom-Json
$entry = $entries | Where-Object number -EQ $Iteration
if ($RecordCommit) {
    $entry.latestCommit = (& git -C $root rev-parse HEAD).Trim()
    $entry.commitDateUtc = (& git -C $root show -s --format=%cI HEAD).Trim()
} else {
    $entry.status = 'Implemented'; $entry.testResult = $TestResult; $entry.blockers = $Blockers
}
[IO.File]::WriteAllText($path,($entries | ConvertTo-Json -Depth 12) + [Environment]::NewLine)
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$implemented = @($entries | Where-Object status -In @('Implemented','Verified','Released')).Count
$verified = @($entries | Where-Object status -In @('Verified','Released')).Count
$released = @($entries | Where-Object status -EQ 'Released').Count
$lines = @('# Iterationshistorik', '', "Version $version. Implementerade: $implemented/18. Verifierade: $verified/18. Publicerade: $released/18.", '', '| Nr | Namn | Status | Rapport |', '|---|---|---|---|')
foreach ($item in $entries) {
    $number = '{0:D2}' -f $item.number
    $report = if (Test-Path (Join-Path $root "docs/iteration-$number.md")) { "[Rapport](docs/iteration-$number.md)" } else { 'Återstår' }
    $lines += "| $number | $($item.name) | $($item.status) | $report |"
}
$lines += @('', 'Verified kräver dokumenterade acceptanskriterier på verklig hårdvara. Released kräver separat signerings-, installations- och publiceringsbevis. Simulering räknas inte som fältvalidering.', '', 'iterations.json registrerar implementationscommitten i en separat metadatacommit eftersom en commit inte kan bädda in sin egen hash. Byggmetadata visar exakt byggd HEAD och om arbetskopian innehöll ändringar.')
[IO.File]::WriteAllText((Join-Path $root 'ITERATIONS.md'),($lines -join [Environment]::NewLine) + [Environment]::NewLine)
