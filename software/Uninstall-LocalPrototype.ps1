$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run from an administrator PowerShell.' }
$instance = 'ROOT\MINIMALJOYSTICK\0000'
$device = Get-PnpDevice -InstanceId $instance -ErrorAction SilentlyContinue
if ($device) {
    $inf = (Get-PnpDeviceProperty -InstanceId $instance -KeyName DEVPKEY_Device_DriverInfPath).Data
    & pnputil.exe /remove-device $instance
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3010) {throw 'Device removal failed; certificate trust was retained for diagnosis.'}
    if ($inf -match '^oem\d+\.inf$') {
        & pnputil.exe /delete-driver $inf
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3010) {throw 'Driver package removal failed; certificate trust was retained for diagnosis.'}
    }
}
$record = Get-Content (Join-Path $PSScriptRoot 'build/driver/local-signing.json') -Raw | ConvertFrom-Json
foreach ($store in @('Root','TrustedPublisher')) {
    $path = "Cert:\LocalMachine\$store\$($record.thumbprint)"
    if (Test-Path $path) { Remove-Item -LiteralPath $path }
}
$record.trustInstalled = $false
$record | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'build/driver/local-signing.json')
Write-Output 'MinimalJoystick device and recorded local test trust removed. No unrelated drivers or certificates were changed.'
