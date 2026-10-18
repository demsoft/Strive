import SpeedIcon from '@mui/icons-material/Speed';
import { Alert, Box, Chip, IconButton, Popover, Tooltip, Typography } from '@mui/material';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import { selectMyParticipantId } from 'src/features/auth/selectors';
import { selectParticipants } from 'src/features/conference/selectors';
import { RootState } from 'src/store';
import { estimateOneWayMs, LatencyReport, LatencyVerdict, verdictFor } from '../estimate';
import useLatencyReporter from '../useLatencyReporter';

const VERDICT_COLOR: Record<LatencyVerdict, 'success' | 'warning' | 'error'> = {
   live: 'success',
   slow: 'warning',
   turns: 'error',
};

/**
 * For musicians (shown with Music mode): how good is my connection, and how long does the sound take between me and
 * each of the others? The numbers come from the browsers of both sides, they are an estimate, not a recording.
 */
export default function LatencyButton() {
   const { t } = useTranslation();
   const musicMode = useSelector((state: RootState) => Boolean(state.settings.obj.mic.musicMode));
   const myId = useSelector(selectMyParticipantId);
   const participants = useSelector(selectParticipants);
   const reports = useSelector((state: RootState) => state.latency.synchronized?.reports ?? {});
   const [anchor, setAnchor] = useState<HTMLElement | null>(null);

   // measuring runs with Music mode, also while the panel is closed: the others see the numbers
   const mine = useLatencyReporter(musicMode);

   if (!musicMode) return null;

   const others = Object.entries(reports).filter(([id]) => id !== myId);

   return (
      <>
         <Tooltip title={t('conference.latency.title')}>
            <IconButton
               id="latency-button"
               color="inherit"
               aria-label={t('conference.latency.title')}
               onClick={(e) => setAnchor(e.currentTarget)}
            >
               <SpeedIcon />
            </IconButton>
         </Tooltip>
         <Popover
            open={Boolean(anchor)}
            anchorEl={anchor}
            onClose={() => setAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            transformOrigin={{ vertical: 'top', horizontal: 'center' }}
         >
            <Box sx={{ p: 2, width: 340 }} id="latency-popover">
               <Typography variant="subtitle1" gutterBottom>
                  {t('conference.latency.title')}
               </Typography>

               {mine ? (
                  <Typography variant="body2" sx={{ mb: 2 }} id="latency-mine">
                     {t('conference.latency.mine', {
                        rtt: mine.rttMs,
                        buffer: mine.jitterBufferMs,
                        output: mine.outputMs,
                     })}
                  </Typography>
               ) : (
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                     {t('conference.latency.measuring')}
                  </Typography>
               )}

               {others.length === 0 && (
                  <Alert severity="info" sx={{ mb: 1 }}>
                     {t('conference.latency.nobody')}
                  </Alert>
               )}

               {mine &&
                  others.map(([id, other]) => {
                     const toThem = estimateOneWayMs(mine, other as LatencyReport);
                     const fromThem = estimateOneWayMs(other as LatencyReport, mine);
                     const worst = Math.max(toThem, fromThem);
                     return (
                        <Box
                           key={id}
                           className="latency-row"
                           sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.75 }}
                        >
                           <Box sx={{ flex: 1, minWidth: 0 }}>
                              <Typography noWrap>{participants[id]?.displayName ?? id}</Typography>
                              <Typography variant="caption" color="text.secondary">
                                 {t('conference.latency.both_ways', { to: toThem, from: fromThem })}
                              </Typography>
                           </Box>
                           <Chip
                              size="small"
                              color={VERDICT_COLOR[verdictFor(worst)]}
                              label={t(`conference.latency.verdict_${verdictFor(worst)}`)}
                           />
                        </Box>
                     );
                  })}

               <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1.5 }}>
                  {t('conference.latency.hint')}
               </Typography>
            </Box>
         </Popover>
      </>
   );
}
