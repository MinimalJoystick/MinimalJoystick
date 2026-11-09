# Physical USB bridge — 0.3 development build

The Windows app now receives MJ1 packets from the USB bench firmware, calibrates the two axes and sends them through the **existing single MinimalJoystick virtual controller**. It uses the existing installed driver; no new driver identity, driver installation, trust or Windows policy change is needed. The final rebuilt main executable is currently blocked by Windows Application Control on this development PC. The source implementation and isolated host tests are complete; physical COM/simulator operation remains untested. Do not change security settings to force execution. Current candidate files are under release/Software-Candidate, while Software retains the prior published app.

## Use with an assembled board

1. Follow `../firmware/README.md` for battery-disconnected first bring-up and compatible firmware flashing. No board has been flashed in this development task.
2. Launch `build/MinimalJoystick.exe` and open **Physical USB / calibration**. Select the board's known COM port. No default port is selected for a new profile and no automatic probe opens other ports. The serial monitor must release the port first.
3. Choose **Connect diagnostics**. This opens only the selected port, at115200 with DTR enabled, and pauses gameplay buttons/rudder until calibration is applied. The last accepted throttle is held. Read angle, ADC, button bits, magnet status/AGC and battery diagnostics.
4. With fresh valid sensors, capture **resting center**, left stop, right stop, throttle idle and throttle full. The labels may be captured in any order; all five are required for a new calibration. Circular angle wrap is handled around the captured center. Reversed sensor/throttle mounting is supported; inversion checkboxes are an additional user preference.
5. Choose **Apply calibration**. Settings save atomically to `%LOCALAPPDATA%/MinimalJoystick/hardware-usb.json`, retaining `.bak` on replacement. Valid samples now feed the same Rz/Slider/four-button virtual controller. Simulation controls are disabled while hardware mode is selected. Hardware calibration bypasses the simulation profile, avoiding double calibration.
6. Check all axes/buttons in Windows controller test before testing simulator bindings. Close the app window to leave its independent bridge running; **Stop bridge** stops input. **Disconnect USB** returns to the simulated laboratory mode, so simulated controls may become active again. Stop the bridge when leaving the bench unattended.

Import/export applies only to the separate hardware profile. An imported calibration is not proof that a different unit has the same endpoints. Startup restores the last saved hardware mode/port; an invalid settings file leaves hardware mode off with a warning. The application never chooses a replacement COM port automatically. A bridge capability marker is bound to its heartbeat; an older background bridge is requested to stop gracefully before upgrade. Hardware connection is refused if a USB-capable bridge is not running.

## Failure and session policy

- Packet framing/version/length, CRC, raw ranges and reserved flags are checked before accepting input. Startup requires progressing packets across two host polls; one parked packet cannot arm gameplay input. A device timestamp that falls more than250ms behind host progress latches the session invalid.
- Angle, ADC, sensor configuration, magnet status and USB-power flags must be valid. Battery-gauge failure alone does not disable controls.
- No valid input, sensor fault, uncalibrated mode or more than250ms without a fresh packet: release buttons, center rudder and hold the last accepted throttle. The existing driver watchdog remains independent.
- Repeated/reversed sequence or non-progressing/reversed device uptime latches the stream invalid. Use **Connect diagnostics** to establish a new session and then reapply calibration. uint32 sequence/uptime wrap is accepted.
- Serial I/O failure releases controls and retries the same explicitly selected port every2seconds. It does not select another device or fall back to synthetic/Bluetooth input while hardware mode remains enabled.
- Excess serial backlog is discarded instead of treating queued readings as current. Incomplete packets are buffered; corrupted streams resynchronize. Host monotonic time determines freshness; UI capture uses the sample's age, including bridge stalls.

MJ1 is a development diagnostic protocol without an authenticated device/session identifier. An explicit COM choice is the current device association. Production identification/handshake and BLE remain future work. Bootloader reconnect/port renumbering, real USB scheduling, cable removal, sensor faults, polarity, charging and current consumption need actual hardware validation.

## Validation and commands

```powershell
./software/Build-App.ps1 -CompileOnly
./software/Test-UsbBridge.ps1
# From project root, with any existing bridge stopped:
./software/build/MinimalJoystick.exe --usb-test software/build/usb-tests.txt
./software/build/MinimalJoystick.exe --usb-driver-test software/build/usb-driver-test.txt
./software/build/MinimalJoystick.exe --bridge-test software/build/usb-bridge-regression.txt
```

The isolated USB test uses the exact firmware C++ encoder fixture and recorded packets, then exercises the production decoder, calibration, session/freshness policy, isolated user-only IPC and profile persistence. The driver test owns the bridge mutex and refuses to run alongside another bridge; it feeds12 recorded packets through the existing actual HID handle, then releases buttons/centers rudder. It does not open COM ports. See `VALIDATION-0.3.md` for measured results and remaining gates.

Serial API reference: [Microsoft SerialPort documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport?view=netframework-4.8.1). No new package dependency is introduced.
