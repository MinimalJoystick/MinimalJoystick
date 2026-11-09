# USB/Bluetooth transport contract v2 — design and reference model

Status: designed and modeled, not a working Bluetooth transport. Existing USB MJ1 reception is unchanged. The reference core in TransportContract.cs does not open a radio, USB port or HID handle. No firmware is changed or flashed by this work.

## One gameplay endpoint

The physical unit exposes USB CDC and a custom BLE GATT service, with no physical HID joystick collection on either link. A per-user bridge owns the one existing MinimalJoystick virtual HID. The driver/product/serial/axis order never depends on the active transport. Pairing or selecting USB must never install a second virtual controller. Multi-unit support is out of scope: one selected physical unit feeds this virtual controller.

An immutable random 128-bit device UUID is provisioned once and stored in firmware settings. Use that UUID, not the BLE name, COM number or privacy-changing radio address, to match USB and BLE. Pairing is user initiated; the app stores the Windows device identifier and verified UUID. Automatic handover is enabled only when both links report the selected UUID and supported protocol. Current USB MJ1 has no identity handshake, so it cannot safely auto-match a BLE device yet. Do not infer identity from its prototype VID/PID.

## GATT and pairing

Project-private service UUID: 1d930fd0-9b15-4b2c-8b7f-f14e1964da10. Project-private characteristic UUIDs use the same prefix with final da11 (DeviceInfo, read), da12 (Motion, notify) and da13 (Session, read). These are proposed project UUIDs, not Bluetooth SIG allocations.

DeviceInfo is 20 bytes: major/minor (1 byte each), device UUID (16 canonical RFC 4122 network-order bytes), capability mask (LE16; bit0 motion-v2, bit1 battery). Session returns the current nonzero 32-bit boot nonce. Read both after connecting; reject unsupported major versions, missing capabilities, zero/mismatched identity or any nonce change until a new handshake. The nonce distinguishes boots; it is not authentication.

Use encrypted, bonded BLE connections with LE Secure Connections where supported. With no display/keypad, Just Works bonding does not provide authenticated MITM protection. Require a deliberate physical pairing window (proposed: hold gear plus center brake for 3 seconds while idle; final mechanical/firmware review pending), advertise for 60 seconds, and suppress that chord from gameplay. Store a small bounded bond list, reconnect only to the selected bond and provide an explicit physical reset-bonds action. Never silently delete bonds or pair to a same-named nearby device. Test privacy-address changes, bond loss, wrong-device and adapter-off cases.

The first Windows adapter should be a small C++/WinRT helper using BluetoothLEDevice, GATT service discovery, Session/DeviceInfo reads and Motion notification subscription. It reports samples through user-scoped IPC to the existing .NET Framework bridge. This keeps Windows WinRT interop out of the current C# 5 build. The helper is a normal user process, not another driver/service/controller. Bound messages carry verified UUID/session and a helper-monotonic receive timestamp. Validate freshness again in the bridge; never stamp a queued sample as newly received. Cancellation, limited reconnect backoff (1/2/4/8 seconds, capped at 8) and explicit disconnect are required. The helper and native radio integration are future implementation work.

## Motion v2, exactly 20 bytes

All multibyte numeric fields are little-endian. This fits a notification value at the default 23-byte ATT MTU without fragmentation. Target 50 Hz; latency, actual connection intervals and battery life must be measured on hardware.

| Offset | Bytes | Meaning |
|---|---:|---|
| 0 | 1 | Version = 2 |
| 1 | 1 | Valid flags: angle bit0, ADC bit1, magnet bit2; upper bits zero |
| 2 | 2 | Sequence, modulo 65536 |
| 4 | 4 | Nonzero boot/session nonce |
| 8 | 4 | Device uptime milliseconds, modulo 2^32 |
| 12 | 2 | Raw rudder angle, 0..4095 |
| 14 | 2 | Raw throttle ADC, 0..4095 |
| 16 | 1 | Four button bits; upper bits zero |
| 17 | 1 | Battery 0..100%, or 255 unknown |
| 18 | 2 | CRC-16/CCITT-FALSE over bytes 0..17, initial 0xffff, polynomial 0x1021 |

CRC detects corruption and framing errors; BLE encryption/bonding supplies link security. USB v2 needs an explicit framing envelope and DeviceInfo/Session query responses before replacing the current MJ1 stream. Do not parse v2 as MJ1 or change the HID report ABI. Calibration belongs to the selected device, is applied exactly once before arbitration, and is independent of simulator bindings. Preserve current COM-based profiles until an explicit migration associates them with a verified UUID.

## Selection and failure behavior

Two progressing samples across host polls are required to qualify each link. USB has priority only while selected-unit identity, calibration, sensors and samples are valid. Warm BLE standby keeps its live sequence/freshness state; no old button edges are synthesized on a switch. If USB becomes stale/invalid and BLE is ready, use BLE; otherwise release buttons, center rudder and retain the last accepted throttle. Show the reason and require the user to acknowledge any future change to throttle failure policy.

Freshness uses host monotonic time with a 250 ms limit, independently of Windows connection status. Duplicate packets do not refresh it. Forward modular sequence/uptime wrap is accepted; replay/reset, a nonce change or queued device time falling more than 250 ms behind host progress latches that channel invalid until a new handshake. Disconnect releases that link immediately. A returning USB link must qualify before reclaiming priority. Never fall back to simulation from a physical session. Keep the existing independent HID watchdog.

A successful handshake for a new boot nonce invalidates the other link's old boot immediately, even if queued packets from that link would otherwise appear fresh. The new link still needs two progressing samples before gameplay can resume.

Firmware follow-up: keep the sensor rail active during BLE gameplay, implement battery-safe idle/sleep/wake, retain bond/device identity across firmware updates and bound flash writes. Existing bench firmware powers sensors only on USB VBUS and is not qualified for battery BLE operation. Radio, charging/current draw, antenna placement, actual unplug handover and long-duration battery tests remain hardware gates. Firmware update transport is separate; do not add wireless DFU to the first gameplay milestone.

## Acceptance sequence

1. Reference codec/arbitration tests and shared golden vectors.
2. Firmware GATT implementation and Windows helper, separately tested with a BLE peripheral simulator/dev board.
3. USB v2 identity handshake and explicit calibration migration.
4. One unit on both links: USB priority, cable removal, reconnect, stale/replay/fault, wrong unit, PC sleep/adapter reset.
5. Check exactly one Windows controller and unchanged GUID/bindings across handovers, app restart and reboot.
6. Only then simulator acceptance and measured latency/battery targets. No model test substitutes for these gates.

Primary references: [Microsoft GATT client and notification APIs](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/gatt-client), [Bluetooth GATT default MTU](https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core-54/out/en/host/generic-attribute-profile--gatt-.html), [Nordic BLE bonding/privacy/security](https://docs.nordicsemi.com/r/bundle/nwp_057/page/wp/nwp_057/from_silicon_to_cloud/secure_comm_ble.html).
