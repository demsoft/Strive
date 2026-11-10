import { Box } from '@mui/material';
import React, { useCallback, useEffect, useState } from 'react';
import { useSelector } from 'react-redux';
import * as api from 'src/services/api/sessions';
import { LiveMatch, Match } from 'src/services/api/sessions';
import { RootState } from 'src/store';
import Scoreboard from './Scoreboard';
import ScoreboardControls from './ScoreboardControls';

/** How often the score is asked again in the room. */
export const SCOREBOARD_REFRESH_MS = 5000;

/**
 * The scoreboard on top of the room of a match, for everybody in it, kept up to date. The organizer also gets the
 * buttons to change it. A room that is not a match shows nothing.
 */
export default function RoomScoreboard() {
   const conferenceId = useSelector((state: RootState) => state.conference.conferenceId);
   const [live, setLive] = useState<LiveMatch | null>(null);

   const load = useCallback(async () => {
      if (!conferenceId) return;
      try {
         setLive(await api.fetchLiveMatch(conferenceId));
      } catch {
         // keep showing the last score
      }
   }, [conferenceId]);

   useEffect(() => {
      load();
      const timer = setInterval(load, SCOREBOARD_REFRESH_MS);
      return () => clearInterval(timer);
   }, [load]);

   if (!live) return null;

   return (
      <Box id="room-scoreboard" sx={{ px: { xs: 1, sm: 2 }, pt: 1 }}>
         <Scoreboard match={live.match} />
         {live.isHost && (
            <ScoreboardControls
               sessionId={live.sessionId}
               match={live.match}
               onChanged={(match: Match) => setLive({ ...live, match })}
            />
         )}
      </Box>
   );
}
