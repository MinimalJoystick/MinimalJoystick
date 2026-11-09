param(
    [string]$ToolchainRoot = (Join-Path (Split-Path $PSScriptRoot) '.toolchain'),
    [string]$SdkRoot = 'C:\Program Files (x86)\Windows Kits\10',
    [string]$SdkVersion = '10.0.26100.0'
)
$ErrorActionPreference = 'Stop'
$vc = Get-ChildItem (Join-Path $ToolchainRoot 'BuildTools/VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (!$vc) { throw 'Microsoft C++ build tools are required. See software/README.md.' }
$wdk = Join-Path $ToolchainRoot 'packages/wdk/c'
$out = Join-Path $PSScriptRoot 'build/driver'
New-Item -ItemType Directory -Force $out | Out-Null
$includes = @(
    (Join-Path $vc.FullName 'include'),
    (Join-Path $SdkRoot "Include/$SdkVersion/ucrt"),
    (Join-Path $SdkRoot "Include/$SdkVersion/um"),
    (Join-Path $SdkRoot "Include/$SdkVersion/shared"),
    (Join-Path $wdk "Include/$SdkVersion/um"),
    (Join-Path $wdk "Include/$SdkVersion/shared"),
    (Join-Path $wdk "Include/$SdkVersion/km"),
    (Join-Path $wdk "Include/$SdkVersion/km/wdm"),
    (Join-Path $wdk 'Include/wdf/umdf/2.15'),
    (Join-Path $PSScriptRoot 'driver')
)
$libraries = @((Join-Path $vc.FullName 'lib/x64'), (Join-Path $SdkRoot "Lib/$SdkVersion/ucrt/x64"), (Join-Path $SdkRoot "Lib/$SdkVersion/um/x64"), (Join-Path $wdk 'Lib/wdf/umdf/x64/2.15'))
$cl = Join-Path $vc.FullName 'bin/Hostx64/x64/cl.exe'
$link = Join-Path $vc.FullName 'bin/Hostx64/x64/link.exe'
Push-Location $out
try {
    $arguments = @('/nologo','/c','/W4','/O2','/MT','/DUNICODE','/D_UNICODE','/DUMDF_VERSION_MAJOR=2','/DUMDF_VERSION_MINOR=15','/DUMDF_USING_NTSTATUS','/D_WIN32_WINNT=0x0A00')
    $arguments += @($includes | ForEach-Object { '/I' + $_ })
    $arguments += @((Join-Path $PSScriptRoot 'driver/vhidmini.c'), (Join-Path $PSScriptRoot 'driver/util.c'))
    & $cl @arguments
    if ($LASTEXITCODE) { throw 'Driver compilation failed.' }
    $arguments = @('/nologo','/DLL','/SUBSYSTEM:WINDOWS','/OUT:MinimalJoystick.dll','vhidmini.obj','util.obj','WdfDriverStubUm.lib','ntdll.lib','kernel32.lib','advapi32.lib')
    $arguments += @($libraries | ForEach-Object { '/LIBPATH:' + $_ })
    & $link @arguments
    if ($LASTEXITCODE) { throw 'Driver link failed.' }
    Copy-Item (Join-Path $PSScriptRoot 'driver/MinimalJoystick.inf') .
    $inf2cat = Get-ChildItem (Join-Path $wdk 'bin') -Filter inf2cat.exe -Recurse | Select-Object -First 1
    if (!$inf2cat) { throw 'WDK Inf2Cat is missing.' }
    & $inf2cat.FullName "/driver:$out" /os:10_X64 /uselocaltime
    if ($LASTEXITCODE) { throw 'Driver catalog generation failed.' }
    Write-Output 'Built unsigned MinimalJoystick driver package. No driver installed; no trust/security settings changed.'
} finally { Pop-Location }
