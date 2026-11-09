from pathlib import Path
import re
import unittest
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
class SupportTests(unittest.TestCase):
    def test_image_map_matches_assets(self):
        folder=ROOT/'simulator-support/X-Plane'
        text=(folder/'MinimalJoystick-Windows.joy').read_text()
        self.assertTrue(text.startswith('I\n1100 version\nJOY\n'))
        self.assertEqual(text.count('Name: MinimalJoystick'),1)
        image=re.search(r'^Image: (.+)$',text,re.M).group(1)
        with Image.open(folder/image) as im:
            self.assertEqual(im.size,(640,540))
            for x,y in re.findall(r'^(?:Axis|Button) \d+ \([^\n]+\): (\d+) (\d+)$',text,re.M):
                self.assertLess(int(x),im.width);self.assertLess(int(y),im.height)
        self.assertEqual(len(re.findall(r'^Button \d+ \(',text,re.M)),4)
        self.assertEqual(len(re.findall(r'^Axis \d+ \(',text,re.M)),2)
    def test_no_automatic_simulator_modification(self):
        self.assertFalse(any((ROOT/'simulator-support').glob('*.exe')))
        self.assertIn('not installed',(ROOT/'simulator-support/README.md').read_text())
if __name__=='__main__':unittest.main()
