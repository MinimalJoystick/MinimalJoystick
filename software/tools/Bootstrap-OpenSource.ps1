param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$softwareRoot=Split-Path $PSScriptRoot
$lock=Get-Content (Join-Path $softwareRoot 'publication/BUILD-DEPENDENCIES.json') -Raw | ConvertFrom-Json
$target=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $target){throw 'Use a fresh tool directory; no existing tools will be replaced.'}
New-Item -ItemType Directory -Force $target | Out-Null
$archive=Join-Path $target 'compiler.zip'
Invoke-WebRequest -Uri $lock.compiler.url -OutFile $archive
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.compiler.sha256){throw 'Compiler archive checksum mismatch; do not execute.'}
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $target 'compiler')
Write-Output (Join-Path $target 'compiler')
