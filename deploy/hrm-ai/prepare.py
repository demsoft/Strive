#!/usr/bin/env python3
"""Generate private, single-installation GOS Meet configuration. Never prints secrets."""
import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
from urllib.parse import urlsplit


def origin(value):
    u = urlsplit(value)
    if (u.scheme != 'https' or not u.hostname or u.username or u.password or
            u.path or u.query or u.fragment or value != 'https://' + u.netloc or
            not re.fullmatch(r'[A-Za-z0-9.-]+(?::[0-9]{1,5})?', u.netloc)):
        raise ValueError('Use an exact HTTPS origin, without path, credentials or trailing slash.')
    return value


def prepare(args):
    for name in ['meet', 'api', 'sfu', 'staff', 'candidate']:
        origin(getattr(args, name))
    if any(urlsplit(getattr(args, name)).port not in (None, 443) for name in ['meet', 'api', 'sfu']):
        raise ValueError('The bundled HTTPS proxy requires standard port443 for meeting/API/SFU origins.')
    if len({args.meet, args.api, args.sfu}) != 3:
        raise ValueError('Dedicated meeting web, API and SFU origins are required.')
    address = ipaddress.ip_address(args.public_ip)
    if address.version != 4 or address.is_loopback or address.is_unspecified or address.is_private:
        raise ValueError('A reachable public IPv4 address is required for the SFU and relay.')
    if not re.fullmatch(r'[A-Za-z0-9.-]+', args.turn_host):
        raise ValueError('TURN host must be a DNS hostname or IPv4, without scheme or port.')
    if not 1024 <= args.media_port <= 49000:
        raise ValueError('Choose a free media port between 1024 and 49000.')
    directory = Path(args.output).resolve()
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    if any(directory.iterdir()):
        raise ValueError('Output must be empty. Never overwrite existing installation keys.')
    os.chmod(directory, 0o700)
    keys = {name: secrets.token_hex(48) for name in ['service', 'session', 'sfu', 'sfu_api', 'rabbit', 'mongo', 'cache', 'turn']}
    instance = secrets.token_hex(16)
    def write(name, value):
        p = directory / name
        p.write_text(value, encoding='utf-8')
        os.chmod(p, 0o600)
    def json_file(name, value):
        write(name, json.dumps(value, indent=2) + '\n')
    urls = [f'turn:{args.turn_host}:3478?transport=udp', f'turn:{args.turn_host}:3478?transport=tcp', f'turns:{args.turn_host}:5349?transport=tcp']
    rabbit = f'amqp://hrm:{keys["rabbit"]}@rabbitmq:5672/hrm-meet'
    json_file('api.json', {
        'Authentication': {'Authority': args.meet + '/account', 'Issuer': args.meet + '/account'},
        'HrmIntegration': {'Enabled': True, 'ServiceSecret': keys['service'], 'SessionSecret': keys['session'],
            'PortalOrigin': args.staff, 'CandidatePortalOrigin': args.candidate, 'MeetingOrigin': args.meet},
        'Cors': {'AllowedOrigins': ','.join([args.meet, args.staff, args.candidate])},
        'MongoDb': {'ConnectionString': f'mongodb://hrm:{keys["mongo"]}@mongo:27017/?authSource=admin', 'DatabaseName': 'GosHrmMeet', 'CollectionNames': {'Conference': 'Conference'}},
        'RabbitMq': {'UseInMemory': False, 'RabbitMq': {'Host': 'rabbitmq://rabbitmq/hrm-meet', 'Username': 'hrm', 'Password': keys['rabbit']}},
        'KeyValueDatabase': {'UseInMemory': False, 'Redis': {'Hosts': [{'Host': 'cache', 'Port': 6379}], 'Password': keys['cache'], 'Database': 1, 'Name': 'GosHrmMeet'}},
        'SFU': {'UrlTemplate': args.sfu + '/{0}', 'ApiKey': keys['sfu_api'], 'TokenSecret': keys['sfu']},
        'Turn': {'Urls': urls, 'Secret': keys['turn'], 'CredentialLifetime': '02:05:00'},
        'Recording': {'Enabled': False}, 'Payments': {'Enabled': False}, 'Reminders': {'Enabled': False},
        'Serilog': {'MinimumLevel': {'Default': 'Warning'}},
    })
    json_file('web.json', {'App': {'FrontendUrl': args.meet, 'ConferenceUrl': args.api,
        'SignalrHubUrl': args.api + '/signalr', 'EquipmentSignalrHubUrl': args.api + '/equipment-signalr',
        'HrmPortalOrigin': args.staff, 'HrmCandidatePortalOrigin': args.candidate}})
    # Merge these fragments into the HRM server's existing protected settings, preserving all other sections.
    json_file('hrm.merge.json', {'GosMeet': {'Enabled': True, 'RecruitmentEnabled': False, 'ApiOrigin': args.api,
        'MeetingOrigin': args.meet, 'PortalOrigin': args.staff, 'CandidatePortalOrigin': args.candidate,
        'InstanceId': instance, 'ServiceSecret': keys['service']},
        'ChatCalls': {'Enabled': False, 'Turn': {'Urls': urls, 'Secret': keys['turn']}}})
    write('sfu.env', '\n'.join([f'API_CONFERENCE_TOKEN={keys["sfu_api"]}', f'API_TOKEN_SECRET={keys["sfu"]}',
        f'AMQP_CONNECTION_STRING={rabbit}', 'API_CONFERENCE_MANAGEMENT=http://api/v1/sfu/{conferenceId}',
        f'MEDIASOUP_ANNOUNCED_IP={args.public_ip}']) + '\n')
    write('mongo.env', f'MONGO_INITDB_ROOT_USERNAME=hrm\nMONGO_INITDB_ROOT_PASSWORD={keys["mongo"]}\n')
    write('rabbit.env', f'RABBITMQ_DEFAULT_USER=hrm\nRABBITMQ_DEFAULT_PASS={keys["rabbit"]}\nRABBITMQ_DEFAULT_VHOST=hrm-meet\n')
    write('redis.conf', f'bind 0.0.0.0\nprotected-mode yes\nrequirepass {keys["cache"]}\nappendonly yes\n')
    write('turnserver.conf', '\n'.join(['listening-port=3478', 'tls-listening-port=5349', 'fingerprint', 'use-auth-secret',
        f'static-auth-secret={keys["turn"]}', f'realm={args.turn_host}', f'external-ip={args.public_ip}',
        'min-port=49160', 'max-port=49260', 'cert=/certs/fullchain.pem', 'pkey=/certs/privkey.pem',
        'no-cli', 'no-multicast-peers', 'no-tlsv1', 'no-tlsv1_1', 'user-quota=12', 'total-quota=1200',
        'denied-peer-ip=0.0.0.0-0.255.255.255', 'denied-peer-ip=10.0.0.0-10.255.255.255',
        'denied-peer-ip=127.0.0.0-127.255.255.255', 'denied-peer-ip=169.254.0.0-169.254.255.255',
        'denied-peer-ip=172.16.0.0-172.31.255.255', 'denied-peer-ip=192.168.0.0-192.168.255.255',
        'denied-peer-ip=224.0.0.0-255.255.255.255', 'log-file=stdout', 'simple-log']) + '\n')
    # Dedicated HTTPS hosts avoid ambiguity between HRM's origin-only API contract and path prefixes.
    blocks = []
    for url, upstream in [(args.meet, 'web:80'), (args.api, 'api:80'), (args.sfu, 'sfu:3000')]:
        host = urlsplit(url).hostname
        blocks.append(f'''server {{
  listen 443 ssl; server_name {host};
  ssl_certificate /certs/fullchain.pem; ssl_certificate_key /certs/privkey.pem;
  ssl_protocols TLSv1.2 TLSv1.3;
  add_header Referrer-Policy no-referrer always;
  location / {{ proxy_pass http://{upstream}; proxy_http_version 1.1;
    proxy_set_header Host $host; proxy_set_header X-Forwarded-Proto https;
    proxy_set_header Upgrade $http_upgrade; proxy_set_header Connection $connection_upgrade;
    proxy_read_timeout 3600s; proxy_buffering off; }}
}}''')
    write('nginx.conf', 'events {}\nhttp {\nmap $http_upgrade $connection_upgrade { default upgrade; "" close; }\naccess_log off;\n' + '\n'.join(blocks) + '\n}\n')
    (directory / 'certs').mkdir(mode=0o700)
    write('installation.json', json.dumps({'meet': args.meet, 'api': args.api, 'sfu': args.sfu, 'staff': args.staff,
        'candidate': args.candidate, 'publicIp': args.public_ip, 'mediaPort': args.media_port, 'instanceId': instance}, indent=2) + '\n')
    write('compose.env', f'INSTALLATION={directory.as_posix()}\nMEDIA_PORT={args.media_port}\n')
    return directory


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for name in ['meet', 'api', 'sfu', 'staff', 'candidate', 'public-ip', 'turn-host', 'output']:
        p.add_argument('--' + name, required=True)
    p.add_argument('--media-port', type=int, default=41000)
    try:
        directory = prepare(p.parse_args())
        print('Private installation configuration created. Apply restricted filesystem ACLs; do not print or commit its contents.')
    except (ValueError, OSError) as e:
        p.exit(1, str(e) + '\n')
