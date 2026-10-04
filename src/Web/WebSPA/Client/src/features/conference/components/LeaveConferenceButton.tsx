import {
   Button,
   Dialog,
   DialogActions,
   DialogContent,
   DialogContentText,
   DialogTitle,
   Fab,
   Tooltip,
} from '@mui/material';
import CallEndIcon from '@mui/icons-material/CallEnd';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import { useHistory } from 'react-router-dom';
import * as coreHub from 'src/core-hub';
import usePermission from 'src/hooks/usePermission';
import { CONFERENCE_CAN_OPEN_AND_CLOSE } from 'src/permissions';

type Props = {
   className?: string;
   size?: 'small' | 'medium' | 'large';
   [x: string]: unknown;
};

/**
 * The red button to leave the meeting without closing the browser. Moderators can also end the meeting for everybody.
 * Leaving closes the connection of this person (the camera and the microphone are released with it) and goes back to the
 * start page.
 */
export default function LeaveConferenceButton({ className, size, ...fabProps }: Props) {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const history = useHistory();
   const canEnd = usePermission(CONFERENCE_CAN_OPEN_AND_CLOSE);
   const [open, setOpen] = useState(false);

   const leave = () => {
      setOpen(false);
      history.push('/');
   };

   const endForEveryone = () => {
      dispatch(coreHub.closeConference());
      leave();
   };

   return (
      <>
         <Tooltip title={t('conference.leave.button')} arrow>
            <Fab
               id="leave-conference-button"
               color="error"
               className={className}
               size={size}
               aria-label={t('conference.leave.button')}
               onClick={() => setOpen(true)}
               {...fabProps}
            >
               <CallEndIcon />
            </Fab>
         </Tooltip>
         <Dialog open={open} onClose={() => setOpen(false)} id="leave-conference-dialog">
            <DialogTitle>{t('conference.leave.title')}</DialogTitle>
            <DialogContent>
               <DialogContentText>
                  {canEnd ? t('conference.leave.text_moderator') : t('conference.leave.text')}
               </DialogContentText>
            </DialogContent>
            <DialogActions sx={{ flexWrap: 'wrap', gap: 1, px: 3, pb: 2 }}>
               <Button onClick={() => setOpen(false)}>{t('common:cancel')}</Button>
               {canEnd && (
                  <Button color="error" id="end-conference-button" onClick={endForEveryone}>
                     {t('conference.leave.end_for_everyone')}
                  </Button>
               )}
               <Button variant="contained" id="leave-conference-confirm" onClick={leave}>
                  {t('conference.leave.confirm')}
               </Button>
            </DialogActions>
         </Dialog>
      </>
   );
}
