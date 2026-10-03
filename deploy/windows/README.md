# Strive on a Windows server (Docker Desktop + IIS)

Production setup for one Windows Server 2022 machine that runs Docker Desktop (WSL2) and IIS (which keeps serving its other
sites). Domain in the examples: `goserp.co.uk`, public IP `173.208.144.83`.

```
browser ──https──► IIS :443 (wildcard certificate) ──► 127.0.0.1:5001-5004 ──► containers
        │                                                  meet → webspa      identity → identity-api
        │                                                  api → strive       sfu → sfu (signaling)
        └──media (UDP, TCP 40000-40003, no proxy)──────► Docker Desktop ──► sfu (mediasoup)
```

| Hostname | What | Goes to |
| --- | --- | --- |
| `meet.goserp.co.uk` | the web app | container `webspa`, 127.0.0.1:5003 |
| `api.goserp.co.uk` | conference API and SignalR | `strive`, 127.0.0.1:5002 |
| `identity.goserp.co.uk` | sign in, Google, accounts | `identity-api`, 127.0.0.1:5001 |
| `sfu.goserp.co.uk` | media server signaling (websocket) | `sfu`, 127.0.0.1:5004 |

The media does not go through IIS: the media server publishes **UDP and TCP 40000-40003** (one port per worker) on the public
IP. The other services (MongoDB, RabbitMQ) are not published at all.

Not in the first release: **TURN** (coturn behind Docker Desktop does not see the client addresses). People on networks that
block UDP still connect through the TCP media port.

## 1. Before you start

- [ ] DNS: `A` records for `meet`, `api`, `identity` and `sfu` (a wildcard `*.goserp.co.uk` is enough) → `173.208.144.83`.
- [ ] Firewall of the hosting provider (if there is one): inbound **UDP and TCP 40000-40003** to the server. 80/443 are open
      already (IIS).
- [ ] Docker Desktop is installed and **starts without a person logging in** (see "Boot" below).
- [ ] IIS modules (once, they do not change sites that do not use them; the install can restart IIS for a few seconds):
  - URL Rewrite: https://www.iis.net/downloads/microsoft/url-rewrite
  - Application Request Routing 3.0: https://www.iis.net/downloads/microsoft/application-request-routing
- [ ] A Brevo account: an API key (Brevo → SMTP & API → API keys) and the sender you will use (`SMTP_FROM`) validated in Brevo
      (a single sender address, or a verified domain with SPF/DKIM records).
- [ ] Google Cloud console → your OAuth client, add:
  - Authorized JavaScript origin: `https://meet.goserp.co.uk`
  - Authorized redirect URI: `https://identity.goserp.co.uk/signin-google`
  - OAuth consent screen → *Publish app* (status "In production"), otherwise only the listed test users can sign in.

## 2. Install

In PowerShell (run as administrator where noted):

```powershell
git clone https://github.com/demsoft/Strive D:\Deployment\dev\Backend\Strive
cd D:\Deployment\dev\Backend\Strive
git checkout feature/accounts          # until it is merged into develop

cd deploy\windows
.\New-ProductionEnv.ps1 -Domain goserp.co.uk -PublicIp 173.208.144.83
notepad ..\..\src\.env.production        # fill in GOOGLE_*, BREVO_API_KEY, SMTP_FROM (and RECORDING_* for recording)

.\Setup-Firewall.ps1                     # administrator
.\strive.ps1 up                          # builds the images (10-20 minutes the first time) and starts
.\Setup-Iis.ps1                          # administrator, after the modules are installed
.\Install-StartupTask.ps1                # administrator
```

`src\.env.production` holds all secrets and is ignored by git. Back it up somewhere safe (a password manager): losing it signs
everybody out.

## 3. Check it

1. https://identity.goserp.co.uk/.well-known/openid-configuration shows JSON with `"issuer":"https://identity.goserp.co.uk"`.
2. https://meet.goserp.co.uk redirects to the sign in page → create an account, or "Continue with Google".
3. Start a conference and join it from a second device on **another network** (a phone on mobile data). Camera and
   microphone must work in both directions.
4. In Chrome open `chrome://webrtc-internals` during the call → the selected candidate pair should be to
   `173.208.144.83:4000x` (udp). `tcp` means UDP is blocked somewhere on the way (provider firewall?).
5. Recording (if `RECORDING_ENABLED=true`): the record button appears for the moderator; stop it and open the recording.

## 4. Everyday

```powershell
.\strive.ps1 ps                    # status
.\strive.ps1 logs strive           # logs of a service
.\strive.ps1 restart sfu
git pull; .\strive.ps1 up          # update
```

Backup of the accounts and conferences (MongoDB):

```powershell
docker exec strive-prod-nosqldata-1 mongodump --archive --gzip > D:\Backups\strive-$(Get-Date -f yyyyMMdd).gz
```

## Boot

Docker Desktop normally starts when a user logs in. On a server that restarts by itself:
- Docker Desktop → Settings → General → *Start Docker Desktop when you sign in*, and let Windows sign that user in
  automatically (Sysinternals *Autologon*), **or** run Docker Engine as a service instead of Docker Desktop.
- `Install-StartupTask.ps1` then waits for the Docker engine and starts the stack; the containers also have
  `restart: unless-stopped`.
- Test it once with a reboot at a quiet moment.

## Things to know

- **Certificate renewal**: IIS holds the wildcard certificate, nothing in Docker needs it. When win-acme renews it, run
  `.\Setup-Iis.ps1` again (it binds the newest certificate) or let win-acme update the bindings of `strive-proxy`.
- **Other IIS sites are not touched.** Everything of Strive is in the one new site `strive-proxy` (bindings for the four
  hostnames only). To remove Strive from IIS delete that site.
- **Ports**: if you change `MEDIASOUP_NUM_WORKERS`, `MEDIASOUP_MAX_PORT` must be `MIN + workers - 1`; update the firewall
  rules (`Setup-Firewall.ps1`). 4 workers are plenty for the number of people one server can carry.
- **Email**: `ACCOUNTS_REQUIRE_EMAIL_CONFIRMATION=false` lets people in without confirming their address. Switch it to `true`
  once the Brevo mails arrive (send yourself a "forgot password" mail to test).
- **Recorder** (optional): needs `RECORDING_*` for Cloudflare R2 (the same values as `src\.env.recording` on the development
  machine: endpoint `https://<account id>.r2.cloudflarestorage.com`) and about 1-2 CPU cores per recording. The recorder's
  browser reaches the public hostnames and the media ports of this same server; if it cannot, `.\strive.ps1 logs recorder` shows it.

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| After sign in the browser lands on `identity.…/authentication/callback` (404) | ARR rewrites redirect headers: run `.\Setup-Iis.ps1` again (sets `reverseRewriteHostInResponseHeaders` to False) |
| Page loads, "Authentication failed" | `identity.` hostname not reachable from the browser, or Google redirect URI missing |
| Sign in works, call shows no remote video | media ports blocked (provider or Windows firewall); check `chrome://webrtc-internals` |
| Works on Wi-Fi, not on mobile data | UDP blocked by the mobile network; TCP media port 4000x must be reachable too |
| WebSocket errors on `api.`/`sfu.` | WebSocket feature missing in IIS (`Install-WindowsFeature Web-WebSockets`) or ARR proxy not enabled |
| `502`/`503` from IIS | containers not running: `.\strive.ps1 ps` |
