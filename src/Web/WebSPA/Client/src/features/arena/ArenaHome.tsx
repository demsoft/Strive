import { Alert, Box, Button, Chip, Container, Skeleton, Typography } from '@mui/material';
import SportsSoccerOutlinedIcon from '@mui/icons-material/SportsSoccerOutlined';
import { DateTime } from 'luxon';
import React, { useEffect, useMemo, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import SiteHeader from 'src/features/creators/SiteHeader';
import MatchCard from 'src/features/matches/MatchCard';
import Scoreboard from 'src/features/matches/Scoreboard';
import { byCompetition, matchdayOf } from 'src/features/matches/matchday';
import { formatMoney, MATCH_CATEGORY } from 'src/features/sessions/money';
import * as api from 'src/services/api/sessions';
import { DiscoverSession } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import ClubsRow from './ClubsRow';

function Section({ id, title, children }: { id: string; title: string; children: React.ReactNode }) {
   return (
      <Box id={id} sx={{ mb: 4 }}>
         <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>
            {title}
         </Typography>
         {children}
      </Box>
   );
}

const grid = { display: 'grid', gap: 2, gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))' };

function Featured({ match }: { match: DiscoverSession }) {
   const { t } = useTranslation();
   const start = match.startsAt ? DateTime.fromISO(match.startsAt) : null;

   return (
      <Box
         id="arena-featured"
         sx={{
            p: { xs: 2.5, sm: 4 },
            mb: 4,
            borderRadius: '28px',
            background: brand.gradientSoft,
            border: `1px solid ${brand.border}`,
            textAlign: 'center',
         }}
      >
         <Box sx={{ display: 'flex', justifyContent: 'center', gap: 1, mb: 1.5, alignItems: 'center' }}>
            {match.isLive && <Chip size="small" color="error" label={t('sessions.live_now')} />}
            <Typography variant="body2" color="text.secondary">
               {match.match?.competition ?? t('matches.friendly')}
            </Typography>
         </Box>
         {match.match && <Scoreboard match={match.match} />}
         <Typography color="text.secondary" sx={{ mt: 1.5 }}>
            {start ? start.toLocaleString(DateTime.DATETIME_FULL) : t('matches.no_date')}
            {' · '}
            {t('sessions.with', { name: match.creatorName })}
         </Typography>
         <Button
            id="arena-featured-cta"
            component={RouterLink}
            to={`/s/${match.sessionId}`}
            variant="contained"
            size="large"
            sx={{ mt: 2.5, boxShadow: `0 12px 32px ${brand.accentShadow}`, borderRadius: 4 }}
         >
            {match.isHost
               ? t('sessions.your_session')
               : match.hasTicket
               ? t(match.isLive ? 'matches.watch_now' : 'matches.have_pass')
               : `${t('matches.get_pass')} · ${formatMoney(match.priceMinor, match.currency)}`}
         </Button>
      </Box>
   );
}

/**
 * The start page of Arena, for everybody: the matchday. What is live now, the match to watch next, today's matches and
 * the rest of the week by competition, then the clubs and leagues. Fans sign in on the way to a pass.
 */
export default function ArenaHome() {
   const { t } = useTranslation();
   const auth = useAuth();
   const [matches, setMatches] = useState<DiscoverSession[] | null>(null);
   const [failed, setFailed] = useState(false);

   useEffect(() => {
      api.fetchDiscover(MATCH_CATEGORY).then(setMatches, () => setFailed(true));
   }, []);

   const day = useMemo(() => matchdayOf(matches ?? []), [matches]);
   const rest = day.featured
      ? [...day.today, ...day.week, ...day.later].filter((x) => x.sessionId !== day.featured?.sessionId)
      : [];

   return (
      <Box sx={{ minHeight: '100%' }} id="arena-home">
         <SiteHeader />
         <Container maxWidth="lg" sx={{ py: { xs: 2, sm: 4 } }}>
            <Box sx={{ mb: 3 }}>
               <Typography variant="overline" color="text.secondary">
                  {DateTime.now().toLocaleString({ weekday: 'long', day: 'numeric', month: 'long' })}
               </Typography>
               <Typography variant="h3" sx={{ fontWeight: 900, fontSize: { xs: '2rem', sm: '2.8rem' } }}>
                  {t('arena_home.title')}
               </Typography>
               <Typography color="text.secondary">{t('arena_home.subtitle')}</Typography>
            </Box>

            {failed && (
               <Alert severity="error" id="arena-failed">
                  {t('matches.page_failed')}
               </Alert>
            )}
            {!failed && matches === null && <Skeleton variant="rounded" height={220} sx={{ borderRadius: '28px' }} />}

            {matches && matches.length === 0 && (
               <Box
                  id="arena-empty"
                  sx={{ p: 4, mb: 4, borderRadius: '24px', textAlign: 'center', border: `1px dashed ${brand.border}` }}
               >
                  <SportsSoccerOutlinedIcon sx={{ fontSize: 40, opacity: 0.6 }} />
                  <Typography sx={{ fontWeight: 700 }}>{t('arena_home.empty_title')}</Typography>
                  <Typography color="text.secondary">{t('arena_home.empty_text')}</Typography>
               </Box>
            )}

            {day.featured && <Featured match={day.featured} />}

            {day.live.length > 1 && (
               <Section id="arena-live" title={t('arena_home.live')}>
                  <Box sx={grid}>
                     {day.live
                        .filter((x) => x.sessionId !== day.featured?.sessionId)
                        .map((m) => (
                           <MatchCard key={m.sessionId} match={m} />
                        ))}
                  </Box>
               </Section>
            )}

            {day.today.filter((x) => x.sessionId !== day.featured?.sessionId).length > 0 && (
               <Section id="arena-today" title={t('arena_home.today')}>
                  <Box sx={grid}>
                     {day.today
                        .filter((x) => x.sessionId !== day.featured?.sessionId)
                        .map((m) => (
                           <MatchCard key={m.sessionId} match={m} />
                        ))}
                  </Box>
               </Section>
            )}

            {byCompetition([...day.week, ...day.later].filter((x) => x.sessionId !== day.featured?.sessionId)).map(
               (group) => (
                  <Section
                     key={group.competition ?? 'none'}
                     id="arena-competition"
                     title={group.competition ?? t('arena_home.other_matches')}
                  >
                     <Box sx={grid}>
                        {group.matches.map((m) => (
                           <MatchCard key={m.sessionId} match={m} />
                        ))}
                     </Box>
                  </Section>
               ),
            )}

            {rest.length > 0 && (
               <Button id="arena-all-matches" component={RouterLink} to="/matches" color="inherit" sx={{ mb: 4 }}>
                  {t('arena_home.all_matches')}
               </Button>
            )}

            <ClubsRow />

            {!auth.isAuthenticated && (
               <Box
                  id="arena-organizers-cta"
                  sx={{ mt: 2, p: 3, borderRadius: '24px', border: `1px solid ${brand.border}`, textAlign: 'center' }}
               >
                  <Typography sx={{ fontWeight: 800 }}>{t('arena_home.organizer_title')}</Typography>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                     {t('arena_home.organizer_text')}
                  </Typography>
                  <Button
                     id="arena-become-organizer"
                     variant="outlined"
                     onClick={() => auth.signinRedirect({ state: { url: '/creator' }, prompt: 'create' })}
                  >
                     {t('roles.creator_button')}
                  </Button>
               </Box>
            )}
         </Container>
      </Box>
   );
}
