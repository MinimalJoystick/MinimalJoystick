# Software license scope

Approved by the owner on 2026-11-09: original MinimalJoystick desktop app, bridge, setup/helper code, own scripts/tests and independently created control art/mappings are available under the MIT license in `LICENSE`.

Exceptions:
- Microsoft originals under `vendor/vhidmini2/` retain their complete MS-PL license, notices and attribution.
- `driver/vhidmini.c`, `driver/vhidmini.h`, `driver/common.h`, `driver/util.c` and `driver/MinimalJoystick.inf` are Microsoft sample derivatives and remain MS-PL under `driver/LICENSE`. Regenerating them does not change that license.
- `driver/DeviceSetup.cs` is original project code, MIT; its location in `driver/` does not make it Microsoft-derived.
- Microsoft Windows/.NET runtime libraries and build tools are external dependencies, subject to their own terms; no relicensing or redistribution of their implementation is implied.

This license decision is limited to the desktop software. It does not license or publish the project's firmware, website, hardware CAD, purchased models, datasheets, incoming source files or historical release archives. See `publication/THIRD-PARTY-NOTICES.md` for provenance.

No SignPath approval, public signing identity or Microsoft certification is claimed. Repository publishing and application submission require a separate decision.
