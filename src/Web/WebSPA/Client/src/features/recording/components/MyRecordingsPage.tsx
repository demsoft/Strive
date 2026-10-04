import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   CircularProgress,
   Container,
   IconButton,
   List,
   ListItem,
   ListItemText,
   Toolbar,
   Tooltip,
   Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import { Link as RouterLink } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import * as api from 'src/services/api/recording';
import { MyRecordingDto } from 'src/services/api/recording';
import { showMessage } from 'src/store/notifier/actions';
import { formatDuration, formatSize } from '../format';

/** The recordings that I started, also of meetings that are closed: the link of a recording is never lost with the meeting. */
export default function MyRecordingsPage() {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const [recordings, setRecordings] = useState<MyRecordingDto[] | null>(null);
   const [error, setError] = useState(false);

   const load = useCallback(async () => {
      try {
         setRecordings(await api.fetchMyRecordings());
         setError(false);
      } catch {
         setError(true);
      }
   }, []);

   // recordings that are still being saved turn into links on their own
   useEffect(() => {
      load();
      const timer = setInterval(load, 10000);
      return () => clearInterval(timer);
   }, [load]);

   const copyLink = async (recording: MyRecordingDto) => {
      const link = api.shareLink(recording.shareToken);
      try {
         await navigator.clipboard.writeText(link);
         dispatch(showMessage({ type: 'success', message: t('conference.recording.link_copied') }));
      } catch {
         dispatch(showMessage({ type: 'info', icon: '🔗', message: link }));
      }
   };

   const remove = async (recording: MyRecordingDto) => {
      if (!window.confirm(t('conference.recording.delete_confirm'))) return;
      try {
         await api.deleteRecording(recording.recordingId);
      } finally {
         await load();
      }
   };

   return (
      <>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar sx={{ gap: 2 }}>
               <BrandLogo size={28} />
               <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  {t('conference.recording.my_title')}
               </Typography>
               <Box sx={{ flex: 1 }} />
               <Button size="small" component={RouterLink} to="/" startIcon={<ArrowBackIcon />}>
                  {t('admin.back')}
               </Button>
            </Toolbar>
         </AppBar>
         <Container maxWidth="md" sx={{ pt: 3, pb: 6 }} id="my-recordings">
            {error && <Alert severity="error">{t('conference.recording.list_error')}</Alert>}
            {!error && recordings === null && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
                  <CircularProgress />
               </Box>
            )}
            {recordings?.length === 0 && (
               <Typography color="text.secondary">{t('conference.recording.my_empty')}</Typography>
            )}
            <List disablePadding>
               {recordings?.map((recording) => (
                  <ListItem
                     key={recording.recordingId}
                     divider
                     className="recording-list-item"
                     secondaryAction={
                        recording.status === 'ready' ? (
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
                              <IconButton aria-label={t('common:delete')} onClick={() => remove(recording)}>
                                 <DeleteOutlinedIcon fontSize="small" />
                              </IconButton>
                           </>
                        ) : recording.status === 'failed' ? (
                           <IconButton aria-label={t('common:delete')} onClick={() => remove(recording)}>
                              <DeleteOutlinedIcon fontSize="small" />
                           </IconButton>
                        ) : undefined
                     }
                  >
                     <ListItemText
                        primary={
                           <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                              <b>{recording.conferenceName || recording.conferenceId}</b>
                              {recording.status === 'failed' && (
                                 <Chip size="small" color="error" label={t('conference.recording.status_failed')} />
                              )}
                              {recording.status !== 'ready' && recording.status !== 'failed' && (
                                 <Chip size="small" color="primary" label={t('conference.recording.status_processing')} />
                              )}
                           </Box>
                        }
                        secondary={[
                           DateTime.fromISO(recording.startedAt).toLocaleString(DateTime.DATETIME_MED),
                           recording.durationSeconds != null ? formatDuration(recording.durationSeconds) : null,
                           recording.sizeBytes != null ? formatSize(recording.sizeBytes) : null,
                           recording.status === 'ready'
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
         </Container>
      </>
   );
}
