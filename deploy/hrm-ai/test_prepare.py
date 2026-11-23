import argparse
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
spec = importlib.util.spec_from_file_location('prepare', Path(__file__).with_name('prepare.py'))
prepare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prepare)
spec2 = importlib.util.spec_from_file_location('release', Path(__file__).with_name('release.py'))
release = importlib.util.module_from_spec(spec2)
spec2.loader.exec_module(release)

class InstallationTests(unittest.TestCase):
    def args(self, output):
        return argparse.Namespace(meet='https://meet.example.com',api='https://meet-api.example.com',sfu='https://media.example.com',staff='https://hrm.example.com',candidate='https://careers.example.com',public_ip='8.8.8.8',turn_host='relay.example.com',media_port=41000,output=output)
    def test_contract_secrets_and_portability(self):
        with tempfile.TemporaryDirectory() as d:
            directory=prepare.prepare(self.args(str(Path(d)/'private')))
            release.validate(directory)
            config=json.loads((directory/'api.json').read_text())
            hrm=json.loads((directory/'hrm.merge.json').read_text())
            self.assertFalse(hrm['ChatCalls']['Enabled'])
            self.assertFalse(hrm['GosMeet']['RecruitmentEnabled'])
            self.assertFalse(config['KeyValueDatabase']['UseInMemory'])
            self.assertNotIn('KeyPrefix', config['KeyValueDatabase']['Redis'])
            self.assertGreaterEqual(len(config['HrmIntegration']['ServiceSecret']),48)
            self.assertNotEqual(config['HrmIntegration']['ServiceSecret'],config['HrmIntegration']['SessionSecret'])
            self.assertNotIn('/gos-meet-api',hrm['GosMeet']['ApiOrigin'])
            self.assertNotIn('173.208.144.83',(directory/'nginx.conf').read_text())
            with self.assertRaises(ValueError):prepare.prepare(self.args(str(directory)))
            config['HrmIntegration']['PortalOrigin']='https://evil.example.com'
            (directory/'api.json').write_text(json.dumps(config))
            with self.assertRaises(ValueError):release.validate(directory)
    def test_bad_origins_and_private_media_address_rejected(self):
        for url in ['http://example.com','https://example.com/path','https://user:secret@example.com','https://example.com/']:
            with self.assertRaises(ValueError):prepare.origin(url)
        with tempfile.TemporaryDirectory() as d:
            args=self.args(d);args.public_ip='127.0.0.1'
            with self.assertRaises(ValueError):prepare.prepare(args)
    def test_compose_has_persistence_and_both_media_protocols(self):
        source=Path(__file__).with_name('compose.yml').read_text()
        self.assertIn('${MEDIA_PORT:?}:${MEDIA_PORT:?}/tcp',source)
        self.assertIn('${MEDIA_PORT:?}:${MEDIA_PORT:?}/udp',source)
        self.assertIn('rabbit-data:/var/lib/rabbitmq',source)
        self.assertNotIn('173.208.144.83',source)

if __name__=='__main__':unittest.main()
