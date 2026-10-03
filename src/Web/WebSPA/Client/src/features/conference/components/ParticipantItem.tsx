import { ButtonBase, alpha, Typography, useTheme } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { Skeleton } from '@mui/material';
import { motion } from 'framer-motion';
import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import AnimatedMicIcon from 'src/assets/animated-icons/AnimatedMicIcon';
import IconHide from 'src/components/IconHide';
import { selectIsHandRaised } from 'src/features/hand-raise/selectors';
import { selectParticipantProducers } from 'src/features/media/selectors';
import useMyParticipantId from 'src/hooks/useMyParticipantId';
import { RootState } from 'src/store';
import { Participant } from '../types';
import ParticipantContextMenuPopper from './ParticipantContextMenuPopper';

const useStyles = makeStyles()((theme) => ({
   root: {
      marginLeft: theme.spacing(1),
   },
   button: {
      padding: theme.spacing(0, 1),
      display: 'flex',
      flexDirection: 'row',
      justifyContent: 'space-between',
      alignItems: 'center',
      borderRadius: theme.shape.borderRadius,
      width: '100%',
      '&:hover': {
         textDecoration: 'none',
         backgroundColor: alpha(theme.palette.text.primary, 0.05),
      },
   },
}));

type Props = {
   participant?: Participant;
};

export default function ParticipantItem({ participant }: Props) {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const producers = useSelector((state: RootState) => selectParticipantProducers(state, participant?.id));
   const handRaised = useSelector((state: RootState) => selectIsHandRaised(state, participant?.id));
   const myParticipantId = useMyParticipantId();

   const theme = useTheme();

   const [popperOpen, setPopperOpen] = useState(false);
   const buttonRef = useRef<HTMLButtonElement>(null);

   const handleClose = () => {
      setPopperOpen(false);
   };

   const handleToggle = () => {
      setPopperOpen((prevOpen) => !prevOpen);
   };

   return (
      <div className={classes.root}>
         <ButtonBase onClick={handleToggle} ref={buttonRef} component={motion.button} className={classes.button}>
            <Typography variant="subtitle1">
               {handRaised && <span aria-label={t('conference.hand_raise.raised')}>✋ </span>}
               {participant ? participant?.displayName : <Skeleton />}
            </Typography>
            <IconHide hidden={!producers?.mic}>
               <AnimatedMicIcon activated={!producers?.mic?.paused} disabledColor={theme.palette.error.main} />
            </IconHide>
         </ButtonBase>
         {participant && participant.id !== myParticipantId && (
            <ParticipantContextMenuPopper
               open={popperOpen}
               onClose={handleClose}
               participant={participant}
               anchorEl={buttonRef.current}
            />
         )}
      </div>
   );
}
