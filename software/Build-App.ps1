param([switch]$CompileOnly)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 C# compiler is required.' }
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $output | Out-Null
$appSource = Join-Path $PSScriptRoot 'MinimalJoystick.cs'
$appBinary = Join-Path $output 'MinimalJoystick.exe'
$sources = @(Get-ChildItem $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll "/out:$appBinary" @sources
if ($LASTEXITCODE) { throw 'Application compilation failed.' }
$helper = Join-Path $output 'driver/DeviceSetup.exe'
New-Item -ItemType Directory -Force (Split-Path $helper) | Out-Null
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$helper" (Join-Path $PSScriptRoot 'driver/DeviceSetup.cs')
if ($LASTEXITCODE) { throw 'Device lifecycle helper compilation failed.' }
$probe = Join-Path $output 'InspectController.exe'
& $compiler /nologo /target:exe /platform:x64 "/out:$probe" (Join-Path $PSScriptRoot 'tools/InspectController.cs')
if ($LASTEXITCODE) { throw 'Windows controller probe compilation failed.' }
$signingRecord = Join-Path $output 'driver/local-signing.json'
if (Test-Path $signingRecord) {
    $thumbprint = (Get-Content $signingRecord -Raw | ConvertFrom-Json).thumbprint
    $signTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
    foreach ($binary in @($appBinary,$helper,$probe)) {
        & $signTool sign /fd SHA256 /s My /sha1 $thumbprint $binary
        if ($LASTEXITCODE) { throw "Local prototype signing failed: $binary" }
    }
}
Copy-Item -LiteralPath $appBinary -Destination (Join-Path $output 'MinimalJoystickSetup.exe') -Force
$compiledSources=[ordered]@{}
Get-ChildItem $PSScriptRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object { $compiledSources[$_.Name]=(Get-FileHash $_.FullName -Algorithm SHA256).Hash }
[ordered]@{status='COMPILED; runtime acceptance is separate';source_sha256=$compiledSources;app_sha256=(Get-FileHash $appBinary -Algorithm SHA256).Hash} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'app-build-sources.json') -Encoding UTF8
if ($CompileOnly) { Write-Output 'Compiled and locally signed; runtime tests were explicitly skipped.'; return }
$process = Start-Process "$output/MinimalJoystick.exe" -ArgumentList "--self-test `"$output/self-test.txt`"" -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode) { throw 'Application self-test failed.' }
Get-Content "$output/self-test.txt"
Copy-Item -LiteralPath $appBinary -Destination (Join-Path $output 'MinimalJoystickSetup.exe') -Force
