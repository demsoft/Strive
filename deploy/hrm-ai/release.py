#!/usr/bin/env python3
"""Build/deploy/check an explicit GOS Meet revision; never modifies HRM or enables its flags."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[2]


def validate(directory):
    def read(name):
        return json.loads((directory / name).read_text())
    manifest = read('installation.json')
    api, web, hrm = read('api.json'), read('web.json')['App'], read('hrm.merge.json')
    h = api['HrmIntegration']
    for name, expected in [('PortalOrigin', manifest['staff']), ('CandidatePortalOrigin', manifest['candidate']), ('MeetingOrigin', manifest['meet'])]:
        if h[name] != expected or hrm['GosMeet'][name] != expected:
            raise ValueError('Portal origins do not match the installation manifest.')
    if web['ConferenceUrl'] != manifest['api'] or hrm['GosMeet']['ApiOrigin'] != manifest['api']:
        raise ValueError('Conference API origins do not match.')
    if h['ServiceSecret'] != hrm['GosMeet']['ServiceSecret'] or min(len(h['ServiceSecret']), len(h['SessionSecret'])) < 48:
        raise ValueError('Protected service connection is incomplete.')
    if api['KeyValueDatabase']['UseInMemory'] or api['RabbitMq']['UseInMemory']:
        raise ValueError('Persistent meeting runtime and messaging are required.')
    if api['Turn']['Secret'] != hrm['ChatCalls']['Turn']['Secret']:
        raise ValueError('Relay keys do not match.')
    for name in ['api.json', 'web.json', 'hrm.merge.json', 'sfu.env', 'mongo.env', 'rabbit.env', 'redis.conf', 'turnserver.conf']:
        path = directory / name
        if not path.is_file() or (os.name != 'nt' and path.stat().st_mode & 0o077):
            raise ValueError('Protect generated configuration files with owner-only permissions.')
    return manifest


def main(args):
    if not re.fullmatch('[a-f0-9]{40}', args.release):
        raise ValueError('Use a full immutable source revision.')
    directory = Path(args.installation).resolve()
    validate(directory)
    environment = dict(os.environ, INSTALLATION=directory.as_posix(), RELEASE=args.release)
    manifest = json.loads((directory / 'installation.json').read_text())
    environment['MEDIA_PORT'] = str(manifest['mediaPort'])
    command = ['docker', 'compose', '-f', str(ROOT / 'deploy/hrm-ai/compose.yml')]
    for profile in ['edge', 'relay']:
        if getattr(args, profile):
            command += ['--profile', profile]
    def compose(*parts, capture=False):
        return subprocess.run(command + list(parts), env=environment, cwd=ROOT, check=True,
                              stdout=subprocess.PIPE if capture else None, text=True)
    compose('config', '--quiet')  # Never display expanded configuration/secrets.
    if args.action == 'validate':
        print('Configuration contracts and Compose validation passed; no services changed.')
        return
    if args.action == 'build':
        head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        if head != args.release or subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=normal'], cwd=ROOT, text=True).strip():
            raise ValueError('Build from the clean approved revision; do not build uncommitted changes as a release.')
        compose('build')
        return
    if args.action == 'stop':
        compose('stop')  # Preserve containers/volumes; no destructive down -v.
        return
    if args.action == 'up':
        if args.edge or args.relay:
            for name in ['fullchain.pem', 'privkey.pem']:
                if not (directory / 'certs' / name).is_file():
                    raise ValueError('Install the valid TLS certificate/key before starting HTTPS or TURN TLS.')
        compose('up', '-d', '--no-build')
    for attempt in range(60):
        try:
            for port, path in [(5102, '/health/ready'), (5103, '/config.js')]:
                with urllib.request.urlopen(f'http://127.0.0.1:{port}{path}', timeout=3) as result:
                    if result.status != 200:
                        raise ValueError('Readiness has not passed.')
            compose('exec', '-T', 'sfu', 'node', '-e', "fetch('http://127.0.0.1:9000/health').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))", capture=True)
            print('Conference API, web runtime configuration and SFU readiness passed. Complete authenticated and cross-network acceptance before enabling HRM flags.')
            return
        except Exception:
            time.sleep(2)
    raise ValueError('Readiness did not pass. Keep HRM flags disabled; inspect private service logs and restore the prior release.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['validate', 'build', 'up', 'check', 'stop'])
    parser.add_argument('--installation', required=True)
    parser.add_argument('--release', required=True)
    parser.add_argument('--edge', action='store_true')
    parser.add_argument('--relay', action='store_true')
    try:
        main(parser.parse_args())
    except (ValueError, OSError, subprocess.CalledProcessError):
        parser.exit(1, 'Release check failed. Keep integration flags disabled and inspect protected configuration or service logs.\n')
