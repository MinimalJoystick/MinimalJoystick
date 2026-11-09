# Build and reproducibility scope

Current repository: https://github.com/MinimalJoystick/MinimalJoystick. Initial hosted verification passed on commit 6b58e255f368e4adcf67a7017f6c24eee06c4a22. USB 0.3 and subsequent updates require their own passing hosted run. The workflow now runs self, reliability and USB model checks with timeouts. No driver is installed in CI. Earlier preparation-status statements below are historical, not the current publication state.

`Build-OpenSource.ps1` builds app/setup/DeviceSetup/Windows capability probe with Microsoft.Net.Compilers.Toolset **4.14.0**, deterministic output, stable source path mapping, fixed assembly version and no code signing. Extract the pinned NuGet package and pass its directory as `-CompilerRoot`; use a new `-OutputDirectory` every time. Windows .NET Framework reference assemblies must be the same version for byte comparisons. The normal local Build-App.ps1 remains the prototype build and may use the locally approved signing identity.

Example from this project's root:

```powershell
.\software\Build-OpenSource.ps1 -CompilerRoot .\.toolchain\packages\roslyn -OutputDirectory .\tmp\oss-build-a
.\software\Build-OpenSource.ps1 -CompilerRoot .\.toolchain\packages\roslyn -OutputDirectory .\tmp\oss-build-b
```

Compare every unsigned EXE's SHA-256, not timestamped signatures. Matching builds on one host demonstrate repeatability under the recorded compiler/reference environment; they do not prove clean-room CI or cross-OS bit reproducibility. Build provenance records the compiler hash. A public CI runner and dependency lock must be reviewed and run before claiming a reproducible public release.

Driver build remains `Build-Driver.ps1`: MSVC 14.44.35207, SDK 10.0.26100.0, Microsoft.Windows.WDK.x64 NuGet 10.0.26100.6584, UMDF 2.15, x64. Its native toolchain bootstrap, version-resource metadata, binary reproducibility and unsigned clean-room CI still need qualification. Do not claim the driver is reproducible merely because the app is. The generator's exact source reproduction is tested separately.

Public source export is software-only. `tools/prepare_public_source.py` uses an allowlist and rejects sensitive/private artifacts; its generated manifest classifies licensing and hashes each included file. The owner approved MIT originals/MS-PL driver on 2026-11-09. The export remains a private publication candidate until publication is approved.

The prepared GitHub workflow lives at `.github/workflows/desktop-source-build.yml` in the project and exported repository. Actions are pinned to immutable revisions, checkout credentials are not persisted, repository permissions are read-only, and no signing secrets, releases or driver installation are involved. The compiler download is version- and SHA-256-pinned in BUILD-DEPENDENCIES.json and verified by Bootstrap-OpenSource.ps1. Test Pillow is version-pinned; hosted Windows/reference assemblies are recorded to make environment differences visible. The first actual GitHub run remains pending publication/access authorization.

Local verification: building from the exported repository in a different directory reproduced all four unsigned app/helper executable hashes exactly; see relocated-build-results.json. The workflow selects Python 3.12 explicitly to match the tested source-check interpreter generation. No hosted CI result or native-driver bit reproducibility is inferred from this local test.
