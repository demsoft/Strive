# Recording check

A manual end-to-end check of recording with two real browsers (fake camera and microphone), not part of CI because it needs
the whole stack, a recorder with Chromium and a storage.

1. Start the stack with recording: `cd src && ./compose.sh --profile recording up -d --build`
2. Run the check (it uses the docker host network, so that `https://localhost` and the media server are reachable):

   ```bash
   docker run --rm --network host -v "$PWD/src/Tests/recording-check:/work" -w /work \
     mcr.microsoft.com/playwright:v1.63.0-noble sh -c "npm i playwright@1.63.0 --silent && node record.js"
   ```

It signs in as two users, starts a conference, records for about a minute, stops, waits until the recording is stored, checks
the recordings list, the share page (signed out visitors must be asked to sign in, signed in users can watch and seek) and
downloads the file to `recording.mp4` plus screenshots in the same folder. Inspect the file with ffprobe/ffmpeg, e.g.
`ffprobe recording.mp4` (one h264 1280x720 and one aac stream) and extract a frame to see what was recorded: the picture must
contain only the conference, no browser window and no token.
