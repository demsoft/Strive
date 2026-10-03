import { Button } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import AddIcon from '@mui/icons-material/Add';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import { openDialogToCreateAsync } from '../reducer';
import CreateConferenceDialog from './CreateConferenceDialog';

const useStyles = makeStyles()((theme) => ({
   extendedIcon: {
      marginRight: theme.spacing(1),
   },
   fabMargin: {
      marginTop: theme.spacing(2),
   },
}));

function ConferenceControls() {
   const { classes } = useStyles();
   const dispatch = useDispatch();
   const { t } = useTranslation();

   const handleCreateConference = () => dispatch(openDialogToCreateAsync());

   return (
      <>
         <Button
            color="primary"
            variant="contained"
            size="large"
            onClick={handleCreateConference}
            id="create-conference-button"
            startIcon={<AddIcon />}
            sx={{ boxShadow: '0 12px 32px rgba(124, 92, 255, 0.45)', borderRadius: 4 }}
         >
            {t('view_main.start_new_conference')}
         </Button>

         <CreateConferenceDialog />
      </>
   );
}

export default ConferenceControls;
