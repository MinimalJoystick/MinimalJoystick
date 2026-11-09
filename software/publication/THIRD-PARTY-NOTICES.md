# Third-party notices and license boundaries

Owner-approved on 2026-11-09: MIT for originals, MS-PL for Microsoft derivatives. Public publication/signing remains pending.

| Component | Provenance | Source license / treatment |
|---|---|---|
| `vendor/vhidmini2/` | microsoft/Windows-driver-samples commit d49bb7d72f3651952b2b6e4ad28d14fe2247eb69 | MS-PL; retain complete upstream license and notices |
| `driver/vhidmini.c`, `vhidmini.h`, `common.h`, `util.c`, `MinimalJoystick.inf` | Generated modifications of that sample via `tools/prepare_driver.py` | MS-PL; do not label these MIT |
| App, bridge, profile/setup UI, DeviceSetup helper and own tools | Written for this project | MIT, approved by owner |
| Simulator control diagram and mappings | Project-generated schematic and independently written bindings | MIT, included in the approved software scope; no third-party product photos copied |
| .NET/Windows inbox frameworks | Microsoft OS components | System dependencies, not relicensed or bundled |
| MSVC, Windows SDK, WDK, Roslyn | Build-time Microsoft tools/packages | Do not copy development-tool installers/toolchain into source release; follow each package's terms |
| Pillow | Build-time PNG generation | Build dependency; package metadata identifies version; do not bundle its implementation |

No vJoy, ViGEm or libvirtualhid implementation is included. No legal conclusion about SignPath's acceptance of the MS-PL sample derivative is asserted. OSI lists MS-PL as approved: https://opensource.org/license/ms-pl

Hardware CAD, purchased models, datasheets, firmware, website, incoming user files and release archives are outside this software-only publication proposal. No project-wide license is applied to them.
