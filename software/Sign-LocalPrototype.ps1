# Creates a local code-signing identity, but does NOT trust it or install a driver.
$ErrorActionPreference = 'Stop'
$driver = Join-Path $PSScriptRoot 'build/driver'
$signTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
$inf2cat = Join-Path (Split-Path $PSScriptRoot) '.toolchain/packages/wdk/c/bin/10.0.26100.0/x86/Inf2Cat.exe'
if (!(Test-Path "$driver/MinimalJoystick.dll")) { throw 'Build the driver first.' }
$certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=MinimalJoystick Local Prototype Only' -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(30) -KeyExportPolicy NonExportable -HashAlgorithm SHA256
& $signTool sign /fd SHA256 /s My /sha1 $certificate.Thumbprint "$driver/MinimalJoystick.dll"
if ($LASTEXITCODE) { throw 'Driver binary signing failed.' }
& $inf2cat "/driver:$driver" /os:10_X64 /uselocaltime
if ($LASTEXITCODE) { throw 'Catalog regeneration failed.' }
& $signTool sign /fd SHA256 /s My /sha1 $certificate.Thumbprint "$driver/MinimalJoystick.cat"
if ($LASTEXITCODE) { throw 'Catalog signing failed.' }
Export-Certificate -Cert $certificate -FilePath "$driver/MinimalJoystick-Local-Test.cer" | Out-Null
foreach ($binary in @((Join-Path $PSScriptRoot 'build/MinimalJoystick.exe'), (Join-Path $driver 'DeviceSetup.exe'), (Join-Path $PSScriptRoot 'build/InspectController.exe'), (Join-Path $PSScriptRoot 'build/InspectDirectInput.exe'))) {
    if (Test-Path $binary) {
        & $signTool sign /fd SHA256 /s My /sha1 $certificate.Thumbprint $binary
        if ($LASTEXITCODE) { throw "Prototype binary signing failed: $binary" }
    }
}
@{ thumbprint=$certificate.Thumbprint; subject=$certificate.Subject; expires=$certificate.NotAfter.ToUniversalTime().ToString('o'); status='LOCAL TEST ONLY — NOT A PUBLICLY TRUSTED DRIVER'; trustInstalled=$false } | ConvertTo-Json | Set-Content "$driver/local-signing.json"
Write-Output 'Local test signatures created. No certificate trust, boot configuration or driver installation was changed.'
