# Simulator support — draft, not installed

The X-Plane Windows package supplies a `.joy` device definition and a matching image map for the single MinimalJoystick identity. File syntax follows https://developer.x-plane.com/article/creating-joystick-configuration-joy-files/ and installed X-Plane examples. Axis indices, directions, command behavior, labels and image placement still require in-simulator validation. No simulator preferences or existing bindings are modified automatically.

For later testing, copy only `X-Plane/MinimalJoystick-Windows.joy` and `MinimalJoystick-controls.png` into `<X-Plane>/Resources/joystick configs/` after backing up any same-named files. Restart the simulator, select MinimalJoystick, calibrate, and test defaults. Remove those two files to undo this package; retain your normal simulator preference backups.

Draft mapping: Rz rudder, Slider throttle, gear toggle, left brake, regular brakes, right brake. Digital brake commands remain digital; no pressure sensing is claimed. Check each axis direction before flight. The supplied control image is a schematic rather than a manufacturing drawing.

MSFS and DCS have a reviewed mapping worksheet in `recommended-bindings.csv`; this is not an importable profile. Native MSFS profiles and aircraft-specific DCS files must be generated/validated in their respective tools. No universal cross-simulator import format is claimed.
