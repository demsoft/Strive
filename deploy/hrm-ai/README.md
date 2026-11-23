# HRM Chat / GOS Meet release package

Use **demsoft/Strive → feature/goshrm-ai-improvements**. The deployment described here is separate from ordinary Strive accounts and does not modify the HRM SQL database or enable HRM features automatically.

## Choose the server topology

Provision three dedicated HTTPS origins for meeting UI, conference API and media control, plus a TURN DNS name. Supply the existing staff/candidate HTTPS origins. DNS must reach the intended server and certificates must cover the meeting/API/SFU/TURN names. No origin may contain a path prefix; HRM's ApiOrigin contract is origin-only. Do not reuse `/gos-meet-api` in that setting.

The default compose publishes API/web/SFU HTTP privately on loopback5102/5103/5104. Existing Windows IIS+ARR can terminate HTTPS and proxy each dedicated host to its corresponding port. Enable WebSockets, long-polling fallback, preserve Host/Origin/Authorization, disable API response caching and HTML error rewriting, and allow long-lived connections. Do not replace other IIS sites. These scripts do not edit IIS or certificates.

Alternatively, a Linux Docker host may use the optional `edge` profile on443. Do not enable it if IIS/nginx already owns443. The optional `relay` profile runs coturn; use a dedicated reachable Linux relay when Windows/Docker NAT cannot correctly expose relay traffic. Physical cross-network validation is required for either choice. Do not declare arbitrary NAT topologies supported without that test.

The compose intentionally runs one conference API and one SFU worker. Redis persists conference runtime, Mongo persists conferences and one-use handoffs, Rabbit persists messaging. Do not add a Redis KeyPrefix: the legacy Lua adapter treats some room/participant values as Redis keys, so prefix rewriting prevents room assignment. The dedicated cache/database provides isolation. Multiple API/SFU instances need separate conference ownership/routing/failover validation; do not use `--scale` as a shortcut. HRM's call signaling state and SignalR backplane use its existing protected session Redis and shared data-protection key ring, not this meeting cache.

## Prepare once (private configuration)

Python3 and Docker Compose v2 are required. Run from this repository, replacing all example values:

```sh
python3 deploy/hrm-ai/prepare.py \
  --meet https://meet.example.com --api https://meet-api.example.com \
  --sfu https://media.example.com --staff https://hrm.example.com \
  --candidate https://careers.example.com --turn-host relay.example.com \
  --public-ip YOUR_PUBLIC_IPV4 --media-port 41000 --output /secure/gos-meet
```

Use an empty output directory outside the repository. The generator creates matching independent service/session/SFU/relay/storage keys and a stable instance ID. It refuses to overwrite an existing installation. Preserve these keys during routine upgrades. On Windows, apply restricted NTFS ACLs granting only administrators and the necessary service/build identity access; POSIX chmod does not replace Windows ACLs. Never print expanded Compose config, commit these files, or paste secrets into Jenkins arguments/logs.

Copy valid PEM TLS chain/private key into the generated `certs/fullchain.pem` and `certs/privkey.pem` for optional edge/relay. Restrict file access while allowing the actual container/service identity to read. Renew certificates and reload edge/relay deliberately. Existing IIS may use its own certificate store, but TURN TLS still needs its certificate. No TLS verification bypass is provided.

## Firewall / routing

Open443 to the selected HTTPS entry point. Open **both UDP and TCP** for the configured SFU media port (default41000). For bundled TURN, expose3478 UDP/TCP,5349 TCP, and49160–49260 UDP, including provider firewall/NAT rules. Relay external IP and SFU announced IP must be the reachable public address. Media does not pass through ARR's ordinary HTTP proxy. Mongo/Rabbit/Redis, SFU health9000 and private HTTP5102–5104 must remain private. Restrict TURN egress/private-peer access; review any additional internal network ranges used at your site.

## Build and deploy an immutable release

Commit/push the matching source first and use its full40-character revision. In a clean checkout at that revision:

```sh
python3 deploy/hrm-ai/release.py validate --installation /secure/gos-meet --release FULL_SHA
python3 deploy/hrm-ai/release.py build --installation /secure/gos-meet --release FULL_SHA
python3 deploy/hrm-ai/release.py up --installation /secure/gos-meet --release FULL_SHA
```

Add `--edge --relay` to `up` only when those profiles are appropriate and their certificates/firewalls are ready. `check` repeats local API, web-config and SFU readiness. A failed readiness check exits nonzero and must block HRM feature enablement. It is not a substitute for authenticated media acceptance. The wrappers build.ps1/deploy.ps1 accept explicit `-Installation` and `-Release` for Windows; Jenkinsfile.hrm-ai keeps deployment off unless DEPLOY_APPROVED is selected. Its Git credential identifier must be mapped to the existing authorized Jenkins credential on a new server; do not invent/store a PAT in source.

