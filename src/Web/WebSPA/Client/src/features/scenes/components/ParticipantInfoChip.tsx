import { useTheme } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import Chip, { ChipProps } from '@mui/material/Chip';
import React from 'react';
import { useSelector } from 'react-redux';
import AnimatedMicIcon from 'src/assets/animated-icons/AnimatedMicIcon';
import { Participant } from 'src/features/conference/types';
import { selectIsHandRaised } from 'src/features/hand-raise/selectors';
import { selectParticipantMicActivated } from 'src/features/media/selectors';
import { ParticipantAudioInfo } from 'src/features/media/types';
import { RootState } from 'src/store';

const useStyles = makeStyles()((theme) => ({
   chip: {
      padding: theme.spacing(0, 1),
      minWidth: 96,
      transition: 'border 200ms ease-out',
      alignItems: 'left',
   },
   chipSpeaking: {
      borderColor: theme.palette.primary.main,
   },
   chipMicDeactivated: {
      borderColor: theme.palette.error.main,
   },
   chipLabel: {
      flex: 1,
      textAlign: 'center',
   },
}));

type Props = ChipProps<any, { component: any }> & {
   participantId: string;
   participant?: Participant;
   audioInfo?: ParticipantAudioInfo;
   className?: string;
};

export default function ParticipantInfoChip({ className, participantId, participant, audioInfo, ...props }: Props) {
   const { classes, cx } = useStyles();
   const theme = useTheme();
   const handRaised = useSelector((state: RootState) => selectIsHandRaised(state, participantId));
   const micActivated = useSelector((state: RootState) => selectParticipantMicActivated(state, participantId));

   return (
      <Chip
         size="small"
         className={cx(classes.chip, className, {
            [classes.chipSpeaking]: audioInfo?.speaking,
            [classes.chipMicDeactivated]: !micActivated,
         })}
         classes={{ label: classes.chipLabel }}
         label={`${handRaised ? '✋ ' : ''}${participant?.displayName ?? participantId}`}
         variant="outlined"
         icon={<AnimatedMicIcon activated={micActivated} disabledColor={theme.palette.error.main} />}
         {...props}
      />
   );
}
