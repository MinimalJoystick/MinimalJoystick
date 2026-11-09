# MinimalJoystick

Windows software prototype for a flight-controller device, designed to expose one stable virtual joystick identity across USB/Bluetooth transport selection.

## Prototype status

Current desktop sources include simulated transports, rudder/throttle/button controls, an independent bridge process, profile/calibration controls and a custom UMDF2 driver. Actual USB/Bluetooth transport integration, simulator acceptance and public installer qualification remain pending. Windows 11 x64 is the initial driver target. No production firmware or hardware purchase is required to inspect/build these desktop sources.

## Build and test

See `software/publication/BUILD.md`. The read-only GitHub workflow builds the unsigned app/helpers twice, compares hashes, runs model tests and uploads clearly marked CI artifacts. It neither signs nor installs the driver. Its first hosted run is pending. Native driver binary reproducibility and clean-Windows acceptance are separate gates.

`software/Install-LocalPrototype.ps1` and the current setup screen are developer tools that import a local test certificate. They are not a public-release installer. Do not disable Windows protections or install a developer root to run an unsigned public download.

## Licenses

Original desktop code/tools/art: MIT (`software/LICENSE`). Microsoft sample and derived driver sources: MS-PL (`software/driver/LICENSE`, `software/vendor/vhidmini2/LICENSE`). Exact boundaries and third-party notices are in `software/LICENSING.md` and `software/publication/THIRD-PARTY-NOTICES.md`.

## Code signing policy

Signing application and policy drafts live under `software/publication/`. The project is **not currently accepted by SignPath Foundation** and has no public signing release. The Microsoft-sample driver lineage and DLL/catalog eligibility require explicit review. Never treat a CI build artifact as a signed/certified release.

## Privacy and feedback

The local simulator does not intentionally upload control input or telemetry. Build dependency downloads and any future signing submission contact their named providers. Do not put private logs, account credentials or identity documents in public issues. Maintainer contact and named release approvers will be configured before public release.

Simulator image/default-binding files are drafts and are not installed automatically. For status and limitations, see the publication audit rather than inferring readiness from a build passing.
