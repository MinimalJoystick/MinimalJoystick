# Desktop 0.4 candidate — validation scope

USB 0.3 source was published at bd84b96a2d79cb7198350fcd180a1c8ea4391e9c and passed hosted Windows workflow 42616875714: source/provenance checks, byte-identical repeat unsigned builds, self/reliability/USB model tests. This is not a physical USB, HID-driver-installation or simulator pass.

The Bluetooth v2 design is in BLUETOOTH-ARCHITECTURE.md. Its isolated reference core passed 30,171 checks locally, including corruption rejection, selected-unit binding, modular wrap, duplicate/stale/backlog handling and 10,000 modeled USB/BLE handovers. It is not integrated into the live bridge, and real BLE pairing/GATT/firmware are not implemented.

Desktop 0.4 adds responsive overview/device/laboratory/diagnostics pages, actual output-axis/button meters, explicit physical versus simulated source status, reported/stale battery labeling, a five-position calibration progress indicator, import/export plus explicit backup restore, and tray close/stop behavior. It does not change the HID descriptor, product name, serial or driver identity. The radio is never scanned automatically.

The updated signed app compiled with the existing development certificate. The exact updated sources passed all 2,488 isolated USB host checks. Its main executable was refused by host Application Control (WinError 4551) during device-free preview export. No host protection, trust store or driver was changed. Hosted source/runtime-model/UI-render checks for 0.4 must be assessed on their own public commit; 0.3 results are not substituted.

--ui-preview renders sample-data WinForms screens without creating bridge IPC, opening a COM port, pairing Bluetooth, opening HID, creating a tray icon or reading/saving user profiles. Those renders test appearance only. Live GUI/calibration persistence acceptance, driver installation/upgrade/removal, physical COM, real radio, battery operation, flight sims and reboot persistence remain pending. No flight simulator was opened and no PC restart was performed.
