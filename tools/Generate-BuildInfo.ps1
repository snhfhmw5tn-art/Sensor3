param([string]$OutputPath, [string]$Platform, [string]$Version, [string]$BuildNumber = '1', [string]$ReleaseChannel = 'Development')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
function GitValue([string[]]$Arguments) {
 $ErrorActionPreference = 'Continue'
 $result = & git -C $root @Arguments 2>$null
 if ($LASTEXITCODE -ne 0 -or !$result) { return 'Unknown' }
 return ($result -join "`n").Trim()
}
$hash = GitValue @('rev-parse','HEAD')
$commitDate = if ($hash -eq 'Unknown') { $null } else { GitValue @('show','-s','--format=%cI','HEAD') }
if ($commitDate -and $commitDate -ne 'Unknown') { $commitDate = ([DateTimeOffset]::Parse($commitDate)).ToUniversalTime().ToString('O') } else { $commitDate = $null }
$branch = GitValue @('rev-parse','--abbrev-ref','HEAD')
$dirty = [bool](& git -C $root status --porcelain --untracked-files=normal)
$iterations = Get-Content -LiteralPath (Join-Path $root iterations.json) -Raw | ConvertFrom-Json
function Latest($statuses) { $n = @($iterations | Where-Object status -in $statuses | ForEach-Object number); if ($n.Count -eq 0) {return 0}; return ($n | Measure-Object -Maximum).Maximum }
$short = if ($hash -eq 'Unknown') {'Unknown'} else {$hash.Substring(0,7)}
$metadata = @{
 ApplicationName='Sensor 3'; ApplicationVersion=$Version; BuildNumber=$BuildNumber; GitCommitHash=$hash; GitCommitShortHash=$short; GitCommitDateUtc=$commitDate; BuildDateUtc=[DateTimeOffset]::UtcNow.ToString('O'); GitBranch=$branch; IsDirtyBuild=$dirty; TargetPlatform=$Platform; ReleaseChannel=$ReleaseChannel;
 LatestImplementedIteration=(Latest @('Implemented','Verified','Released')); LatestVerifiedIteration=(Latest @('Verified')); LatestPublishedIteration=(Latest @('Released')); BuildIdentifier="$Version-$BuildNumber-$short-$Platform"
}
$parent = Split-Path $OutputPath
New-Item -ItemType Directory -Force $parent | Out-Null
$metadata | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding utf8
