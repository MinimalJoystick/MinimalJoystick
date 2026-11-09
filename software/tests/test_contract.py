"""Check HID report layout, generated-source drift and driver/app wire contract."""
from pathlib import Path
import re
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]

class ContractTests(unittest.TestCase):
    def test_generated_sources_reproducible(self):
        files = list((ROOT / 'driver').glob('*.h')) + [ROOT / 'driver/vhidmini.c', ROOT / 'driver/util.c', ROOT / 'driver/MinimalJoystick.inf']
        before = {p: p.read_bytes() for p in files}
        subprocess.run([sys.executable, str(ROOT / 'tools/prepare_driver.py')], check=True)
        self.assertEqual(before, {p: p.read_bytes() for p in files})

    def test_descriptor(self):
        source = (ROOT / 'driver/vhidmini.c').read_text()
        descriptor = source.split('G_DefaultReportDescriptor[] = {', 1)[1].split('};', 1)[0]
        descriptor = re.sub(r'//[^\n]*', '', descriptor)
        data = bytes(int(x, 16) for x in re.findall(r'0x([0-9A-Fa-f]+)', descriptor))
        pos = 0
        size = count = report = 0
        bits = {}
        tlcs = 0
        depth = 0
        usages = []
        page = None
        while pos < len(data):
            prefix = data[pos]; pos += 1
            n = (0, 1, 2, 4)[prefix & 3]
            value = int.from_bytes(data[pos:pos+n], 'little'); pos += n
            tag = prefix & 0xFC
            if tag == 0x04: page = value
            if tag == 0x08: usages.append((page,value))
            if tag == 0x74: size = value
            if tag == 0x94: count = value
            if tag == 0x84: report = value
            if tag == 0xA0:
                if depth == 0:
                    tlcs += 1
                    self.assertEqual(usages[-1], (1,4), 'one Joystick, no gamepad or second controller')
                depth += 1
            if tag == 0xC0: depth -= 1
            if tag in (0x80,0xB0,0x90): bits[(tag,report)] = bits.get((tag,report),0)+size*count
        self.assertEqual(tlcs, 1)
        self.assertEqual(depth, 0)
        self.assertEqual(bits, {(0x80,1):40, (0xB0,2):56})
        self.assertIn((1,0x35), usages) # rudder Rz
        self.assertIn((1,0x36), usages) # throttle Slider

    def test_identity_and_watchdog(self):
        header = (ROOT / 'driver/common.h').read_text()
        self.assertIn('L"MinimalJoystick"', header)
        self.assertIn('L"MJ-PROTOTYPE-0001"', header)
        driver = (ROOT / 'driver/vhidmini.c').read_text()
        self.assertNotIn('hidAttributes->ProductID     = controlInfo', driver)
        self.assertIn('LastFeed > 500', driver)
        self.assertNotIn('status = CheckRegistryForDescriptor(device);', driver)

if __name__ == '__main__': unittest.main()
