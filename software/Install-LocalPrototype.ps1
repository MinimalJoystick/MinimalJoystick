param([switch]$TrustLocalTestCertificate)
$ErrorActionPreference = 'Stop'
if (!$TrustLocalTestCertificate) { throw 'Explicit -TrustLocalTestCertificate is required: installation adds this prototype signing certificate to LocalMachine Root and TrustedPublisher. Review README first.' }
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run from an administrator PowerShell.' }
$package = Join-Path $PSScriptRoot 'build/driver'
$certPath = Join-Path $package 'MinimalJoystick-Local-Test.cer'
$certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($certPath)
$record = Get-Content "$package/local-signing.json" -Raw | ConvertFrom-Json
if ($certificate.Thumbprint -ne $record.thumbprint -or $certificate.Subject -ne 'CN=MinimalJoystick Local Prototype Only' -or $certificate.NotAfter -lt (Get-Date)) {throw 'Unexpected or expired prototype certificate.'}
foreach ($name in @('MinimalJoystick.dll','MinimalJoystick.cat')) {
    $signature = Get-AuthenticodeSignature (Join-Path $package $name)
    if (!$signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint -or $signature.Status -eq 'HashMismatch') {throw "Invalid prototype signature: $name"}
}
# No test-signing boot flag, Secure Boot or Memory Integrity changes are made.
foreach ($store in @('Root','TrustedPublisher')) { Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\LocalMachine\$store" | Out-Null }
& pnputil.exe /add-driver "$package/MinimalJoystick.inf"
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3010) {throw 'Driver staging failed; remove the local test certificate using Uninstall-LocalPrototype.ps1.'}
& "$package/DeviceSetup.exe" "$package/MinimalJoystick.inf"
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3010) {throw 'Device installation failed. Do not weaken Windows security settings; inspect setupapi.dev.log and run Uninstall-LocalPrototype.ps1.'}
Get-PnpDevice -InstanceId 'ROOT\MINIMALJOYSTICK\0000' | Format-List Status,FriendlyName,InstanceId
$record.trustInstalled = $true
$record | ConvertTo-Json | Set-Content "$package/local-signing.json"
Write-Output 'Launch MinimalJoystick.exe and verify it in joy.cpl before opening the simulator.'
