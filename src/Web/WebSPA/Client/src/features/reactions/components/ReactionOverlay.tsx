import { Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { AnimatePresence, motion } from 'framer-motion';
import React from 'react';
import { useSelector } from 'react-redux';
import { selectParticipants } from 'src/features/conference/selectors';
import { RootState } from 'src/store';
import { ActiveReaction } from '../reducer';

const useStyles = makeStyles()((theme) => ({
   root: {
      position: 'absolute',
      left: 0,
      right: 0,
      bottom: 0,
      height: '60%',
      pointerEvents: 'none',
      overflow: 'hidden',
   },
   reaction: {
      position: 'absolute',
      bottom: theme.spacing(10),
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
   },
   emoji: {
      fontSize: 40,
      lineHeight: 1,
   },
   name: {
      marginTop: theme.spacing(0.5),
      padding: theme.spacing(0, 1),
      borderRadius: theme.shape.borderRadius,
      backgroundColor: 'rgba(0, 0, 0, 0.55)',
   },
}));

/** a stable horizontal position (in percent) derived from the id, so a reaction does not jump on rerender */
export function getReactionPosition(id: string) {
   let hash = 0;
   for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) | 0;
   return 10 + (Math.abs(hash) % 80);
}

export default function ReactionOverlay() {
   const { classes } = useStyles();
   const reactions = useSelector((state: RootState) => state.reactions.active);
   const participants = useSelector(selectParticipants);

   return (
      <div className={classes.root} id="reactions-overlay" aria-hidden="true">
         <AnimatePresence>
            {reactions.map((reaction: ActiveReaction) => (
               <motion.div
                  key={reaction.id}
                  className={classes.reaction}
                  style={{ left: `${getReactionPosition(reaction.id)}%` }}
                  initial={{ opacity: 0, y: 0, scale: 0.6 }}
                  animate={{ opacity: 1, y: -160, scale: 1 }}
                  exit={{ opacity: 0, y: -220 }}
                  transition={{ duration: 2.5, ease: 'easeOut' }}
               >
                  <span className={classes.emoji}>{reaction.emoji}</span>
                  {participants?.[reaction.participantId]?.displayName && (
                     <Typography variant="caption" className={classes.name}>
                        {participants[reaction.participantId].displayName}
                     </Typography>
                  )}
               </motion.div>
            ))}
         </AnimatePresence>
      </div>
   );
}
