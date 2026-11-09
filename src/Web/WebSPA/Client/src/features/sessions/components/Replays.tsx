import { Box, Button, Typography } from '@mui/material';
import PlayCircleOutlinedIcon from '@mui/icons-material/PlayCircleOutlined';
import { DateTime } from 'luxon';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { brandKey } from 'src/brand-text';
import * as api from 'src/services/api/sessions';
import { Replay } from 'src/services/api/sessions';

const minutes = (seconds?: number | null) => (seconds ? Math.max(1, Math.round(seconds / 60)) : null);

/** The recordings of a session, for the host and the people with a ticket or a pass. Nothing is shown without any. */
export default function Replays({ sessionId }: { sessionId: string }) {
   const { t } = useTranslation();
   const [replays, setReplays] = useState<Replay[]>([]);
   const [playing, setPlaying] = useState<Replay | null>(null);

   useEffect(() => {
      // a person without access, or an error: no replays, and nothing to say about it
      api.fetchReplays(sessionId).then(setReplays, () => setReplays([]));
   }, [sessionId]);

   if (replays.length === 0) return null;

   return (
      <Box id="session-replays" sx={{ mt: 3 }}>
         <Typography sx={{ fontWeight: 700, mb: 1 }}>{t(brandKey('matches.replays_title'))}</Typography>
         {playing && (
            <video
               id="replay-player"
               src={playing.url}
               controls
               autoPlay
               playsInline
               style={{ width: '100%', maxHeight: '60vh', borderRadius: 12, backgroundColor: '#000' }}
            />
         )}
         {replays.map((replay) => (
            <Button
               key={replay.recordingId}
               className="replay"
               fullWidth
               color="inherit"
               startIcon={<PlayCircleOutlinedIcon />}
               onClick={() => setPlaying(replay)}
               sx={{ justifyContent: 'flex-start', mt: 1, textAlign: 'left' }}
            >
               {DateTime.fromISO(replay.startedAt).toLocaleString(DateTime.DATETIME_MED)}
               {minutes(replay.durationSeconds) !== null &&
                  ` · ${t('matches.replay_minutes', { count: minutes(replay.durationSeconds) })}`}
            </Button>
         ))}
      </Box>
   );
}
