import {
   Button,
   Dialog,
   DialogActions,
   DialogContent,
   DialogContentText,
   DialogTitle,
   IconButton,
   TextField,
   Tooltip,
} from '@mui/material';
import SmartDisplayOutlinedIcon from '@mui/icons-material/SmartDisplayOutlined';
import StopCircleOutlinedIcon from '@mui/icons-material/StopCircleOutlined';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import usePermission from 'src/hooks/usePermission';
import { WATCH_TOGETHER_CAN_CONTROL } from 'src/permissions';
import { selectWatchTogetherSession } from '../selectors';

/**
 * App bar button for the people that may control it: opens a window to paste the link of a YouTube video that everybody
 * then watches together, and stops it again.
 */
export default function WatchTogetherButton() {
   const { t } = useTranslation();
   const dispatch = useDispatch();

   const canControl = usePermission(WATCH_TOGETHER_CAN_CONTROL);
   const session = useSelector(selectWatchTogetherSession);

   const [open, setOpen] = useState(false);
   const [url, setUrl] = useState('');

   if (!canControl) return null;

   const handleStart = () => {
      const trimmed = url.trim();
      if (!trimmed) return;

      dispatch(coreHub.startWatchTogether({ url: trimmed }));
      setOpen(false);
      setUrl('');
   };

   const label = session ? t('conference.watch_together.stop') : t('conference.watch_together.title');

   return (
      <>
         <Tooltip title={label}>
            <IconButton
               id="watch-together-button"
               color={session ? 'primary' : 'inherit'}
               aria-label={label}
               onClick={() => (session ? dispatch(coreHub.stopWatchTogether()) : setOpen(true))}
            >
               {session ? <StopCircleOutlinedIcon /> : <SmartDisplayOutlinedIcon />}
            </IconButton>
         </Tooltip>
         <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm" id="watch-together-dialog">
            <DialogTitle>{t('conference.watch_together.title')}</DialogTitle>
            <DialogContent>
               <DialogContentText sx={{ mb: 2 }}>{t('conference.watch_together.description')}</DialogContentText>
               <TextField
                  autoFocus
                  fullWidth
                  id="watch-together-url"
                  label={t('conference.watch_together.link')}
                  placeholder="https://www.youtube.com/watch?v=…"
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                  onKeyDown={(e) => {
                     if (e.key === 'Enter') handleStart();
                  }}
               />
            </DialogContent>
            <DialogActions>
               <Button onClick={() => setOpen(false)}>{t('common:cancel')}</Button>
               <Button variant="contained" id="watch-together-start-button" disabled={!url.trim()} onClick={handleStart}>
                  {t('conference.watch_together.start')}
               </Button>
            </DialogActions>
         </Dialog>
      </>
   );
}
