param([string]$Username = 'release-admin')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$securePassword = Read-Host 'Administratörslösenord (minst 16 tecken)' -AsSecureString
$password = [System.Net.NetworkCredential]::new('', $securePassword).Password
try {
    if ($password.Length -lt 16) { throw 'Använd minst 16 tecken.' }
    $salt = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $derivation = [System.Security.Cryptography.Rfc2898DeriveBytes]::new($password, $salt, 210000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $hash = $derivation.GetBytes(32) } finally { $derivation.Dispose() }
    $encoded = 'pbkdf2-sha256:210000:' + [Convert]::ToBase64String($salt) + ':' + [Convert]::ToBase64String($hash)
    $settings = @{ 'Distribution:AdminUsername' = $Username; 'Distribution:AdminPasswordHash' = $encoded } | ConvertTo-Json
    foreach ($project in @('Sensor3.Api','Sensor3.Dashboard')) {
        $settings | dotnet user-secrets set --project (Join-Path $root $project) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'User secrets kunde inte sparas.' }
    }
    Write-Host 'Administratörskontot konfigurerat i lokala Development user-secrets. Starta om API/dashboard.'
} finally { $password = $null; $settings = $null; $encoded = $null; $securePassword.Dispose() }
