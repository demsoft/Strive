import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   Container,
   Dialog,
   DialogContent,
   DialogTitle,
   Paper,
   TextField,
   Toolbar,
   Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { brand } from 'src/theme';
import BrandLogo from 'src/components/BrandLogo';
import { formatMoney } from 'src/features/sessions/money';
import { fetchVideoReports, keepVideo, removeVideo, RemovedVideo, VideoReportItem } from '../api';

/** The videos that people reported: watch them, then keep them or remove them (with a word for the creator). */
export default function AdminVideosPage() {
   const { t } = useTranslation();
   const [items, setItems] = useState<VideoReportItem[] | null>(null);
   const [forbidden, setForbidden] = useState(false);
   const [failed, setFailed] = useState(false);
   const [watching, setWatching] = useState<VideoReportItem | null>(null);
   const [refunds, setRefunds] = useState<RemovedVideo | null>(null);
   const [removing, setRemoving] = useState<VideoReportItem | null>(null);
   const [note, setNote] = useState('');
   const [actionError, setActionError] = useState<string | null>(null);

   const load = useCallback(() => {
      fetchVideoReports().then(
         (list) => {
            setItems(list);
            setFailed(false);
         },
         (e) => {
            const status = (e as { response?: { status?: number } }).response?.status;
            if (status === 403 || status === 401) setForbidden(true);
            else setFailed(true);
         },
      );
   }, []);

   useEffect(() => {
      load();
   }, [load]);

   const decide = async (action: () => Promise<void | RemovedVideo>) => {
      setActionError(null);
      try {
         const done = await action();
         // removing says how the refunds went
         setRefunds(done ?? null);
         setRemoving(null);
         setNote('');
         load();
      } catch {
         setActionError(t('admin.videos_action_failed'));
      }
   };

   const header = (
      <AppBar position="static" color="transparent" elevation={0}>
         <Toolbar sx={{ gap: 2 }}>
            <BrandLogo size={28} />
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
               {t('admin.videos_title')}
            </Typography>
            <Box sx={{ flex: 1 }} />
            <Button size="small" startIcon={<RefreshIcon />} onClick={load}>
               {t('admin.refresh')}
            </Button>
            <Button size="small" component={RouterLink} to="/admin" startIcon={<ArrowBackIcon />}>
               {t('admin.title')}
            </Button>
         </Toolbar>
      </AppBar>
   );

   if (forbidden)
      return (
         <>
            {header}
            <Container maxWidth="sm" sx={{ mt: 8 }}>
               <Alert severity="info" id="admin-videos-forbidden">
                  {t('admin.forbidden')}
               </Alert>
            </Container>
         </>
      );

   return (
      <>
         {header}
         <Container maxWidth="md" sx={{ pt: 3, pb: 6 }} id="admin-videos-page">
            {failed && (
               <Alert severity="error" sx={{ mb: 2 }}>
                  {t('admin.videos_failed')}
               </Alert>
            )}
            {items === null && !failed && <Typography color="text.secondary">{t('admin.loading')}</Typography>}
            {items?.length === 0 && (
               <Alert severity="success" id="admin-videos-none">
                  {t('admin.videos_none')}
               </Alert>
            )}

            {refunds && (refunds.refunded > 0 || refunds.refundsFailed > 0) && (
               <Alert
                  id="admin-videos-refunds"
                  severity={refunds.refundsFailed > 0 ? 'warning' : 'success'}
                  sx={{ mb: 2 }}
                  onClose={() => setRefunds(null)}
               >
                  {t('admin.videos_refunded', { count: refunds.refunded })}
                  {refunds.refundsFailed > 0 &&
                     ` ${t('admin.videos_refunds_failed', { count: refunds.refundsFailed })}`}
               </Alert>
            )}

            {items?.map((item) => (
               <Paper
                  key={item.videoId}
                  className="reported-video"
                  sx={{
                     p: 2,
                     mb: 2,
                     display: 'flex',
                     gap: 2,
                     flexWrap: 'wrap',
                     borderRadius: '20px',
                     border: `1px solid ${brand.border}`,
                  }}
               >
                  <Box
                     sx={{
                        width: 120,
                        height: 160,
                        flexShrink: 0,
                        borderRadius: '12px',
                        backgroundColor: 'rgba(255,255,255,0.06)',
                        backgroundImage: item.posterUrl ? `url(${item.posterUrl})` : undefined,
                        backgroundSize: 'cover',
                        backgroundPosition: 'center',
                     }}
                  />
                  <Box sx={{ flex: 1, minWidth: 220 }}>
                     <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                        <b>{item.title}</b>
                        {item.moderation === 'hidden' && (
                           <Chip
                              size="small"
                              color="warning"
                              label={t('admin.videos_hidden')}
                              className="video-hidden"
                           />
                        )}
                     </Box>
                     <Typography variant="body2" color="text.secondary">
                        {item.creatorName}
                        {item.priceMinor > 0 && ` · ${formatMoney(item.priceMinor, 'NGN')}`}
                        {item.latestReportAt &&
                           ` · ${DateTime.fromISO(item.latestReportAt).toLocaleString(DateTime.DATETIME_SHORT)}`}
                     </Typography>
                     <Typography sx={{ mt: 1 }} className="report-count">
                        {t('admin.videos_reports', { count: item.openReports })}
                     </Typography>
                     <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap', mt: 0.5 }}>
                        {Object.entries(item.reasons).map(([reason, count]) => (
                           <Chip
                              key={reason}
                              size="small"
                              className="report-reason"
                              label={`${t(`videos.reason_${reason}`)} · ${count}`}
                           />
                        ))}
                     </Box>
                     {item.details.map((detail, i) => (
                        <Typography
                           key={i}
                           variant="body2"
                           color="text.secondary"
                           className="report-detail"
                           sx={{ mt: 0.5 }}
                        >
                           “{detail.text}”
                        </Typography>
                     ))}
                     {item.moderation === 'removed' ? (
                        <Alert severity="warning" icon={false} sx={{ mt: 1 }} className="report-refunds-open">
                           {t('admin.videos_refunds_open', { count: item.refundsPending ?? 0 })}
                        </Alert>
                     ) : (
                        item.buyers > 0 && (
                           <Alert severity="warning" icon={false} sx={{ mt: 1 }} className="report-buyers">
                              {t('admin.videos_buyers', { count: item.buyers })}
                           </Alert>
                        )
                     )}
                     <Box sx={{ display: 'flex', gap: 1, mt: 1.5, flexWrap: 'wrap' }}>
                        {item.playbackUrl && (
                           <Button
                              size="small"
                              variant="outlined"
                              className="report-watch"
                              onClick={() => setWatching(item)}
                           >
                              {t('admin.videos_watch')}
                           </Button>
                        )}
                        {item.moderation === 'removed' ? (
                           <Button
                              size="small"
                              variant="contained"
                              className="report-retry"
                              onClick={() => decide(() => removeVideo(item.videoId, ''))}
                           >
                              {t('admin.videos_retry_refunds')}
                           </Button>
                        ) : (
                           <>
                              <Button
                                 size="small"
                                 variant="contained"
                                 color="success"
                                 className="report-keep"
                                 onClick={() => decide(() => keepVideo(item.videoId))}
                              >
                                 {t('admin.videos_keep')}
                              </Button>
                              <Button
                                 size="small"
                                 variant="contained"
                                 color="error"
                                 className="report-remove"
                                 onClick={() => setRemoving(item)}
                              >
                                 {t('admin.videos_remove')}
                              </Button>
                           </>
                        )}
                     </Box>
                  </Box>
               </Paper>
            ))}
         </Container>

         <Dialog open={watching !== null} onClose={() => setWatching(null)} maxWidth="sm" fullWidth>
            <DialogTitle>{watching?.title}</DialogTitle>
            <DialogContent>
               {watching?.playbackUrl && (
                  <video
                     id="admin-video-player"
                     src={watching.playbackUrl}
                     controls
                     playsInline
                     style={{ width: '100%', maxHeight: '70vh', backgroundColor: '#000', borderRadius: 12 }}
                  />
               )}
            </DialogContent>
         </Dialog>

         <Dialog open={removing !== null} onClose={() => setRemoving(null)} maxWidth="xs" fullWidth>
            <DialogTitle>{t('admin.videos_remove_title', { title: removing?.title ?? '' })}</DialogTitle>
            <DialogContent>
               <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {t('admin.videos_remove_text')}
               </Typography>
               <TextField
                  id="remove-note"
                  fullWidth
                  multiline
                  minRows={2}
                  label={t('admin.videos_note')}
                  value={note}
                  onChange={(e) => setNote(e.target.value.slice(0, 500))}
               />
               {actionError && (
                  <Alert severity="error" sx={{ mt: 2 }} id="admin-videos-error">
                     {actionError}
                  </Alert>
               )}
               <Button
                  id="remove-confirm"
                  variant="contained"
                  color="error"
                  fullWidth
                  sx={{ mt: 2 }}
                  onClick={() => removing && decide(() => removeVideo(removing.videoId, note.trim()))}
               >
                  {t('admin.videos_remove')}
               </Button>
            </DialogContent>
         </Dialog>
      </>
   );
}
