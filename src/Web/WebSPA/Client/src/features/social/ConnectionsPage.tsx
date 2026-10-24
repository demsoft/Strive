import {
   Alert,
   Box,
   Button,
   CircularProgress,
   Container,
   Skeleton,
   Snackbar,
   Tab,
   Tabs,
   Typography,
} from '@mui/material';
import PeopleOutlinedIcon from '@mui/icons-material/PeopleOutlined';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory, useLocation } from 'react-router-dom';
import { requestErrorMessage } from 'src/features/creator/errors';
import CreatorCard from 'src/features/creators/CreatorCard';
import SiteHeader from 'src/features/creators/SiteHeader';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import * as api from 'src/services/api/follows';
import { CreatorCard as Card, Follower } from 'src/services/api/follows';
import { brand } from 'src/theme';

const PAGE = 50;
type Tab = 'following' | 'followers';

/** Whom the person follows (and can stop following), and, for a creator, who follows them. */
export default function ConnectionsPage() {
   const { t } = useTranslation();
   const auth = useAuth();
   const history = useHistory();
   const location = useLocation();
   const tab: Tab = new URLSearchParams(location.search).get('tab') === 'followers' ? 'followers' : 'following';

   const [following, setFollowing] = useState<Card[] | null>(null);
   const [followers, setFollowers] = useState<Follower[] | null>(null);
   const [followersTotal, setFollowersTotal] = useState(0);
   // null: not known yet; false: the person is no creator, there is no list of followers
   const [isCreator, setIsCreator] = useState<boolean | null>(null);
   const [failed, setFailed] = useState(false);
   const [loadingMore, setLoadingMore] = useState(false);
   const [message, setMessage] = useState<string | null>(null);

   useEffect(() => {
      let cancelled = false;
      api.fetchCreatorCards({ following: true, sort: 'new', limit: 60 }).then(
         (list) => !cancelled && setFollowing(list.items),
         () => !cancelled && setFailed(true),
      );
      api.fetchMyFollowers(0, PAGE).then(
         (list) => {
            if (cancelled) return;
            setIsCreator(list !== null);
            setFollowers(list?.items ?? []);
            setFollowersTotal(list?.total ?? 0);
         },
         () => !cancelled && setIsCreator(false),
      );
      return () => {
         cancelled = true;
      };
   }, []);

   const moreFollowers = async () => {
      if (!followers) return;
      setLoadingMore(true);
      try {
         const list = await api.fetchMyFollowers(followers.length, PAGE);
         setFollowers((current) => [...(current ?? []), ...(list?.items ?? [])]);
         setFollowersTotal(list?.total ?? followersTotal);
      } catch {
         setFailed(true);
      }
      setLoadingMore(false);
   };

   const unfollowOrFollow = useCallback(
      async (card: Card) => {
         const id = card.creator.creatorId;
         const was = card.following;
         const set = (value: boolean) =>
            setFollowing((current) =>
               (current ?? []).map((x) => (x.creator.creatorId === id ? { ...x, following: value } : x)),
            );
         set(!was);
         try {
            await (was ? api.unfollowCreator(id) : api.followCreator(id));
         } catch (e) {
            set(was);
            setMessage(requestErrorMessage(e));
         }
      },
      [setFollowing],
   );

   const choose = (_: unknown, next: Tab) =>
      history.replace(next === 'followers' ? '/connections?tab=followers' : '/connections');
   // somebody who is no creator has no followers tab, even when the link asks for it
   const shown: Tab = tab === 'followers' && isCreator ? 'followers' : 'following';
   const followingCount = following?.filter((x) => x.following).length;

   return (
      <Box sx={{ minHeight: '100%' }} id="connections-page">
         <SiteHeader />
         <Box
            sx={{
               pt: { xs: 4, md: 6 },
               pb: 2,
               background: `radial-gradient(900px 320px at 12% -10%, ${brand.glowA}, transparent)`,
            }}
         >
            <Container maxWidth="md">
               <Typography variant="h3" sx={{ fontSize: { xs: '1.9rem', md: '2.5rem' } }} id="connections-title">
                  {t('social.connections_title')}
               </Typography>
               <Typography color="text.secondary" sx={{ mt: 1 }}>
                  {t('social.connections_subtitle')}
               </Typography>
               <Tabs value={shown} onChange={choose} sx={{ mt: 3 }} id="connections-tabs">
                  <Tab
                     value="following"
                     id="connections-tab-following"
                     label={`${t('social.connections_tab_following')}${
                        followingCount !== undefined ? ` · ${followingCount}` : ''
                     }`}
                  />
                  {isCreator && (
                     <Tab
                        value="followers"
                        id="connections-tab-followers"
                        label={`${t('social.connections_tab_followers')} · ${followersTotal}`}
                     />
                  )}
               </Tabs>
            </Container>
         </Box>

         <Container maxWidth="md" sx={{ pb: 8, pt: 2 }}>
            {failed && (
               <Alert severity="error" id="connections-failed" sx={{ mb: 2 }}>
                  {t('social.connections_followers_failed')}
               </Alert>
            )}

            {shown === 'following' && (
               <>
                  {following === null && (
                     <Box
                        sx={{ display: 'grid', gap: 2, gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))' }}
                     >
                        {[0, 1, 2].map((i) => (
                           <Skeleton key={i} variant="rounded" height={300} sx={{ borderRadius: '22px' }} />
                        ))}
                     </Box>
                  )}
                  {following?.length === 0 && (
                     <Box id="connections-following-none" sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
                        <PeopleOutlinedIcon sx={{ fontSize: 56, opacity: 0.5 }} />
                        <Typography sx={{ mb: 2 }}>{t('social.connections_following_none')}</Typography>
                        <Button
                           id="connections-find"
                           variant="contained"
                           component={RouterLink}
                           to="/creators"
                           sx={{ background: brand.gradient, borderRadius: '999px', px: 3 }}
                        >
                           {t('social.connections_find')}
                        </Button>
                     </Box>
                  )}
                  {following && following.length > 0 && (
                     <Box
                        id="connections-following"
                        sx={{ display: 'grid', gap: 2.5, gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))' }}
                     >
                        {following.map((card) => (
                           <CreatorCard
                              key={card.creator.creatorId}
                              card={card}
                              onFollow={() => unfollowOrFollow(card)}
                           />
                        ))}
                     </Box>
                  )}
               </>
            )}

            {shown === 'followers' && (
               <>
                  {followers === null && <CircularProgress />}
                  {followers?.length === 0 && (
                     <Box id="connections-followers-none" sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
                        <PeopleOutlinedIcon sx={{ fontSize: 56, opacity: 0.5 }} />
                        <Typography sx={{ mb: 2 }}>{t('social.connections_followers_none')}</Typography>
                        <Button id="connections-your-page" component={RouterLink} to="/creator" variant="outlined">
                           {t('social.connections_your_page')}
                        </Button>
                     </Box>
                  )}
                  {followers && followers.length > 0 && (
                     <Box id="connections-followers">
                        {followers.map((follower, index) => (
                           <Box
                              key={index}
                              className="follower"
                              sx={{
                                 display: 'flex',
                                 alignItems: 'center',
                                 gap: 1.5,
                                 py: 1.25,
                                 borderTop: index ? `1px solid ${brand.border}` : 'none',
                              }}
                           >
                              <HostAvatar name={follower.name} size={44} background={brand.gradient} />
                              <Box sx={{ minWidth: 0, flex: 1 }}>
                                 <Typography className="follower-name" sx={{ fontWeight: 700 }} noWrap>
                                    {follower.name}
                                 </Typography>
                                 <Typography className="follower-since" variant="body2" color="text.secondary">
                                    {t('social.connections_followers_since', {
                                       when: DateTime.fromISO(follower.since).toRelative() ?? '',
                                    })}
                                 </Typography>
                              </Box>
                           </Box>
                        ))}
                        {followers.length < followersTotal && (
                           <Box sx={{ textAlign: 'center', mt: 2 }}>
                              <Button id="connections-more" disabled={loadingMore} onClick={moreFollowers}>
                                 {loadingMore ? (
                                    <CircularProgress size={20} color="inherit" />
                                 ) : (
                                    t('social.connections_followers_more')
                                 )}
                              </Button>
                           </Box>
                        )}
                     </Box>
                  )}
               </>
            )}
         </Container>
         <Snackbar open={!!message} autoHideDuration={4000} onClose={() => setMessage(null)} message={message} />
      </Box>
   );
}
