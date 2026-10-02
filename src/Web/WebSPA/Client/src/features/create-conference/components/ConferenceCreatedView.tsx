import { Box, Button, DialogActions, DialogContent, TextField } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React from 'react';
import { useDispatch } from 'react-redux';
import { closeDialog } from '../reducer';
import to from 'src/utils/to';
import { useTranslation } from 'react-i18next';

const useStyles = makeStyles()((theme) => ({
   conferenceUrlField: {
      flex: 1,
      marginRight: theme.spacing(2),
   },
}));

type Props = {
   conferenceId: string;
};

export default function ConferenceCreatedView({ conferenceId }: Props) {
   const { classes } = useStyles();
   const dispatch = useDispatch();
   const { t } = useTranslation();

   const handleClose = () => dispatch(closeDialog());

   return (
      <DialogContent>
         <Box
            sx={{
               display: 'flex',
               flexDirection: 'row',
               alignItems: 'center',
            }}
         >
            <TextField
               id="created-conference-url"
               variant="outlined"
               label={t('dialog_create_conference.created.conference_url')}
               value={new URL('/c/' + conferenceId, document.baseURI).href}
               className={classes.conferenceUrlField}
               slotProps={{
                  input: { readOnly: true },
               }}
            />
            <Button
               id="join-conference-button"
               variant="contained"
               {...to('/c/' + conferenceId)}
               onClick={handleClose}
               color="primary"
            >
               {t('dialog_create_conference.created.join')}
            </Button>
         </Box>
         <DialogActions>
            <Button onClick={handleClose} color="primary">
               {t('common:close')}
            </Button>
         </DialogActions>
      </DialogContent>
   );
}
