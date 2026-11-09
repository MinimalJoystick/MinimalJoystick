# USB bridge 0.3 — 2026-11-09

Implemented: USB MJ1 reception, separate physical calibration, circular rudder mapping, fault/stale/session handling, bridge integration and a physical USB calibration screen. This is a development build, not a physical acceptance record.

Passed on this PC:

- App compiled and locally signed with the existing approved development certificate. An earlier iteration ran its self-test under the unchanged Windows policy, but the FINAL rebuilt main executable is again blocked by Application Control. The final isolated host test executable runs normally; it does not launch or load the blocked app. No enforcement setting was changed.
- 2,488 USB host checks: actual firmware C++ fixture; all packet split points; every single-bit packet corruption and resynchronization; invalid ranges/flags; circular endpoints, reversed mounting, inversion, invalid calibration, atomic profile backup; source freshness, sensor faults, reset/replay latch and uint32 wrap; isolated user-only hardware IPC; 1,000 recorded packets decoded into calibrated control states.
- An earlier iteration (before final startup/freshness/capability guards) passed12 firmware-format packets decoded/calibrated and round-tripped through the actual existing single MinimalJoystick HID handle. No COM device or simulator was opened. No driver was installed or replaced.
- The same earlier iteration passed the independent bridge regression: UI-heartbeat loss/button release,30 actual HID USB/BLE simulated handovers, frozen packets, forced crash/restart and clean stop.
- Earlier app self-test and seven Python descriptor/provenance/publication/support checks passed. The final native Windows runtime gate remains BLOCKED, not passed.
- New calibration screen opened and inspected at the PC's150% scale; all fields and actions fit. Capture with no fresh valid sensors correctly displayed a rejection warning. Button contrast and pre-existing broken main-window text were corrected during inspection.

Pending: actual board enumeration/USB packet timing; real sensor endpoint/noise calibration and saved-profile use; real USB disconnect/reset/reconnect/backlog behavior; physical live captures and import/export acceptance; simulator bindings and latency; reboot; BLE; battery operation and production USB identity. The recorded-packet HID test does not establish those results.

Build/source hashes and the final isolated test receipt are under build/app-build-sources.json and build/usb-host-validation.json. The release uses -UsbBridgeCandidate to preserve the prior published app while packaging current sources/binary as Software-Candidate.

No board flashed, battery connected, manufacturing order placed, driver installed/replaced, certificate trust added or Windows security setting changed. UI test bridge was stopped afterward.
