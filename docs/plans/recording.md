# Plan: server-side meeting recording

Status: direction decided, Cloudinary spike done (see below), storage choice to be confirmed.

## Decisions so far

| Topic | Decision |
| --- | --- |
| Recording | server-side, a hidden receive-only recorder participant (headless Chromium + ffmpeg) renders the meeting in a recording layout and writes a file |
| Storage | Cloudinary in development and production, uploaded in chunks, behind `IRecordingStorage` so Cloudflare R2 (S3 API) can replace it |
| Fallback | if Cloudinary cannot take our file sizes, switch the production storage to R2 (config change plus the second adapter) |
| Sharing | a Strive link `/r/<token>`, not the raw storage URL |

## Recording is always opt-in

Nothing is ever recorded automatically. A moderator starts a recording on purpose for one meeting, everyone sees that it
is running (REC indicator, notice and sound), and the moderator stops it. Conferences can switch the feature off
completely in their settings, and the permission `recording/canManage` decides who may start and stop.

## Spike result (2026-10-03, real account, Free plan)

- The account reports `video_max_size_bytes = 104857600` (100 MB) in its media limits.
- A 51 MB video uploaded in chunks fine (43 s from the dev machine); a 164 MB video was rejected with
  `400 File size too large. Got 125829120. Maximum is 104857600` after the first chunks were already sent. Chunking does
  not lift the per-file limit.
- Private (`authenticated`) assets: the plain URL answers 401, a signed URL answers 206 with byte ranges, so playback and
  seeking work. An expiring download URL (`expires_at`) also works.
- The 25 GB is a shared credit pool: roughly 1 GB of storage or 1 GB of delivered video costs 1 credit, together with
  transformations. The account already used 6.7 % of it. Every time a recording is watched it consumes credits.

Conclusion: Cloudinary Free cannot store a normal meeting in one file (about 100 MB is 7-9 minutes at a typical 1.5-2.5
Mbps). It works only if recordings are cut into parts below 100 MB, or on a paid plan.

## Original step 0 (kept for reference)

Cloudinary documents a 100 MB per video limit on the Free plan, independent of chunking. Before building, with the real
account:

1. upload a ~150 MB and a ~600 MB test video with `upload_large` (resource type `video`, type `authenticated`),
2. check whether it is accepted, how long it takes, and what the account's actual limit is,
3. check how a time-limited or signed delivery URL for an `authenticated` asset behaves (expiry, revocation),
4. play the delivered file in a `<video>` element and seek in it.

Outcome: either Cloudinary is confirmed (continue) or R2 becomes the production adapter (build it in phase 2).

## Phases

1. **Recorder** (3-4 d): `Strive.Recorder` container with Chromium and ffmpeg, joins with a service token as a hidden
   participant, recording route in the web app (grid, or active speaker with screen share priority, no controls), writes
   fragmented MP4 to disk, stops cleanly, restarts safely.
2. **Storage** (1-2 d): `IRecordingStorage` (`Upload`, `GetPlaybackUrl`, `Delete`), Cloudinary adapter with chunked upload
   and retries, local file removed only after success, MinIO/R2 adapter if the spike requires it.
3. **Control and state** (1-2 d): `Recording` documents in MongoDB (status: starting, recording, uploading, ready,
   failed; owner; times; size), synchronized object so every client shows REC and a timer, hub methods start and stop,
   permission `recording/canManage` (moderators), notice to everyone when recording starts.
4. **Sharing and UI** (1-2 d): `/r/<token>` page and player, recordings list with copy link and delete, access rules
   (signed in by default, optional anyone with the link, optional expiry).
5. **Retention and hardening** (1 d): auto-delete after N days (also in storage), recorder failure handling, resource
   limits per recording, integration tests with a fake recorder and storage, e2e for start, stop and playback.

## Defaults I will use unless told otherwise

- Layout: active speaker with screen share given priority, 1280x720, 25 fps.
- Visibility: signed-in users with the link; moderators can switch a recording to anyone-with-the-link.
- Retention: 30 days, configurable per conference.
- Consent: banner and sound when recording starts, REC indicator for everyone, recorded participants listed in the
  recording record.

## Risks

- Cloudinary limits (spike), signed URL expiry on the Free plan (spike).
- Each running recording needs roughly 1-2 CPU cores for Chromium and ffmpeg.
- Chromium inside Docker on Apple silicon and Colima is slow in development; the recorder must be optional in the dev
  compose file.
