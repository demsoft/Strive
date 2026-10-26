import {
   Alert,
   Box,
   Button,
   CircularProgress,
   Container,
   InputAdornment,
   Skeleton,
   Snackbar,
   TextField,
   ToggleButton,
   ToggleButtonGroup,
   Typography,
} from '@mui/material';
import GroupsOutlinedIcon from '@mui/icons-material/GroupsOutlined';
import SearchIcon from '@mui/icons-material/Search';
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router-dom';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/follows';
import { CreatorCard as Card, CreatorSort } from 'src/services/api/follows';
import { brand } from 'src/theme';
import CreatorCard from './CreatorCard';
import SiteHeader from './SiteHeader';

const PAGE = 24;
type View = CreatorSort | 'following';

/** Creators for fans to find and follow: search, popular or new, or the ones already followed. */
export default function CreatorsPage() {
   const { t } = useTranslation();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const location = useLocation();
   const [view, setView] = useState<View>('popular');
   const [text, setText] = useState('');
   const [query, setQuery] = useState('');
   const [cards, setCards] = useState<Card[] | null>(null);
   const [total, setTotal] = useState(0);
   const [failed, setFailed] = useState(false);
   const [loadingMore, setLoadingMore] = useState(false);
   const [message, setMessage] = useState<string | null>(null);

   // the words are searched for when the person stopped typing
   useEffect(() => {
      const timer = setTimeout(() => setQuery(text.trim()), 300);
      return () => clearTimeout(timer);
   }, [text]);

   const ask = useCallback(
      (skip: number) =>
         api.fetchCreatorCards({
            q: query,
            sort: view === 'following' ? 'popular' : view,
            following: view === 'following',
            skip,
            limit: PAGE,
         }),
      [query, view],
   );

   useEffect(() => {
      // wait until it is known whether the person is signed in: who they follow depends on it
      if (auth.isLoading) return;
      let cancelled = false;
      setCards(null);
      setFailed(false);
      ask(0).then(
         (list) => {
            if (cancelled) return;
            setCards(list.items);
            setTotal(list.total);
         },
         () => !cancelled && setFailed(true),
      );
      return () => {
         cancelled = true;
      };
   }, [ask, auth.isLoading, token]);

   const more = async () => {
      if (!cards) return;
      setLoadingMore(true);
      try {
         const list = await ask(cards.length);
         setCards((current) => {
            const known = new Set((current ?? []).map((x) => x.creator.creatorId));
            return [...(current ?? []), ...list.items.filter((x) => !known.has(x.creator.creatorId))];
         });
         setTotal(list.total);
      } catch {
         setFailed(true);
      }
      setLoadingMore(false);
   };

   const toggleFollow = async (card: Card) => {
      if (!token) return auth.signinRedirect({ state: { url: location.pathname } });

      const id = card.creator.creatorId;
      const was = card.following;
      const set = (following: boolean) =>
         setCards((current) =>
            (current ?? []).map((x) =>
               x.creator.creatorId === id
                  ? {
                       ...x,
                       following,
                       followers: Math.max(0, x.followers + (following === x.following ? 0 : following ? 1 : -1)),
                    }
                  : x,
            ),
         );
      set(!was);
      try {
         await (was ? api.unfollowCreator(id) : api.followCreator(id));
      } catch (e) {
         // it goes back, and the person is told why (for example that nobody follows themself)
         set(was);
         setMessage(requestErrorMessage(e) || t('creators.follow_failed'));
      }
   };

   const changeView = (_: unknown, next: View | null) => {
      if (!next) return;
      if (next === 'following' && !token) return auth.signinRedirect({ state: { url: location.pathname } });
      setView(next);
   };

   return (
      <Box sx={{ minHeight: '100%' }} id="creators-page">
         <SiteHeader withoutConnections />

         <Box
            sx={{
               pt: { xs: 4, md: 7 },
               pb: { xs: 3, md: 4 },
               background: `radial-gradient(900px 340px at 15% -10%, ${brand.glowA}, transparent), radial-gradient(700px 300px at 90% 0%, ${brand.glowB}, transparent)`,
            }}
         >
            <Container maxWidth="lg">
               <Typography variant="h2" sx={{ fontSize: { xs: '2rem', md: '3rem' } }} id="creators-title">
                  {t('creators.title')}
               </Typography>
               <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 620 }}>
                  {t('creators.subtitle')}
               </Typography>

               <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center', mt: 3 }}>
                  <TextField
                     id="creators-search"
                     value={text}
                     onChange={(e) => setText(e.target.value)}
                     placeholder={t('creators.search')}
                     size="small"
                     sx={{
                        flex: '1 1 280px',
                        maxWidth: 460,
                        '& .MuiOutlinedInput-root': {
                           borderRadius: '999px',
                           backgroundColor: 'rgba(255,255,255,0.05)',
                        },
                     }}
                     slotProps={{
                        input: {
                           startAdornment: (
                              <InputAdornment position="start">
                                 <SearchIcon fontSize="small" />
                              </InputAdornment>
                           ),
                        },
                     }}
                  />
                  <ToggleButtonGroup exclusive size="small" value={view} onChange={changeView} id="creators-views">
                     <ToggleButton
                        value="popular"
                        id="creators-view-popular"
                        sx={{ px: 2, borderRadius: '999px 0 0 999px' }}
                     >
                        {t('creators.sort_popular')}
                     </ToggleButton>
                     <ToggleButton value="new" id="creators-view-new" sx={{ px: 2 }}>
                        {t('creators.sort_new')}
                     </ToggleButton>
                     <ToggleButton
                        value="following"
                        id="creators-view-following"
                        sx={{ px: 2, borderRadius: '0 999px 999px 0' }}
                     >
                        {t('creators.tab_following')}
                     </ToggleButton>
                  </ToggleButtonGroup>
               </Box>
            </Container>
         </Box>

         <Container maxWidth="lg" sx={{ pb: 8 }}>
            {failed && (
               <Alert severity="error" id="creators-failed" sx={{ mb: 2 }}>
                  {t('creators.failed')}
               </Alert>
            )}

            {cards === null && !failed && (
               <Box sx={grid} id="creators-loading">
                  {[0, 1, 2, 3, 4, 5, 6, 7].map((i) => (
                     <Skeleton key={i} variant="rounded" height={320} sx={{ borderRadius: '22px' }} />
                  ))}
               </Box>
            )}

            {cards?.length === 0 && (
               <Box id="creators-none" sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
                  <GroupsOutlinedIcon sx={{ fontSize: 56, opacity: 0.5 }} />
                  <Typography sx={{ mt: 1 }}>
                     {view === 'following' && !query ? t('creators.none_following') : t('creators.none')}
                  </Typography>
               </Box>
            )}

            {cards && cards.length > 0 && (
               <Box sx={grid} id="creators-grid">
                  {cards.map((card) => (
                     <CreatorCard key={card.creator.creatorId} card={card} onFollow={() => toggleFollow(card)} />
                  ))}
               </Box>
            )}

            {cards && cards.length < total && (
               <Box sx={{ textAlign: 'center', mt: 4 }}>
                  <Button id="creators-more" variant="outlined" disabled={loadingMore} onClick={more}>
                     {loadingMore ? <CircularProgress size={20} color="inherit" /> : t('creators.load_more')}
                  </Button>
               </Box>
            )}
         </Container>

         <Snackbar open={!!message} autoHideDuration={4000} onClose={() => setMessage(null)} message={message} />
      </Box>
   );
}

const grid = {
   display: 'grid',
   gap: 2.5,
   gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))',
} as const;
