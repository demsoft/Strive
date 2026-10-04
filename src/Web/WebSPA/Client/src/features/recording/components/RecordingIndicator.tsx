import { Chip } from '@mui/material';
import { keyframes } from 'tss-react';
import { makeStyles } from 'tss-react/mui';
import FiberManualRecordIcon from '@mui/icons-material/FiberManualRecord';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import { formatDuration } from '../format';
import { selectActiveRecording } from '../selectors';

const pulse = keyframes`
   0%, 100% { opacity: 1; }
   50% { opacity: 0.35; }
`;

const useStyles = makeStyles()((theme) => ({
   chip: {
      backgroundColor: 'rgba(248, 113, 113, 0.16)',
      border: `1px solid ${theme.palette.error.main}`,
      color: theme.palette.error.light,
      fontWeight: 700,
      marginRight: theme.spacing(1),
   },
   dot: {
      color: `${theme.palette.error.main} !important`,
      animation: `${pulse} 1.4s ease-in-out infinite`,
   },
}));

/** Everybody in the conference sees this while it is recorded. Recording is never secret. */
export default function RecordingIndicator() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const active = useSelector(selectActiveRecording);

   const [now, setNow] = useState(() => Date.now());
   useEffect(() => {
      if (!active) return;
      const timer = setInterval(() => setNow(Date.now()), 1000);
      return () => clearInterval(timer);
   }, [active?.recordingId]);

   if (!active) return null;

   const label =
      active.status === 'finalizing'
         ? t('conference.recording.saving')
         : active.status === 'starting'
           ? t('conference.recording.starting')
           : `${t('conference.recording.rec')} ${formatDuration((now - new Date(active.startedAt).getTime()) / 1000)}`;

   return (
      <Chip
         id="recording-indicator"
         role="status"
         size="small"
         className={classes.chip}
         icon={<FiberManualRecordIcon className={classes.dot} />}
         label={label}
      />
   );
}
