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

## Accounts: sign up and sign in (optional)

By default the app runs in **demo mode**: anybody can sign in with a made up name and any password (this is what
development and the Cypress tests use). With **accounts** people create an account with an email address and a password, or
continue with Google. They confirm their email address, can reset a forgotten password and choose the name other
participants see (a Google account only offers its name as the default).

Accounts are stored in the MongoDB of the stack (database `strive-identity`). Conferences of demo identities are not carried
over: accounts get new ids, so a switch to accounts starts fresh.

### Development

```bash
cd src
cp .env.identity.example .env.identity      # git ignores it, never commit it
./compose.sh --profile recording up -d --build
```

`compose.sh` turns accounts on when `.env.identity` exists. The app then runs on **https://localtest.me**: Google does not
accept `*.localhost` as a redirect address, and `localtest.me` (with all subdomains) points to `127.0.0.1` without a hosts
entry. The development certificate is created again for it the first time (trust it once in the browser). Emails go to a local
inbox, [Mailpit](http://localhost:8025), unless you set `SMTP_HOST`.

### Sign in with Google

1. [Google Cloud console](https://console.cloud.google.com/apis/credentials) -> create a project, configure the OAuth consent
   screen (External, scopes `openid`, `email`, `profile`; add yourself as a test user while it is in *Testing*).
2. Credentials -> *Create credentials* -> *OAuth client ID* -> *Web application*:
   - Authorized JavaScript origins: `https://localtest.me`
   - Authorized redirect URIs: `https://identity.localtest.me/signin-google`
3. Put the client id and secret into `.env.identity` (`GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`) and run `compose.sh up -d`.

The "Continue with Google" button only shows when both values are set. In production use your own domain: the redirect
address is `https://identity.<your domain>/signin-google`.

### Settings

| Setting | Where | Meaning |
| --- | --- | --- |
| `ACCOUNTS_MODE` / `Accounts__Mode` | `identity-api` | `Demo` (default) or `Accounts`; `compose.sh` sets `Accounts` with `.env.identity` |
| `ACCOUNTS_ALLOWED_EMAIL_DOMAINS` | `.env.identity` | comma separated email domains that may create an account, empty for everybody. It applies to registration and the first Google sign in; people who have an account stay in |
| `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | `.env.identity` | Google sign in |
| `ACCOUNTS_REQUIRE_EMAIL_CONFIRMATION` | `.env.identity` | `false` (current default in compose): new accounts sign in at once; `true`: they must confirm their email address first |
| `BREVO_API_KEY` | `.env.identity` / `.env.production` | send the mails through the Brevo API instead of SMTP; `SMTP_FROM` must be a sender validated in Brevo, and the IP address of the server must be allowed in Brevo (Security → Authorised IPs) |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_TLS`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | `.env.identity` / `.env` | the mail server (confirmation and password reset). Without it nothing is sent, only logged, so password reset does not work |

### Things to know

- A Google account is linked to the account with the same email address when Google has verified it. An unconfirmed
  password registration for that address is taken over (its password is removed), so nobody can claim an address they do not own.
- Wrong passwords lock an account for 15 minutes after 5 attempts; sign in, registration and reset pages are rate limited per
  IP address. Confirmation links work for 24 hours, reset links for one hour, both only once.
- Passwords need at least 8 characters.
- With email confirmation on, registration, "forgot password" and "resend" answer the same way whether the email address has an account or not.
- The session cookies and anti-forgery keys are stored in MongoDB, so sign ins survive restarts and several instances.

## Recording (optional)

A moderator can record a conference. Recording is never automatic: it is started with the record button (after a
confirmation), everybody in the conference sees a REC indicator and gets a notice, and a conference can switch it off in its
settings (`recording.isEnabled`). The recording is stored for 30 days and shared with a link (`/r/<token>`) that needs a
sign in by default; a moderator can open it to everybody with the link or delete it.

It needs two things next to the normal services: the **recorder** (a headless browser plus ffmpeg, see
[src/Services/Recorder](src/Services/Recorder/README.md)) and an **S3-compatible storage** for the files.

### Start it

```bash
cd src
./compose.sh --profile recording up -d --build      # use --profile recording with down, logs etc. as well
```

Without the profile nothing of this runs and recording is hidden in the app.

### Storage: Cloudflare R2

1. Create an R2 bucket (e.g. `strive-recordings`, private) and an API token with *Object Read & Write* for that bucket.
2. Put the values into the file `src/.env.recording` (git ignores it, never commit it):

   ```
   R2_ACCOUNT_ID=...
   R2_ACCESS_KEY_ID=...
   R2_SECRET_ACCESS_KEY=...
   R2_BUCKET=strive-recordings
   ```

`compose.sh` uses R2 when this file exists. Without it a small local S3-compatible storage (`storage`, profile
`local-storage`) is started instead, which is fine to try recording on a development machine. Set `RECORDING_STORAGE=local`
to use it even if the R2 file exists. Any other S3-compatible store (AWS S3, ...) works by setting
`Recording__Storage__ServiceUrl`, `Bucket`, `AccessKeyId` and `SecretAccessKey` on the `strive` service and the matching
`STORAGE_*` variables on the `recorder` (see the recorder README).

The server deletes recordings after `Recording__RetentionDays` (default 30). The R2 token does not need permission to
manage bucket settings; if you want an additional safety net, add a lifecycle rule in the Cloudflare dashboard.

### Settings

| Setting | Where | Meaning |
| --- | --- | --- |
| `RECORDER_SHARED_SECRET`, `RECORDER_TOKEN_SECRET` | `.env` | secrets between server and recorder (>= 16 and >= 32 characters), change them in production |
| `Recording__Enabled` | `strive` service | the feature switch (set by `--profile recording`) |
| `Recording__RetentionDays`, `Recording__MaxDurationMinutes` | `strive` service | 30 days, 240 minutes |
| `MAX_CONCURRENT_RECORDINGS`, `VIDEO_*` | `recorder` service | capacity (about 1-2 CPU cores per recording) and quality (1280x720, 25 fps) |

### Things to know

- Only the main room is recorded.
- The recorder joins as a participant called "Recording" that can only receive; it is visible in the participant list.
- Every recording needs roughly 1-2 CPU cores and about 0.5-1.5 GB per hour of storage at the default quality.
- In development the recorder runs in the network of the docker host (see `docker-compose.dev.yml`), because its browser has
  to reach the media server on the announced address (`ANNOUNCED_IP`, `127.0.0.1`). In production, set `ANNOUNCED_IP`
  and the host names to real addresses and the recorder can use the docker network.
