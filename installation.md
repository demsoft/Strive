https://github.com/DoTheEvo/Traefik-v2-examples

## In Strive/src folder
```bash
touch acme.json && chmod 600 acme.json

```

## Firewall
```bash
sudo apt install ufw
sudo ufw allow ssh comment 'allow SSH connections'
sudo ufw default deny incoming
sudo ufw default allow outgoing comment 'allow all outgoing traffic'
sudo ufw allow 80/tcp comment 'Strive HTTP'
sudo ufw allow 443/tcp comment 'Strive HTTPS'
sudo ufw allow 40000:49999/tcp comment 'Strive WebRTC'
sudo ufw allow 40000:49999/udp comment 'Strive WebRTC'
sudo ufw allow 3478 comment 'Strive TURN (UDP and TCP)'
sudo ufw allow 50000:59999/udp comment 'Strive TURN relay'
sudo ufw enable
```

`sudo ufw status verbose`

## TURN server (participants behind strict firewalls)

Participants whose network blocks the SFU's UDP/TCP ports (corporate networks, some mobile carriers, hotel WiFi) cannot
send or receive media directly. Strive ships a [coturn](https://github.com/coturn/coturn) TURN server that relays their
media. It is part of the compose files and needs two settings (`run_prod.sh` shows them):

| Variable | Description |
| --- | --- |
| `TURN_HOST` | Public DNS name or IP address of this server. Browsers connect to it on port 3478 (UDP and TCP). |
| `TURN_SECRET` | A long random secret. The API uses it to issue time-limited TURN credentials (valid 24 hours), coturn checks them with the same secret. Participants never see it. |
| `TURN_MIN_PORT` / `TURN_MAX_PORT` | UDP port range for relayed media (default `50000`-`59999` in `run_prod.sh`). It must not overlap with `MEDIASOUP_MIN_PORT`/`MEDIASOUP_MAX_PORT` and has to be open in the firewall (see above). |

Notes:
- Browsers open a relay for each of their two peer connections while they connect and drop it again if a direct connection
  works. Plan for two ports per participant in the worst case.
- If the server is behind a 1:1 NAT (AWS, Azure, ...) add `external-ip=<public-ip>/<private-ip>` to `src/coturn/turnserver.conf`.
- coturn does not relay to private networks (see `denied-peer-ip` in `src/coturn/turnserver.conf`), so the SFU has to be
  reachable on its public address (`ANNOUNCED_IP`).
- Some networks only allow HTTPS (port 443). To also offer `turns:` (TLS), provide a certificate in `turnserver.conf` and add the
  url to `Turn__Urls__2` of the `strive` service in `docker-compose.override.yml`.
- To disable TURN, remove the `Turn__*` entries. The API then returns no ICE servers.

To check a deployment, generate a temporary credential and test it with
[Trickle ICE](https://webrtc.github.io/samples/src/content/peerconnection/trickle-ice/) (TURN uri `turn:<TURN_HOST>:3478`):

```bash
TURN_USER="$(( $(date +%s) + 3600 )):check"
TURN_PASSWORD=$(printf '%s' "$TURN_USER" | openssl dgst -sha1 -hmac "$TURN_SECRET" -binary | base64)
echo "username: $TURN_USER"; echo "password: $TURN_PASSWORD"
```

A candidate of type `relay` in the result means that coturn, the secret and the firewall are set up correctly. Also join a
conference from a network that only allows TCP/443 or temporarily block UDP in your browser to see relayed media.
