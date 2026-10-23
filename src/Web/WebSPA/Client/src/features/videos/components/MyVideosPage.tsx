import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   CircularProgress,
   Container,
   Dialog,
   DialogContent,
   DialogTitle,
   LinearProgress,
   TextField,
   Toolbar,
   Typography,
} from '@mui/material';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/videos';
import { UploadError, Video } from 'src/services/api/videos';
import { brand } from 'src/theme';
import { fileProblem, formatDuration, formatMegabytes, readDuration } from '../videoFile';

/** How often the list is asked again while a video is being sent or converted. */
const REFRESH_MS = 8000;

type Step = { state: 'idle' } | { state: 'sending'; fraction: number } | { state: 'checking' };

/**
 * Where a creator adds videos: choose a file, it goes straight to the converter (with a progress bar), the server
 * checks it and conversion starts. The list shows what each video is doing, and plays the ones that are ready.
 */
export default function MyVideosPage() {
   const { t } = useTranslation();
   const [videos, setVideos] = useState<Video[] | null>(null);
   const [loadError, setLoadError] = useState<string | null>(null);

   const [title, setTitle] = useState('');
   const [description, setDescription] = useState('');
   const [file, setFile] = useState<File | null>(null);
   const [duration, setDuration] = useState<number | null>(null);
   const [step, setStep] = useState<Step>({ state: 'idle' });
   const [formError, setFormError] = useState<string | null>(null);
   const abort = useRef<(() => void) | null>(null);
   const [watching, setWatching] = useState<Video | null>(null);

   const load = useCallback(async () => {
      try {
         setVideos(await api.fetchMyVideos());
         setLoadError(null);
      } catch (e) {
         setLoadError(requestErrorMessage(e));
      }
   }, []);

   useEffect(() => {
      load();
   }, [load]);

   // something is on its way: look again until it is done
   const busy = videos?.some((v) => v.status === 'uploading' || v.status === 'processing') ?? false;
   useEffect(() => {
      if (!busy) return;
      const timer = setInterval(load, REFRESH_MS);
      return () => clearInterval(timer);
   }, [busy, load]);

   useEffect(() => () => abort.current?.(), []);

   const choose = async (event: React.ChangeEvent<HTMLInputElement>) => {
      const chosen = event.target.files?.[0] ?? null;
      event.target.value = '';
      setFormError(null);
      setFile(chosen);
      setDuration(null);
      if (!chosen) return;

      const seconds = await readDuration(chosen);
      setDuration(seconds);
      const problem = fileProblem(chosen, seconds);
      if (problem) {
         setFile(null);
         setFormError(t(`videos.problem_${problem}`));
      } else if (!title) {
         // the name of the file is a start for the title: "my-first-reel.mp4"
         setTitle(
            chosen.name
               .replace(/\.[^.]+$/, '')
               .replace(/[-_]+/g, ' ')
               .slice(0, 100),
         );
      }
   };

   const send = async () => {
      if (!file) return;
      setFormError(null);
      setStep({ state: 'sending', fraction: 0 });

      try {
         const created = await api.createVideo(title.trim(), description.trim(), file.size);
         const upload = api.uploadToConverter(created.ticket, file, (fraction) =>
            setStep({ state: 'sending', fraction }),
         );
         abort.current = upload.abort;
         await upload.promise;
         abort.current = null;

         setStep({ state: 'checking' });
         await api.completeUpload(created.video.videoId);

         setTitle('');
         setDescription('');
         setFile(null);
         setDuration(null);
      } catch (e) {
         if (!(e instanceof UploadError && e.message === 'aborted'))
            setFormError(e instanceof UploadError ? e.message || t('videos.upload_failed') : requestErrorMessage(e));
      }

      abort.current = null;
      setStep({ state: 'idle' });
      await load();
   };

   const watch = async (video: Video) => {
      try {
         setWatching(await api.fetchVideo(video.videoId));
      } catch (e) {
         setLoadError(requestErrorMessage(e));
      }
   };

   const remove = async (video: Video) => {
      if (!window.confirm(t('videos.delete_confirm', { title: video.title }))) return;
      try {
         await api.deleteVideo(video.videoId);
      } catch (e) {
         setLoadError(requestErrorMessage(e));
      }
      await load();
   };

   const sending = step.state !== 'idle';
   const canSend = !sending && file !== null && title.trim().length >= 3;

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar sx={{ gap: 2 }}>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="md" sx={{ py: { xs: 2, sm: 4 } }} id="my-videos-page">
            <Typography variant="h3" sx={{ fontSize: { xs: '2rem', sm: '2.6rem' }, mb: 0.5 }}>
               {t('videos.title')}
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 3 }}>
               {t('videos.subtitle')}
            </Typography>

            <Box
               id="video-form"
               sx={{
                  p: 2.5,
                  mb: 4,
                  borderRadius: '20px',
                  backgroundColor: brand.surface,
                  border: `1px solid ${brand.border}`,
               }}
            >
               <Button component="label" variant="outlined" disabled={sending} id="video-choose">
                  {file ? t('videos.change_file') : t('videos.choose_file')}
                  <input id="video-input" type="file" accept="video/*" hidden onChange={choose} />
               </Button>
               {file && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }} id="video-file-info">
                     {file.name} · {formatMegabytes(file.size)}
                     {duration !== null && ` · ${formatDuration(duration)}`}
                  </Typography>
               )}
               <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 0.5 }}>
                  {t('videos.limits')}
               </Typography>

               <TextField
                  id="video-title"
                  fullWidth
                  label={t('videos.video_title')}
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  disabled={sending}
                  sx={{ mt: 2 }}
               />
               <TextField
                  id="video-description"
                  fullWidth
                  multiline
                  minRows={2}
                  label={t('videos.video_description')}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  disabled={sending}
                  sx={{ mt: 2 }}
               />

               {step.state === 'sending' && (
                  <Box sx={{ mt: 2 }} id="video-progress">
                     <LinearProgress variant="determinate" value={Math.round(step.fraction * 100)} />
                     <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 0.5 }}>
                        <Typography variant="body2" color="text.secondary">
                           {t('videos.sending', { percent: Math.round(step.fraction * 100) })}
                        </Typography>
                        <Button size="small" color="inherit" id="video-cancel" onClick={() => abort.current?.()}>
                           {t('videos.cancel_upload')}
                        </Button>
                     </Box>
                  </Box>
               )}
               {step.state === 'checking' && (
                  <Alert severity="info" icon={<CircularProgress size={18} />} sx={{ mt: 2 }} id="video-checking">
                     {t('videos.checking')}
                  </Alert>
               )}
               {formError && (
                  <Alert severity="error" sx={{ mt: 2 }} id="video-error">
                     {formError}
                  </Alert>
               )}

               <Button
                  id="video-send"
                  variant="contained"
                  size="large"
                  fullWidth
                  disabled={!canSend}
                  onClick={send}
                  sx={{ mt: 2 }}
               >
                  {t('videos.upload')}
               </Button>
            </Box>

            {loadError && (
               <Alert severity="error" sx={{ mb: 2 }}>
                  {loadError}
               </Alert>
            )}
            {!loadError && videos === null && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
                  <CircularProgress />
               </Box>
            )}
            {videos?.length === 0 && (
               <Typography color="text.secondary" id="no-videos">
                  {t('videos.none_yet')}
               </Typography>
            )}

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }} id="video-list">
               {videos?.map((video) => (
                  <Box
                     key={video.videoId}
                     className="my-video"
                     sx={{
                        display: 'flex',
                        gap: 2,
                        p: 1.5,
                        borderRadius: '16px',
                        backgroundColor: brand.surface,
                        border: `1px solid ${brand.border}`,
                     }}
                  >
                     <Box
                        sx={{
                           width: 96,
                           height: 96,
                           flexShrink: 0,
                           borderRadius: '12px',
                           overflow: 'hidden',
                           backgroundColor: 'rgba(255,255,255,0.06)',
                           backgroundImage: video.posterUrl ? `url(${video.posterUrl})` : undefined,
                           backgroundSize: 'cover',
                           backgroundPosition: 'center',
                        }}
                     />
                     <Box sx={{ minWidth: 0, flex: 1 }}>
                        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                           <b>{video.title}</b>
                           <Chip
                              size="small"
                              className={`video-status video-status-${video.status}`}
                              color={
                                 video.status === 'ready' ? 'success' : video.status === 'failed' ? 'error' : 'default'
                              }
                              label={t(`videos.status_${video.status}`)}
                           />
                           {video.durationSeconds ? (
                              <Typography variant="body2" color="text.secondary">
                                 {formatDuration(video.durationSeconds)}
                              </Typography>
                           ) : null}
                        </Box>
                        <Typography variant="body2" color="text.secondary">
                           {DateTime.fromISO(video.createdAt).toLocaleString(DateTime.DATETIME_MED)}
                        </Typography>
                        {video.status === 'failed' && video.failureReason && (
                           <Typography variant="body2" color="error" className="video-failure">
                              {video.failureReason}
                           </Typography>
                        )}
                        <Box sx={{ mt: 0.5, display: 'flex', gap: 1 }}>
                           {video.status === 'ready' && (
                              <Button size="small" className="video-watch" onClick={() => watch(video)}>
                                 {t('videos.watch')}
                              </Button>
                           )}
                           <Button size="small" color="inherit" className="video-delete" onClick={() => remove(video)}>
                              {t('videos.delete')}
                           </Button>
                        </Box>
                     </Box>
                  </Box>
               ))}
            </Box>
         </Container>

         <Dialog open={watching !== null} onClose={() => setWatching(null)} maxWidth="sm" fullWidth>
            <DialogTitle>{watching?.title}</DialogTitle>
            <DialogContent>
               {watching?.playbackUrl && (
                  <video
                     id="video-player"
                     src={watching.playbackUrl}
                     poster={watching.posterUrl ?? undefined}
                     controls
                     playsInline
                     preload="metadata"
                     style={{ width: '100%', maxHeight: '70vh', borderRadius: 12, backgroundColor: '#000' }}
                  />
               )}
               {watching?.description && (
                  <Typography color="text.secondary" sx={{ mt: 1 }}>
                     {watching.description}
                  </Typography>
               )}
            </DialogContent>
         </Dialog>
      </Box>
   );
}
