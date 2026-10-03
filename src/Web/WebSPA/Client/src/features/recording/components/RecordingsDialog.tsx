import {
   Alert,
   Box,
   Button,
   Chip,
   CircularProgress,
   Dialog,
   DialogActions,
   DialogContent,
   DialogTitle,
   IconButton,
   List,
   ListItem,
   ListItemText,
   Menu,
   MenuItem,
   Tooltip,
   Typography,
} from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';
import { ConferenceRouteParams } from 'src/routes/types';
import * as api from 'src/services/api/recording';
import { RecordingDto, RecordingVisibility } from 'src/services/api/recording';
import { showMessage } from 'src/store/notifier/actions';
import { formatDuration, formatSize } from '../format';
import { selectActiveRecording } from '../selectors';

type Props = {
   open: boolean;
   onClose: () => void;
};

/** The recordings of this conference, for moderators: share, change who can watch, delete. */
export default function RecordingsDialog({ open, onClose }: Props) {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const { id: conferenceId } = useParams<ConferenceRouteParams>();
   const active = useSelector(selectActiveRecording);

   const [recordings, setRecordings] = useState<RecordingDto[] | null>(null);
   const [error, setError] = useState(false);
   const [menu, setMenu] = useState<{ anchor: HTMLElement; recording: RecordingDto } | null>(null);

   const load = useCallback(async () => {
      if (!conferenceId) return;
      try {
         setRecordings(await api.fetchRecordings(conferenceId));
         setError(false);
      } catch {
         setError(true);
      }
   }, [conferenceId]);

   // reload when the dialog opens and when a recording starts or ends
   useEffect(() => {
      if (open) load();
   }, [open, active?.recordingId, active?.status, load]);

   const copyLink = async (recording: RecordingDto) => {
      const link = api.shareLink(recording.shareToken);
      try {
         await navigator.clipboard.writeText(link);
         dispatch(showMessage({ type: 'success', message: t('conference.recording.link_copied') }));
      } catch {
         dispatch(showMessage({ type: 'info', icon: '🔗', message: link }));
      }
   };

   const changeVisibility = async (recording: RecordingDto, visibility: RecordingVisibility) => {
      setMenu(null);
      await api.setVisibility(recording.recordingId, visibility);
      await load();
   };

   const remove = async (recording: RecordingDto) => {
      setMenu(null);
      if (!window.confirm(t('conference.recording.delete_confirm'))) return;
      try {
         await api.deleteRecording(recording.recordingId);
      } finally {
         await load();
      }
   };

   const statusChip = (recording: RecordingDto) => {
      switch (recording.status) {
         case 'Ready':
            return null;
         case 'Failed':
            return <Chip size="small" color="error" label={t('conference.recording.status_failed')} />;
         default:
            return <Chip size="small" color="primary" label={t('conference.recording.status_processing')} />;
      }
   };

   return (
      <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm" id="recordings-dialog">
         <DialogTitle>{t('conference.recording.list_title')}</DialogTitle>
         <DialogContent>
            {error && <Alert severity="error">{t('conference.recording.list_error')}</Alert>}
            {!error && recordings === null && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
                  <CircularProgress />
               </Box>
            )}
            {recordings?.length === 0 && (
               <Typography color="textSecondary" sx={{ py: 2 }}>
                  {t('conference.recording.list_empty')}
               </Typography>
            )}
            <List disablePadding>
               {recordings?.map((recording) => (
                  <ListItem
                     key={recording.recordingId}
                     divider
                     className="recording-list-item"
                     secondaryAction={
                        recording.status === 'Ready' ? (
                           <>
                              <Tooltip title={t('conference.recording.copy_link')}>
                                 <IconButton aria-label={t('conference.recording.copy_link')} onClick={() => copyLink(recording)}>
                                    <ContentCopyIcon fontSize="small" />
                                 </IconButton>
                              </Tooltip>
                              <Tooltip title={t('conference.recording.watch')}>
                                 <IconButton
                                    aria-label={t('conference.recording.watch')}
                                    href={api.shareLink(recording.shareToken)}
                                    target="_blank"
                                 >
                                    <PlayArrowIcon fontSize="small" />
                                 </IconButton>
                              </Tooltip>
                              <IconButton
                                 aria-label={t('conference.recording.more')}
                                 onClick={(event) => setMenu({ anchor: event.currentTarget, recording })}
                              >
                                 <MoreVertIcon fontSize="small" />
                              </IconButton>
                           </>
                        ) : recording.status === 'Failed' ? (
                           <IconButton aria-label={t('common:delete')} onClick={() => remove(recording)}>
                              <DeleteOutlinedIcon fontSize="small" />
                           </IconButton>
                        ) : undefined
                     }
                  >
                     <ListItemText
                        primary={
                           <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                              {DateTime.fromISO(recording.startedAt).toLocaleString(DateTime.DATETIME_MED)}
                              {statusChip(recording)}
                           </Box>
                        }
                        secondary={[
                           recording.durationSeconds != null ? formatDuration(recording.durationSeconds) : null,
                           recording.sizeBytes != null ? formatSize(recording.sizeBytes) : null,
                           recording.status === 'Ready'
                              ? recording.visibility === 'AnyoneWithLink'
                                 ? t('conference.recording.visibility_anyone')
                                 : t('conference.recording.visibility_signed_in')
                              : null,
                           recording.status === 'Ready'
                              ? t('conference.recording.deleted_on', {
                                   date: DateTime.fromISO(recording.expiresAt).toLocaleString(DateTime.DATE_MED),
                                })
                              : null,
                        ]
                           .filter(Boolean)
                           .join(' · ')}
                     />
                  </ListItem>
               ))}
            </List>
            <Menu open={Boolean(menu)} anchorEl={menu?.anchor} onClose={() => setMenu(null)}>
               {menu?.recording.visibility === 'SignedIn' ? (
                  <MenuItem onClick={() => changeVisibility(menu.recording, 'AnyoneWithLink')}>
                     {t('conference.recording.make_public')}
                  </MenuItem>
               ) : (
                  <MenuItem onClick={() => changeVisibility(menu!.recording, 'SignedIn')}>
                     {t('conference.recording.make_private')}
                  </MenuItem>
               )}
               <MenuItem onClick={() => remove(menu!.recording)}>{t('common:delete')}</MenuItem>
            </Menu>
         </DialogContent>
         <DialogActions>
            <Button onClick={onClose}>{t('common:close')}</Button>
         </DialogActions>
      </Dialog>
   );
}
