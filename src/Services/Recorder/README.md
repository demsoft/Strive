# Strive Recorder

Records a conference. A moderator starts a recording in the app (it is never started automatically and everybody in the
conference is told); the Strive server then asks this service to record it.

## How it works

1. The server calls `POST /recordings` with a one-recording join token and the storage key for the file.
2. The recorder starts a virtual screen (Xvfb) and a virtual sound card (PulseAudio), opens Chromium on
   `{WEB_URL}/c/{conferenceId}/recording#token=...` and waits until the recording view reports that it is connected.
   The recorder is a hidden-in-the-scene, receive-only participant called "Recording"; it can do nothing else.
3. ffmpeg captures the screen and the sound into a fragmented MP4 on disk (playable even if the process dies).
4. When the recording is stopped (by a moderator, the maximum duration, or because the conference closed), the file is
   made seekable (a stream copy, no re-encoding), uploaded to the S3-compatible storage (Cloudflare R2, AWS S3)
   as a multipart upload, and the result is reported to the server (`POST {STRIVE_API_URL}/internal/recorder/{id}/report`).
5. After a crash or restart the files left on the disk are uploaded and reported on startup.

Only the main room is recorded: the recorder is placed in the default room like any participant.

## Configuration (environment variables)

| Variable | Description |
| --- | --- |
| `RECORDER_SHARED_SECRET` | shared with the server (`Recording__Recorder__SharedSecret`), at least 16 characters |
| `STRIVE_API_URL` | where the server listens for reports, e.g. `http://strive` |
| `WEB_URL` | the web app the browser opens, e.g. `https://meet.example.com` |
| `STORAGE_BUCKET`, `STORAGE_ACCESS_KEY_ID`, `STORAGE_SECRET_ACCESS_KEY` | storage credentials (needs read and write for the bucket) |
| `STORAGE_ENDPOINT` | `https://<account id>.r2.cloudflarestorage.com` for R2, empty for AWS S3 |
| `STORAGE_REGION` (`auto`), `STORAGE_FORCE_PATH_STYLE` (`false`) | only for self-hosted gateways |
| `VIDEO_WIDTH`, `VIDEO_HEIGHT`, `VIDEO_FPS`, `VIDEO_CRF` | 1280, 720, 25, 26 |
| `MAX_CONCURRENT_RECORDINGS` | 2, every recording needs roughly 1-2 CPU cores |
| `WORK_DIR` | `/data/recordings`, mount a volume here |
| `IGNORE_HTTPS_ERRORS`, `HOST_RESOLVER_RULES` | development only (self-signed certificate, name mapping) |

The container needs `shm_size: 1gb` for Chromium.

## API (every call except `/health` needs the header `X-Recorder-Secret`)

- `POST /recordings` `{ recordingId, conferenceId, joinToken, storageKey, maxDurationMinutes }` → 202, 400 invalid, 409 duplicate, 503 busy
- `POST /recordings/{id}/stop` → 202 (idempotent)
- `GET /health`

## Development

`yarn test` runs the unit tests (no browser or ffmpeg needed, the operating system is replaced by fakes).
`yarn typecheck`, `yarn build`.
