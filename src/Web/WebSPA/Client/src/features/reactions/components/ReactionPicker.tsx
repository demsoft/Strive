import { Fab, IconButton, Popover, Tooltip } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import EmojiEmotionsOutlinedIcon from '@mui/icons-material/EmojiEmotionsOutlined';
import { motion } from 'framer-motion';
import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import * as coreHub from 'src/core-hub';
import { REACTION_EMOJIS } from '../emojis';

const useStyles = makeStyles()((theme) => ({
   picker: {
      display: 'flex',
      flexDirection: 'row',
      padding: theme.spacing(0.5),
   },
   emoji: {
      fontSize: 24,
      lineHeight: 1,
   },
}));

type Props = {
   className?: string;
   variants?: any;
};

export default function ReactionPicker({ className, variants }: Props) {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const anchorRef = useRef<HTMLButtonElement>(null);
   const [open, setOpen] = useState(false);

   const handleSend = (emoji: string) => {
      dispatch(coreHub.sendReaction({ emoji }));
   };

   return (
      <>
         <Tooltip title={t('conference.reactions.send')} arrow>
            <Fab
               id="reactions-picker-toggle"
               ref={anchorRef}
               color="default"
               className={className}
               onClick={() => setOpen((x) => !x)}
               component={motion.button}
               variants={variants}
               aria-label={t('conference.reactions.send')}
               aria-haspopup="true"
               aria-expanded={open}
            >
               <EmojiEmotionsOutlinedIcon />
            </Fab>
         </Tooltip>
         <Popover
            open={open}
            anchorEl={anchorRef.current}
            onClose={() => setOpen(false)}
            anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
            transformOrigin={{ vertical: 'bottom', horizontal: 'center' }}
         >
            <div className={classes.picker} id="reactions-picker">
               {REACTION_EMOJIS.map((emoji) => (
                  <IconButton key={emoji} onClick={() => handleSend(emoji)} aria-label={emoji}>
                     <span className={classes.emoji}>{emoji}</span>
                  </IconButton>
               ))}
            </div>
         </Popover>
      </>
   );
}
