import { Box, CircularProgress, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import to from 'src/utils/to';

const useStyles = makeStyles()((theme) => ({
   root: {
      height: '100%',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      textAlign: 'center',
      padding: theme.spacing(2),
   },
   spinner: {
      marginBottom: theme.spacing(3),
   },
}));

type Props = {
   denied: boolean;
};

/** Shown to participants that wait in the lobby (or were denied) instead of the conference. */
export default function LobbyWaitingView({ denied }: Props) {
   const { classes } = useStyles();
   const { t } = useTranslation();

   return (
      <div className={classes.root} id={denied ? 'lobby-denied' : 'lobby-waiting'}>
         {!denied && <CircularProgress className={classes.spinner} />}
         <Typography variant="h5" gutterBottom>
            {denied ? t('conference.lobby.denied_title') : t('conference.lobby.waiting_title')}
         </Typography>
         <Typography color="textSecondary">
            {denied ? t('conference.lobby.denied_text') : t('conference.lobby.waiting_text')}
         </Typography>
         {denied && (
            <Box sx={{ mt: 3 }}>
               <Link {...to('/')}>{t('common:back_to_start')}</Link>
            </Box>
         )}
      </div>
   );
}
