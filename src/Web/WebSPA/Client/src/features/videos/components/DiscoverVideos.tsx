import { Alert, Box, Button, CircularProgress, Typography } from '@mui/material';
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import * as api from 'src/services/api/videos';
import { Video } from 'src/services/api/videos';
import VideoTile from './VideoTile';

/** The latest videos of the creators (reels and lessons), a page at a time. Nothing is shown while there are none. */
export default function DiscoverVideos() {
   const { t } = useTranslation();
   const signedIn = !!useAuth().user;
   // only the videos of the creators the person follows
   const [only, setOnly] = useState(false);
   const [videos, setVideos] = useState<Video[] | null>(null);
   const [next, setNext] = useState<string | null>(null);
   const [loadingMore, setLoadingMore] = useState(false);
   const [failed, setFailed] = useState(false);

   useEffect(() => {
      let cancelled = false;
      setVideos(null);
      setNext(null);
      setFailed(false);
      api.fetchFeed(undefined, 12, only).then(
         (feed) => {
            if (cancelled) return;
            setVideos(feed.items);
            setNext(feed.next ?? null);
         },
         () => !cancelled && setFailed(true),
      );
      return () => {
         cancelled = true;
      };
   }, [only]);

   const more = useCallback(async () => {
      if (!next) return;
      setLoadingMore(true);
      try {
         const feed = await api.fetchFeed(next, 12, only);
         // a video that arrived meanwhile must not be shown twice
         setVideos((current) => {
            const known = new Set((current ?? []).map((x) => x.videoId));
            return [...(current ?? []), ...feed.items.filter((x) => !known.has(x.videoId))];
         });
         setNext(feed.next ?? null);
      } catch {
         setFailed(true);
      }
      setLoadingMore(false);
   }, [next, only]);

   if (videos?.length === 0 && !failed && !only) return null;

   return (
      <Box id="discover-videos" sx={{ maxWidth: 1180, width: '100%', mx: 'auto', py: 4 }}>
         <Box sx={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 2, mb: 3 }}>
            <Box>
               <Typography variant="h4" sx={{ fontWeight: 800 }}>
                  {t('videos.feed_title')}
               </Typography>
               <Typography color="text.secondary">{t('videos.feed_subtitle')}</Typography>
            </Box>
            {videos && videos.length > 0 && (
               <Button
                  id="discover-videos-reels"
                  variant="contained"
                  component={RouterLink}
                  to={only ? '/reels?following=1' : '/reels'}
               >
                  {t('videos.reels_open')}
               </Button>
            )}
         </Box>

         {signedIn && (
            <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
               <Button
                  id="discover-videos-tab-all"
                  size="small"
                  variant={only ? 'outlined' : 'contained'}
                  color={only ? 'inherit' : 'primary'}
                  onClick={() => setOnly(false)}
               >
                  {t('social.tab_all')}
               </Button>
               <Button
                  id="discover-videos-tab-following"
                  size="small"
                  variant={only ? 'contained' : 'outlined'}
                  color={only ? 'primary' : 'inherit'}
                  onClick={() => setOnly(true)}
               >
                  {t('social.tab_following')}
               </Button>
            </Box>
         )}
         {videos?.length === 0 && only && !failed && (
            <Typography id="discover-videos-following-empty" color="text.secondary">
               {t('social.following_empty')}
            </Typography>
         )}

         {failed && (
            <Alert severity="error" id="discover-videos-failed" sx={{ mb: 2 }}>
               {t('videos.feed_failed')}
            </Alert>
         )}
         {videos === null && !failed && (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
               <CircularProgress />
            </Box>
         )}
         {videos && videos.length > 0 && (
            <Box
               sx={{
                  display: 'grid',
                  gap: 2,
                  gridTemplateColumns: { xs: 'repeat(2, 1fr)', sm: 'repeat(3, 1fr)', md: 'repeat(4, 1fr)' },
               }}
            >
               {videos.map((video) => (
                  <VideoTile key={video.videoId} video={video} />
               ))}
            </Box>
         )}
         {next && (
            <Box sx={{ textAlign: 'center', mt: 3 }}>
               <Button id="discover-videos-more" variant="outlined" disabled={loadingMore} onClick={more}>
                  {loadingMore ? <CircularProgress size={20} color="inherit" /> : t('videos.load_more')}
               </Button>
            </Box>
         )}
      </Box>
   );
}
