import importlib.util,pathlib,unittest,xml.etree.ElementTree as ET

ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('app_installer',ROOT/'scripts/package-app-installer.py')
installer=importlib.util.module_from_spec(spec);spec.loader.exec_module(installer)

class InstallerArchitectures(unittest.TestCase):
    def test_apple_silicon_distribution(self):
        root=ET.fromstring(installer.distribution((ROOT/'packaging/app-installer/Distribution.xml').read_bytes(),'osx-arm64'))
        self.assertEqual(root.find('options').get('hostArchitectures'),'arm64')
        self.assertIn('Apple Silicon',root.findtext('title'))
        self.assertEqual(root.find('volume-check/allowed-os-versions/os-version').get('min'),'15.0')

    def test_intel_distribution(self):
        root=ET.fromstring(installer.distribution((ROOT/'packaging/app-installer/Distribution.xml').read_bytes(),'osx-x64'))
        self.assertEqual(root.find('options').get('hostArchitectures'),'x86_64')
        self.assertIn('Intel',root.findtext('title'))
        self.assertEqual(root.find('pkg-ref/must-close/app').get('id'),'org.rovunet.macos.development')
        self.assertIsNone(root.find('installation-check'))

    def test_unknown_architecture_is_not_packaged(self):
        with self.assertRaises(KeyError):installer.distribution((ROOT/'packaging/app-installer/Distribution.xml').read_bytes(),'osx-unknown')

if __name__=='__main__':unittest.main()
