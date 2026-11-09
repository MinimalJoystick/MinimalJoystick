param([Parameter(Mandatory=$true)][ValidatePattern('^[0-9A-Fa-f]{40}$')][string]$CertificateThumbprint)
$ErrorActionPreference='Stop'
$cert=Get-Item "Cert:/CurrentUser/My/$CertificateThumbprint" -ErrorAction Stop
if ($cert.Subject -eq $cert.Issuer) {throw 'A self-signed test certificate is not a public signing identity.'}
if ($cert.NotAfter -lt (Get-Date)) {throw 'Certificate has expired.'}
if (!$cert.HasPrivateKey) {throw 'Connect/authenticate the cloud signing provider first; never export its private key.'}
$signTool='C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
$files=@('MinimalJoystick.exe','MinimalJoystickSetup.exe','InspectController.exe','InspectDirectInput.exe','driver/DeviceSetup.exe')
foreach ($file in $files) {
    $path=Join-Path $PSScriptRoot "build/$file"
    if (!(Test-Path -LiteralPath $path)) {throw "Missing candidate file $file"}
}
foreach ($file in $files) {
    $path=Join-Path $PSScriptRoot "build/$file"
    & $signTool sign /fd SHA256 /tr http://time.certum.pl /td SHA256 /s My /sha1 $CertificateThumbprint $path
    if ($LASTEXITCODE) {throw "Public signing failed for $file"}
    & $signTool verify /pa /v $path
    if ($LASTEXITCODE) {throw "Public signature verification failed for $file"}
}
Write-Output 'App/setup/helper binaries signed and timestamped. Driver DLL/catalog signing and release qualification are separate; no driver trust or installation changed.'
