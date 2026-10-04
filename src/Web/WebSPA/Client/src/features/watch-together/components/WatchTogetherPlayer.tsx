import { Box, Button, IconButton, Slider, Tooltip, Typography } from '@mui/material';
import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import React, { useContext, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import * as coreHub from 'src/core-hub';
import { expandToBox } from 'src/features/scenes/calculations';
import LayoutChildSizeContext from 'src/features/scenes/layout-child-size-context';
import { WatchTogetherSession } from 'src/store/signal/synchronization/synchronized-object-ids';
import { expectedPosition, planSync, PlayerSnapshot } from '../sync';
import useServerClock from '../useServerClock';
import { loadYouTubeApi, YouTubePlayer, YT_STATE } from '../youtube';

const SYNC_INTERVAL_MS = 1000;
/** events of the player that are caused by the synchronisation itself are not sent to the others */
const SUPPRESS_EVENTS_MS = 1500;
/** a jump of the position that is more than this (besides the normal progress) is a seek of the person controlling */
const SEEK_DETECTION_SECONDS = 1.5;
const BLOCKED_AUTOPLAY_AFTER_MS = 4000;

type Props = {
   session: WatchTogetherSession;
   /** may start, pause, seek and change the speed: the player shows the controls of YouTube and sends what is done */
   canControl: boolean;
};

const toSnapshot = (player: YouTubePlayer): PlayerSnapshot => {
   const state = player.getPlayerState();
   return {
      state:
         state === YT_STATE.playing
            ? 'playing'
            : state === YT_STATE.paused || state === YT_STATE.ended
              ? 'paused'
              : state === YT_STATE.buffering
                ? 'buffering'
                : 'idle',
      position: player.getCurrentTime(),
      rate: player.getPlaybackRate(),
   };
};

/**
 * The video that everybody watches. The server keeps where it is; every client follows that with its own YouTube
 * player. People that control it also send what they do in the player (play, pause, seek, speed).
 */
export default function WatchTogetherPlayer({ session, canControl }: Props) {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const serverNow = useServerClock();

   const size = useContext(LayoutChildSizeContext);
   const box = expandToBox({ width: 16, height: 9 }, size);

   const host = useRef<HTMLDivElement>(null);
   const playerRef = useRef<YouTubePlayer | null>(null);
   const sessionRef = useRef(session);
   sessionRef.current = session;
   const serverNowRef = useRef(serverNow);
   serverNowRef.current = serverNow;

   const suppressUntil = useRef(0);
   const lastObserved = useRef<{ position: number; at: number } | null>(null);
   const startedAt = useRef(Date.now());

   const [ready, setReady] = useState(false);
   const [error, setError] = useState<string | null>(null);
   const [blocked, setBlocked] = useState(false);
   const [volume, setVolume] = useState(100);
   const [muted, setMuted] = useState(false);

   const suppress = () => {
      suppressUntil.current = Date.now() + SUPPRESS_EVENTS_MS;
   };
   const isSuppressed = () => Date.now() < suppressUntil.current;

   const sendControl = (action: 'play' | 'pause' | 'seek' | 'setRate', player: YouTubePlayer) => {
      if (!canControl || isSuppressed()) return;

      dispatch(
         coreHub.controlWatchTogether({
            action,
            positionSeconds: action === 'setRate' ? undefined : Math.max(0, player.getCurrentTime()),
            rate: action === 'setRate' ? player.getPlaybackRate() : undefined,
         }),
      );
   };

   // create the player once
   useEffect(() => {
      let destroyed = false;
      let created: YouTubePlayer | null = null;

      loadYouTubeApi()
         .then((api) => {
            if (destroyed || !host.current) return;

            const current = sessionRef.current;
            const start = Math.floor(expectedPosition(current, serverNowRef.current()));
            suppress();
            created = new api.Player(host.current, {
               width: Math.max(1, box.width),
               height: Math.max(1, box.height),
               videoId: current.videoId,
               playerVars: {
                  autoplay: 1,
                  start,
                  controls: canControl ? 1 : 0,
                  disablekb: canControl ? 0 : 1,
                  fs: canControl ? 1 : 0,
                  rel: 0,
                  modestbranding: 1,
                  playsinline: 1,
                  iv_load_policy: 3,
                  origin: window.location.origin,
               },
               events: {
                  onReady: () => {
                     playerRef.current = created;
                     setReady(true);
                  },
                  onStateChange: (event) => {
                     if (!created || !canControl) return;
                     if (event.data === YT_STATE.playing) sendControl('play', created);
                     if (event.data === YT_STATE.paused) sendControl('pause', created);
                  },
                  onPlaybackRateChange: () => {
                     if (created) sendControl('setRate', created);
                  },
                  onError: () => setError(t('conference.watch_together.error')),
               },
            });
         })
         .catch(() => setError(t('conference.watch_together.error_youtube')));

      return () => {
         destroyed = true;
         playerRef.current = null;
         created?.destroy();
      };
      // the player is created again when the controls change (they cannot be switched on a running player)
   }, [canControl]);

   // another video
   useEffect(() => {
      const player = playerRef.current;
      if (!ready || !player) return;
      if (player.getVideoData().video_id === session.videoId) return;

      suppress();
      player.loadVideoById({
         videoId: session.videoId,
         startSeconds: Math.floor(expectedPosition(session, serverNow())),
      });
   }, [session.videoId, ready]);

   // follow the shared playback, and notice a seek of the person that controls the video
   useEffect(() => {
      if (!ready) return;

      const tick = () => {
         const player = playerRef.current;
         if (!player) return;

         const current = sessionRef.current;
         const snapshot = toSnapshot(player);
         const now = Date.now();

         // a seek in the player of a controller: the position jumped more than the video could have played
         if (canControl && !isSuppressed() && lastObserved.current) {
            const elapsed = (now - lastObserved.current.at) / 1000;
            const progress = snapshot.state === 'playing' ? elapsed * snapshot.rate : 0;
            if (Math.abs(snapshot.position - lastObserved.current.position - progress) > SEEK_DETECTION_SECONDS) {
               dispatch(
                  coreHub.controlWatchTogether({ action: 'seek', positionSeconds: Math.max(0, snapshot.position) }),
               );
               lastObserved.current = { position: snapshot.position, at: now };
               return;
            }
         }
         lastObserved.current = { position: snapshot.position, at: now };

         const commands = planSync(current, serverNowRef.current(), snapshot);
         if (commands.length > 0) suppress();
         for (const command of commands) {
            if (command.type === 'play') player.playVideo();
            else if (command.type === 'pause') player.pauseVideo();
            else if (command.type === 'seek') player.seekTo(command.to, true);
            else player.setPlaybackRate(command.rate);
         }

         // the browser may not let a video start by itself (no click on the page yet)
         setBlocked(
            current.state === 'playing' &&
               snapshot.state === 'idle' &&
               now - startedAt.current > BLOCKED_AUTOPLAY_AFTER_MS,
         );
      };

      const timer = setInterval(tick, SYNC_INTERVAL_MS);
      return () => clearInterval(timer);
   }, [ready, canControl]);

   useEffect(() => {
      playerRef.current?.setSize(Math.max(1, box.width), Math.max(1, box.height));
   }, [box.width, box.height, ready]);

   const handleVolume = (_: unknown, value: number | number[]) => {
      const v = Array.isArray(value) ? value[0] : value;
      setVolume(v);
      playerRef.current?.setVolume(v);
      if (v > 0 && muted) toggleMute();
   };

   const toggleMute = () => {
      const player = playerRef.current;
      if (!player) return;
      if (player.isMuted()) player.unMute();
      else player.mute();
      setMuted(player.isMuted());
   };

   const handleStartClick = () => {
      suppress();
      playerRef.current?.playVideo();
      setBlocked(false);
   };

   return (
      <Box
         id="watch-together-player"
         sx={{ position: 'relative', width: box.width, height: box.height, backgroundColor: '#000', borderRadius: 2, overflow: 'hidden' }}
      >
         <div ref={host} />

         {/* people that only watch cannot click into the video: pause, seek and speed belong to the host */}
         {!canControl && <Box sx={{ position: 'absolute', inset: 0 }} />}

         {error && (
            <Box sx={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', p: 2, textAlign: 'center' }}>
               <Typography>{error}</Typography>
            </Box>
         )}

         {blocked && !error && (
            <Box sx={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', backgroundColor: 'rgba(0,0,0,0.55)' }}>
               <Button id="watch-together-start" variant="contained" startIcon={<PlayArrowIcon />} onClick={handleStartClick}>
                  {t('conference.watch_together.click_to_play')}
               </Button>
            </Box>
         )}

         {!canControl && ready && (
            <Box
               sx={{
                  position: 'absolute',
                  left: 8,
                  bottom: 8,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 1,
                  px: 1,
                  borderRadius: 10,
                  backgroundColor: 'rgba(0,0,0,0.55)',
               }}
            >
               <Tooltip title={muted ? t('conference.watch_together.unmute') : t('conference.watch_together.mute')}>
                  <IconButton size="small" onClick={toggleMute} aria-label={t('conference.watch_together.mute')}>
                     {muted || volume === 0 ? <VolumeOffIcon fontSize="small" /> : <VolumeUpIcon fontSize="small" />}
                  </IconButton>
               </Tooltip>
               <Slider
                  size="small"
                  value={muted ? 0 : volume}
                  onChange={handleVolume}
                  aria-label={t('conference.watch_together.volume')}
                  sx={{ width: 90 }}
               />
            </Box>
         )}
      </Box>
   );
}
