param([Parameter(Mandatory)][string]$PrivateKeyPath, [Parameter(Mandatory)][string]$PublicKeyPath)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot)) + [IO.Path]::DirectorySeparatorChar
$privatePath = [IO.Path]::GetFullPath($PrivateKeyPath)
if ($privatePath.StartsWith($repo,[StringComparison]::OrdinalIgnoreCase)) { throw 'Private key must be outside the repository.' }
if ((Test-Path -LiteralPath $privatePath) -or (Test-Path -LiteralPath $PublicKeyPath)) { throw 'Key files already exist; never replace the pinned signing identity.' }
$rsa = [Security.Cryptography.RSA]::Create(3072)
try {
    [IO.File]::WriteAllText($privatePath, $rsa.ExportPkcs8PrivateKeyPem())
    $acl = Get-Acl -LiteralPath $privatePath
    $acl.SetAccessRuleProtection($true,$false)
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','Allow')
    $acl.SetAccessRule($rule)
    Set-Acl -LiteralPath $privatePath -AclObject $acl
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($PublicKeyPath),$rsa.ExportSubjectPublicKeyInfoPem())
    Write-Host 'Manifest key created. Configure the server private-key path and embed the public key in client update settings.'
} finally { $rsa.Dispose() }
