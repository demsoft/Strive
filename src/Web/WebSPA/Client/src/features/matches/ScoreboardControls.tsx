import { Box, Button, ButtonGroup, IconButton, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import * as api from 'src/services/api/sessions';
import { Match, MatchPeriod } from 'src/services/api/sessions';

const PERIODS: MatchPeriod[] = ['upcoming', '1h', 'ht', '2h', 'ft'];

type Props = {
   sessionId: string;
   match: Match;
   onChanged: (match: Match) => void;
};

/** The organizer's buttons for the score and the period. Every press is sent at once. */
export default function ScoreboardControls({ sessionId, match, onChanged }: Props) {
   const { t } = useTranslation();
   const [busy, setBusy] = useState(false);
   const [failed, setFailed] = useState(false);

   const home = match.homeScore ?? 0;
   const away = match.awayScore ?? 0;
   const period: MatchPeriod = match.period ?? 'upcoming';

   const send = async (homeScore: number, awayScore: number, next: MatchPeriod) => {
      if (homeScore < 0 || awayScore < 0 || homeScore > 99 || awayScore > 99) return;
      setBusy(true);
      setFailed(false);
      try {
         onChanged(await api.setScoreboard(sessionId, homeScore, awayScore, next));
      } catch {
         setFailed(true);
      }
      setBusy(false);
   };

   const side = (name: string, score: number, change: (delta: number) => void, id: string) => (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
         <Typography variant="body2" noWrap sx={{ maxWidth: 110 }}>
            {name}
         </Typography>
         <IconButton size="small" id={`${id}-minus`} disabled={busy || score === 0} onClick={() => change(-1)}>
            <RemoveIcon fontSize="small" />
         </IconButton>
         <Typography sx={{ minWidth: 20, textAlign: 'center', fontWeight: 800 }}>{score}</Typography>
         <IconButton size="small" id={`${id}-plus`} disabled={busy} onClick={() => change(1)}>
            <AddIcon fontSize="small" />
         </IconButton>
      </Box>
   );

   return (
      <Box
         id="scoreboard-controls"
         sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1.5, px: 2, py: 1 }}
      >
         {side(match.homeTeam, home, (d) => send(home + d, away, period === 'upcoming' ? '1h' : period), 'score-home')}
         {side(match.awayTeam, away, (d) => send(home, away + d, period === 'upcoming' ? '1h' : period), 'score-away')}
         <ButtonGroup size="small" variant="outlined">
            {PERIODS.map((p) => (
               <Button
                  key={p}
                  id={`period-${p}`}
                  variant={p === period ? 'contained' : 'outlined'}
                  disabled={busy}
                  onClick={() => send(home, away, p)}
               >
                  {t(`matches.period_short_${p}`)}
               </Button>
            ))}
         </ButtonGroup>
         {failed && (
            <Typography color="error" variant="caption" id="scoreboard-failed">
               {t('matches.scoreboard_failed')}
            </Typography>
         )}
      </Box>
   );
}
