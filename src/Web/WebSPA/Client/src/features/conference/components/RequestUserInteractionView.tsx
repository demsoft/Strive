import { Button, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import VideocamIcon from '@mui/icons-material/Videocam';
import React from 'react';
import { useTranslation } from 'react-i18next';
import BrandLogo from 'src/components/BrandLogo';

const useStyles = makeStyles()((theme) => ({
   root: {
      height: '100%',
      width: '100%',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      flexDirection: 'column',
      cursor: 'pointer',
      textAlign: 'center',
      padding: theme.spacing(2),
      boxSizing: 'border-box',
      gap: theme.spacing(1.5),
   },
}));

/**
 * Browsers only allow audio after the user interacted with the page. Any click or key press continues (the
 * UserInteractionListener listens globally), the button makes that obvious.
 */
export default function RequestUserInteractionView() {
   const { classes } = useStyles();
   const { t } = useTranslation();

   return (
      <div className={classes.root} id="request-user-interaction">
         <BrandLogo size={48} showName={false} />
         <Typography variant="h3">{t('request_user_interaction.title')}</Typography>
         <Typography color="textSecondary">{t('request_user_interaction.request')}</Typography>
         <Button variant="contained" size="large" startIcon={<VideocamIcon />} sx={{ mt: 1 }}>
            {t('request_user_interaction.button')}
         </Button>
         <Typography variant="caption" color="textSecondary" sx={{ maxWidth: 420, mt: 2 }}>
            {t('request_user_interaction.description')}
         </Typography>
      </div>
   );
}
