$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$vc = Get-ChildItem "$root/.toolchain/BuildTools/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdk = 'C:\Program Files (x86)\Windows Kits\10'
$out = Join-Path $PSScriptRoot 'build'
$arguments = @('/nologo','/EHsc','/MT','/DUNICODE','/D_UNICODE',('/I'+$vc.FullName+'/include'))
foreach ($kind in @('ucrt','um','shared')) { $arguments += '/I'+$sdk+'/Include/10.0.26100.0/'+$kind }
$arguments += @((Join-Path $PSScriptRoot 'tools/InspectDirectInput.cpp'), '/link',('/LIBPATH:'+$vc.FullName+'/lib/x64'))
foreach ($kind in @('ucrt','um')) { $arguments += '/LIBPATH:'+$sdk+'/Lib/10.0.26100.0/'+$kind+'/x64' }
$arguments += @('dinput8.lib','dxguid.lib','ole32.lib','user32.lib','/OUT:InspectDirectInput.exe')
Push-Location $out
try {
    & "$($vc.FullName)/bin/Hostx64/x64/cl.exe" @arguments
    if ($LASTEXITCODE) {throw 'DirectInput probe build failed.'}
    $record = Join-Path $out 'driver/local-signing.json'
    if (Test-Path $record) {
        $thumbprint = (Get-Content $record -Raw | ConvertFrom-Json).thumbprint
        & "$sdk/bin/10.0.26100.0/x64/signtool.exe" sign /fd SHA256 /s My /sha1 $thumbprint (Join-Path $out 'InspectDirectInput.exe')
        if ($LASTEXITCODE) {throw 'Probe signing failed.'}
    }
} finally {Pop-Location}
