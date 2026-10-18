import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import { Box, Button, IconButton, MenuItem, Popover, Slider, TextField, Tooltip, Typography } from '@mui/material';
import AvTimerIcon from '@mui/icons-material/AvTimer';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import useServerClock from 'src/features/watch-together/useServerClock';
import usePermission from 'src/hooks/usePermission';
import { METRONOME_CAN_CONTROL } from 'src/permissions';
import { setMetronomeMuted, setMetronomeVolume } from 'src/features/settings/reducer';
import { selectMetronomeMuted, selectMetronomeVolume } from 'src/features/settings/selectors';
import { selectMetronomeSession } from '../selectors';
import { beatInBar, clampBpm, MAX_BEATS_PER_BAR, MAX_BPM, MIN_BEATS_PER_BAR, MIN_BPM } from '../schedule';
import useMetronomeClick from '../useMetronomeClick';

const DEFAULT_BPM = 100;
const DEFAULT_BEATS = 4;

/**
 * The shared click. Everybody hears it (this component plays it) and can change the volume on their own computer;
 * the people who may control it (the teachers) set the tempo and start and stop it.
 */
export default function MetronomeButton() {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const canControl = usePermission(METRONOME_CAN_CONTROL);
   const session = useSelector(selectMetronomeSession);
   const volume = useSelector(selectMetronomeVolume);
   const muted = useSelector(selectMetronomeMuted);
   const getServerNow = useServerClock();
   const beat = useMetronomeClick(session, getServerNow, volume, muted);

   const [anchor, setAnchor] = useState<HTMLElement | null>(null);
   const [bpm, setBpm] = useState(session?.bpm ?? DEFAULT_BPM);
   const [beatsPerBar, setBeatsPerBar] = useState(session?.beatsPerBar ?? DEFAULT_BEATS);

   // follow the tempo when somebody else changes it
   useEffect(() => {
      if (session) {
         setBpm(session.bpm);
         setBeatsPerBar(session.beatsPerBar);
      }
   }, [session?.bpm, session?.beatsPerBar]);

   // nothing to see for people that cannot control it, as long as no click runs
   if (!canControl && !session) return null;

   const label = session ? t('conference.metronome.running', { bpm: session.bpm }) : t('conference.metronome.title');
   const countingBeat = session && beat !== null ? beatInBar(beat, session.beatsPerBar) : null;

   const start = () => dispatch(coreHub.startMetronome({ bpm: clampBpm(bpm), beatsPerBar }));
   const stop = () => dispatch(coreHub.stopMetronome());

   return (
      <>
         <Tooltip title={label}>
            <IconButton
               id="metronome-button"
               color={session ? 'primary' : 'inherit'}
               aria-label={label}
               onClick={(e) => setAnchor(e.currentTarget)}
            >
               <AvTimerIcon />
            </IconButton>
         </Tooltip>
         <Popover
            open={Boolean(anchor)}
            anchorEl={anchor}
            onClose={() => setAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
            transformOrigin={{ vertical: 'top', horizontal: 'center' }}
         >
            <Box sx={{ p: 2, width: 300 }} id="metronome-popover">
               <Typography variant="subtitle1" gutterBottom>
                  {t('conference.metronome.title')}
               </Typography>

               {session && (
                  <Box sx={{ display: 'flex', gap: 0.75, mb: 2, alignItems: 'center' }} aria-live="off">
                     {Array.from({ length: session.beatsPerBar }, (_, i) => (
                        <Box
                           key={i}
                           className="metronome-dot"
                           sx={{
                              width: i === 0 ? 18 : 14,
                              height: i === 0 ? 18 : 14,
                              borderRadius: '50%',
                              bgcolor: countingBeat === i + 1 ? 'primary.main' : 'action.disabledBackground',
                              transition: 'background-color 60ms',
                           }}
                        />
                     ))}
                     <Typography variant="body2" color="text.secondary" sx={{ ml: 1 }}>
                        {session.bpm} BPM · {session.beatsPerBar}/4
                     </Typography>
                  </Box>
               )}

               {canControl && (
                  <Box id="metronome-controls" sx={{ mb: 2 }}>
                     <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <IconButton
                           aria-label={t('conference.metronome.slower')}
                           onClick={() => setBpm(clampBpm(bpm - 1))}
                           size="small"
                        >
                           <RemoveIcon />
                        </IconButton>
                        <TextField
                           id="metronome-bpm"
                           type="number"
                           size="small"
                           label="BPM"
                           value={bpm}
                           onChange={(e) => setBpm(Number(e.target.value))}
                           onBlur={() => setBpm(clampBpm(bpm))}
                           slotProps={{ htmlInput: { min: MIN_BPM, max: MAX_BPM } }}
                           sx={{ width: 100 }}
                        />
                        <IconButton
                           aria-label={t('conference.metronome.faster')}
                           onClick={() => setBpm(clampBpm(bpm + 1))}
                           size="small"
                        >
                           <AddIcon />
                        </IconButton>
                        <TextField
                           id="metronome-beats"
                           select
                           size="small"
                           label={t('conference.metronome.beats')}
                           value={beatsPerBar}
                           onChange={(e) => setBeatsPerBar(Number(e.target.value))}
                           sx={{ width: 90 }}
                        >
                           {Array.from({ length: MAX_BEATS_PER_BAR - MIN_BEATS_PER_BAR + 1 }, (_, i) => (
                              <MenuItem key={i} value={i + MIN_BEATS_PER_BAR}>
                                 {i + MIN_BEATS_PER_BAR}
                              </MenuItem>
                           ))}
                        </TextField>
                     </Box>
                     <Slider
                        aria-label="BPM"
                        value={clampBpm(bpm)}
                        min={MIN_BPM}
                        max={MAX_BPM}
                        onChange={(_, value) => setBpm(value as number)}
                        sx={{ mt: 1 }}
                     />
                     <Box sx={{ display: 'flex', gap: 1 }}>
                        <Button id="metronome-start" variant="contained" fullWidth onClick={start}>
                           {session ? t('conference.metronome.change') : t('conference.metronome.start')}
                        </Button>
                        {session && (
                           <Button id="metronome-stop" variant="outlined" color="inherit" onClick={stop}>
                              {t('conference.metronome.stop')}
                           </Button>
                        )}
                     </Box>
                  </Box>
               )}

               <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <IconButton
                     id="metronome-mute"
                     size="small"
                     aria-label={muted ? t('conference.metronome.unmute') : t('conference.metronome.mute')}
                     aria-pressed={muted}
                     onClick={() => dispatch(setMetronomeMuted(!muted))}
                  >
                     {muted ? <VolumeOffIcon /> : <VolumeUpIcon />}
                  </IconButton>
                  <Slider
                     aria-label={t('conference.metronome.volume')}
                     value={volume}
                     min={0}
                     max={1}
                     step={0.05}
                     onChange={(_, value) => dispatch(setMetronomeVolume(value as number))}
                     disabled={muted}
                  />
               </Box>
               <Typography variant="caption" color="text.secondary">
                  {t('conference.metronome.local_hint')}
               </Typography>
            </Box>
         </Popover>
      </>
   );
}
