import {
   Button,
   Dialog,
   DialogActions,
   DialogContent,
   DialogContentText,
   DialogTitle,
   IconButton,
   Tooltip,
} from '@mui/material';
import FiberManualRecordIcon from '@mui/icons-material/FiberManualRecord';
import StopIcon from '@mui/icons-material/Stop';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import usePermission from 'src/hooks/usePermission';
import { RECORDING_CAN_MANAGE } from 'src/permissions';
import { selectActiveRecording, selectIsRecordingAvailable } from '../selectors';

/**
 * Starts and stops the recording. Recording is always a deliberate choice: it asks for confirmation and tells the
 * moderator that everybody will be notified.
 */
export default function RecordButton() {
   const { t } = useTranslation();
   const dispatch = useDispatch();

   const canManage = usePermission(RECORDING_CAN_MANAGE);
   const available = useSelector(selectIsRecordingAvailable);
   const active = useSelector(selectActiveRecording);
   const [confirmOpen, setConfirmOpen] = useState(false);

   if (!canManage || !available) return null;

   const isRecording = Boolean(active);
   const finalizing = active?.status === 'Finalizing';

   const handleClick = () => {
      if (isRecording) dispatch(coreHub.stopRecording());
      else setConfirmOpen(true);
   };

   const handleConfirm = () => {
      setConfirmOpen(false);
      dispatch(coreHub.startRecording());
   };

   const label = isRecording ? t('conference.recording.stop') : t('conference.recording.start');

   return (
      <>
         <Tooltip title={label}>
            <span>
               <IconButton
                  id="recording-toggle"
                  color="inherit"
                  aria-label={label}
                  onClick={handleClick}
                  disabled={finalizing}
                  size="large"
               >
                  {isRecording ? <StopIcon color="error" /> : <FiberManualRecordIcon />}
               </IconButton>
            </span>
         </Tooltip>
         <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)} id="recording-confirm">
            <DialogTitle>{t('conference.recording.confirm_title')}</DialogTitle>
            <DialogContent>
               <DialogContentText>{t('conference.recording.confirm_text')}</DialogContentText>
            </DialogContent>
            <DialogActions>
               <Button onClick={() => setConfirmOpen(false)}>{t('common:cancel')}</Button>
               <Button variant="contained" onClick={handleConfirm} id="recording-confirm-start">
                  {t('conference.recording.confirm_start')}
               </Button>
            </DialogActions>
         </Dialog>
      </>
   );
}
