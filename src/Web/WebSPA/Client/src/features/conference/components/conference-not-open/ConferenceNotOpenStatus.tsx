import { Typography } from '@mui/material';
import { keyframes } from 'tss-react';
import { makeStyles } from 'tss-react/mui';
import { DateTime } from 'luxon';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { brand } from 'src/theme';

const pulse = keyframes`
   0% { box-shadow: 0 0 0 0 rgba(34, 211, 238, 0.6); }
   70% { box-shadow: 0 0 0 12px rgba(34, 211, 238, 0); }
   100% { box-shadow: 0 0 0 0 rgba(34, 211, 238, 0); }
`;

const useStyles = makeStyles()((theme) => ({
   dot: {
      width: 12,
      height: 12,
      borderRadius: '50%',
      backgroundColor: brand.cyan,
      animation: `${pulse} 2s infinite`,
      margin: '0 auto',
      marginBottom: theme.spacing(3),
   },
}));

type Props = {
   name?: string | null;
   title: string;
   description?: string;
   scheduledDate?: string | null;
};

/** The headline of the not-open screens: the conference name, what is going on and what happens next. */
export default function ConferenceNotOpenStatus({ name, title, description, scheduledDate }: Props) {
   const { classes } = useStyles();
   const { t } = useTranslation();

   return (
      <>
         <div className={classes.dot} />
         {name && (
            <Typography variant="overline" color="textSecondary" id="not-open-conference-name">
               {name}
            </Typography>
         )}
         <Typography variant="h4" gutterBottom>
            {title}
         </Typography>
         {scheduledDate && (
            <Typography color="textSecondary" gutterBottom>
               {t('conference_not_open.conference_scheduled_for')}{' '}
               <b>{DateTime.fromISO(scheduledDate).toLocaleString(DateTime.DATETIME_FULL)}</b>
            </Typography>
         )}
         {description && <Typography color="textSecondary">{description}</Typography>}
      </>
   );
}