Build images have immutable source tags. Record the deployed SHA/image digests, installation directory, DNS/port mapping and prior release. Do not build dirty files and label them with an unrelated revision. Start-at-boot must be configured for the chosen supported container host and verified with an actual reboot; containers use restart:unless-stopped.

## Connect HRM and release portal screens

Release the matching HRM backend, staff wrapper and candidate wrapper from their AI branches. Back up current protected HRM configuration. Deep-merge the generated `hrm.merge.json` sections into it; do not replace unrelated database, authentication, AI or deployment settings. Preserve app-pool read ACLs. Add the exact allowed portal origins to existing browser-session settings as required; keep existing scoped cookie/CSRF/session behavior.

`GosMeet.Enabled` can validate the connection; `GosMeet.RecruitmentEnabled` and `ChatCalls.Enabled` are deliberately generated **false**. After the matching portal wrappers and service pass the checks below, enable the agreed features in protected HRM configuration and restart only the owned HRM application using its normal release procedure. Do not call a successful source push a deployed feature. No new HRM SQL migration is part of this meeting bridge. However, the current combined HRM AI head also includes the separate leave change and pending migration `20261123110000_AddLeaveProjectSupervisor`. Review/apply that migration through the normal approved database release procedure before using the corresponding leave screens. This package neither applies it nor authorizes additional database changes. For Chat, verify existing Chat_ReadCursor/Chat_MessageDelivery via approved migration/status checks rather than reapplying ad hoc SQL.

For immediate Chat calls, the new HRM API requires its existing protected Redis connection/namespace and shared key ring; production refuses enabled calls without shared state and valid protected TURN configuration. It issues temporary coturn credentials (125minutes for a maximum two-hour call), never the relay master key. Browser refreshes those credentials when starting/accepting. SignalR call-change events use the same authenticated inbox groups/backplane; bounded reconciliation recovers dropped events. P2P media remains inside the app; the signaling server cannot forcibly stop a malicious peer's established P2P stream. If centrally enforced media removal is required, an SFU call path is a separate mandatory requirement.

## Acceptance before general availability

Use two authorized test staff accounts on the deployed HRM site. Check text send/reply, unread increment/clear, refresh/reconnect idempotency and normal notifications; test denied recipient and expired-session error messages. Start immediate audio/video in Chat and answer from another page. Check busy/decline/mute/camera/end, access-loss cleanup and API worker restart. Test real desktop and mobile browsers, Wi-Fi versus mobile data and forced TURN relay; verify selected WebRTC candidate pairs and received media. Local synthetic checks do not prove the site's firewall or camera permissions.

Schedule a permitted Online interview, retry unchanged scheduling and confirm one interview/room only. Every chosen candidate should receive their existing authenticated portal invitation. Staff/candidate should join their permitted interview without another GOS Meet login; wrong applicant/company, replayed ticket, expired session and outside join-window attempts must be denied. Leave must release devices. Verify UTC display across time zones and retained offline/external interview workflows. Do not use real applicant invitations as a diagnostic without authorization.

The source includes isolated tests for call state/races/protected Redis, relay credential compatibility, meeting authorization, browser handoff, and two-participant SFU generated-media acceptance. Run the source's focused suites before release, then complete these real-server checks. Agree measured concurrent-user/call-setup/media-quality targets before broader rollout. No video recording, Flutter call UI or OS background ringing is implied by this web release.

## Operations / rollback

Back up Mongo, Redis persistence, Rabbit data and the protected installation configuration with restricted access; test restoring into an isolated environment. Never publish those backups. Alert on readiness failure, auth/room denials, signaling failures, queue backlog, media loss and CPU/bandwidth/disk pressure. Logs must exclude credentials, tickets, SDP/ICE, chat contents and applicant details. Default edge access logging is off to avoid accidental query credential capture.

Disable the affected HRM flags first and drain sessions. Re-run `up` with the prior retained release SHA/images and the same installation configuration; do not regenerate identity keys. Restore matching HRM API/portal artifacts through their existing rollback procedure. `release.py stop` preserves data. Never run `down -v`, drop HRM tables, delete unrelated queues, or reset an existing normal Strive deployment. Verify sign-in, text Chat and offline interviews after rollback. Certificate rotation and server migration require the same media/network/identity acceptance, not just health endpoints.
