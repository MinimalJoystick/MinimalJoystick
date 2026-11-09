import importlib.util
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('public_export',ROOT/'tools/prepare_public_source.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class PublicationTests(unittest.TestCase):
    def test_upstream_exact(self):self.assertEqual(len(module.check_upstream()),20)
    def test_export_boundaries(self):
        files=module.inventory()
        self.assertIn('AssemblyInfo.cs',files)
        self.assertIn('LICENSE',files)
        self.assertIn('LICENSING.md',files)
        self.assertNotIn('publication/LICENSE-MIT.proposed.txt',files)
        self.assertIn('vendor/vhidmini2/LICENSE',files)
        self.assertIn('driver/vhidmini.c',files)
        self.assertTrue(module.DERIVED.issubset(files), 'All generated driver sources/INF must be exported')
        for path in files:
            self.assertFalse(path.startswith(('build/','../','.git/')))
            self.assertNotIn('signing-cart',path)
            self.assertNotIn('Development-Environment',path)
            self.assertNotIn('install-transcript',path)
if __name__=='__main__':unittest.main()
