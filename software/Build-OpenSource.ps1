param(
    [Parameter(Mandatory=$true)][string]$CompilerRoot,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$compiler=Join-Path $CompilerRoot 'tasks/net472/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {throw 'Use Microsoft.Net.Compilers.Toolset 4.14.0; see publication/BUILD.md.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {throw 'Use a new output directory; prior evidence is never overwritten.'}
New-Item -ItemType Directory -Force $output | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$argsBase=@('/nologo','/noconfig','/nostdlib+','/deterministic+','/optimize+','/platform:x64','/debug-','/langversion:5',('/pathmap:'+$PSScriptRoot+'=/src'))
foreach($reference in @('mscorlib','System','System.Core','System.Xml','System.Drawing','System.Windows.Forms','System.Runtime.Serialization')) {$argsBase+='/reference:'+(Join-Path $framework ($reference+'.dll'))}
$sources=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
& $compiler @argsBase /target:winexe (('/out:')+(Join-Path $output 'MinimalJoystick.exe')) @sources
if($LASTEXITCODE){throw 'Deterministic app compilation failed.'}
foreach($entry in @(@('driver/DeviceSetup.cs','DeviceSetup.exe'),@('tools/InspectController.cs','InspectController.exe'))) {
    & $compiler @argsBase /target:exe (('/out:')+(Join-Path $output $entry[1])) (Join-Path $PSScriptRoot $entry[0]) (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
    if($LASTEXITCODE){throw "Compilation failed: $($entry[1])"}
}
Copy-Item -LiteralPath (Join-Path $output 'MinimalJoystick.exe') -Destination (Join-Path $output 'MinimalJoystickSetup.exe')
@{compilerPackage='Microsoft.Net.Compilers.Toolset';compilerVersion='4.14.0';compilerSha256=(Get-FileHash $compiler -Algorithm SHA256).Hash;framework='Windows .NET Framework 4.x';sourceRootMappedTo='/src';signed=$false;runtimeTestsRun=$false}|ConvertTo-Json|Set-Content (Join-Path $output 'build-provenance.json')
Write-Output 'Built unsigned deterministic app/helpers. No signing, execution, driver installation or policy changes performed.'
