$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $output | Out-Null
$sources=@(Get-ChildItem $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
$binary=Join-Path $output 'UsbBridgeTests.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ /main:MinimalJoystick.UsbTestMain /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll "/out:$binary" @sources (Join-Path $PSScriptRoot 'tools/UsbTestMain.cs')
if ($LASTEXITCODE) { throw 'USB bridge test compilation failed.' }
Push-Location $root
try {
    & $binary (Join-Path $output 'usb-bridge-tests.txt')
    if ($LASTEXITCODE) { throw 'USB bridge tests failed.' }
    $record=Get-Content (Join-Path $output 'app-build-sources.json') -Raw | ConvertFrom-Json
    $currentSources=@(Get-ChildItem $PSScriptRoot -Filter '*.cs' | Sort-Object Name)
    if (@($record.source_sha256.PSObject.Properties).Count -ne $currentSources.Count) { throw 'Rebuild the app: compiled source inventory changed.' }
    foreach ($source in $currentSources) { if ($record.source_sha256.($source.Name) -ne (Get-FileHash $source.FullName -Algorithm SHA256).Hash) { throw "Rebuild the app: source changed after compilation: $($source.Name)" } }
    if ($record.app_sha256 -ne (Get-FileHash (Join-Path $output 'MinimalJoystick.exe') -Algorithm SHA256).Hash) { throw 'App binary differs from its build record.' }
    [ordered]@{status='PASS: isolated USB host tests; does not claim GUI/COM/HID runtime acceptance';source_sha256=$record.source_sha256;app_sha256=$record.app_sha256;fixture_sha256=(Get-FileHash (Join-Path $PSScriptRoot 'tests/fixtures/firmware-mj1.json') -Algorithm SHA256).Hash;test_main_sha256=(Get-FileHash (Join-Path $PSScriptRoot 'tools/UsbTestMain.cs') -Algorithm SHA256).Hash;report_sha256=(Get-FileHash (Join-Path $output 'usb-bridge-tests.txt') -Algorithm SHA256).Hash} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'usb-host-validation.json') -Encoding UTF8
} finally { Pop-Location }
