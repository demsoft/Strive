import { Alert, Box, Button, CircularProgress, Typography } from '@mui/material';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import * as api from 'src/services/api/videos';
import { Video } from 'src/services/api/videos';
import VideoTile from './VideoTile';

/** The latest videos of the creators (reels and lessons), a page at a time. Nothing is shown while there are none. */
export default function DiscoverVideos() {
   const { t } = useTranslation();
   const [videos, setVideos] = useState<Video[] | null>(null);
   const [next, setNext] = useState<string | null>(null);
   const [loadingMore, setLoadingMore] = useState(false);
   const [failed, setFailed] = useState(false);

   useEffect(() => {
      api.fetchFeed().then(
         (feed) => {
            setVideos(feed.items);
            setNext(feed.next ?? null);
         },
         () => setFailed(true),
      );
   }, []);

   const more = useCallback(async () => {
      if (!next) return;
      setLoadingMore(true);
      try {
         const feed = await api.fetchFeed(next);
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
   }, [next]);

   if (videos?.length === 0 && !failed) return null;

   return (
      <Box id="discover-videos" sx={{ maxWidth: 1180, width: '100%', mx: 'auto', py: 4 }}>
         <Typography variant="h4" sx={{ fontWeight: 800 }}>
            {t('videos.feed_title')}
         </Typography>
         <Typography color="text.secondary" sx={{ mb: 3 }}>
            {t('videos.feed_subtitle')}
         </Typography>

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
