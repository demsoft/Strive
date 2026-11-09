import {
   Alert,
   Box,
   Button,
   Chip,
   Container,
   InputAdornment,
   Skeleton,
   TextField,
   ToggleButton,
   ToggleButtonGroup,
   Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import SportsSoccerOutlinedIcon from '@mui/icons-material/SportsSoccerOutlined';
import { DateTime } from 'luxon';
import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import SiteHeader from 'src/features/creators/SiteHeader';
import { formatMoney, MATCH_CATEGORY } from 'src/features/sessions/money';
import * as api from 'src/services/api/sessions';
import { DiscoverSession } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import { competitionsOf, filterMatches, sortMatches, WhenFilter } from './matchFilters';

const WHEN: WhenFilter[] = ['all', 'live', 'today', 'week'];

function MatchCard({ match }: { match: DiscoverSession }) {
   const { t } = useTranslation();
   const start = match.startsAt ? DateTime.fromISO(match.startsAt) : null;

   return (
      <Box
         className="match-card"
         component={RouterLink}
         to={`/s/${match.sessionId}`}
         sx={{
            display: 'flex',
            flexDirection: 'column',
            gap: 1,
            p: 2.25,
            borderRadius: '20px',
            textDecoration: 'none',
            color: 'inherit',
            backgroundColor: brand.surface,
            border: `1px solid ${brand.border}`,
            transition: 'transform 140ms ease, border-color 140ms ease',
            '&:hover': { transform: 'translateY(-2px)', borderColor: 'rgba(255,255,255,0.18)' },
         }}
      >
         <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
            <Typography variant="caption" color="text.secondary" className="match-competition" noWrap>
               {match.match?.competition ?? t('matches.friendly')}
            </Typography>
            {match.isLive && <Chip size="small" color="error" label={t('sessions.live_now')} className="match-live" />}
         </Box>
         <Box sx={{ textAlign: 'center', py: 1 }}>
            <Typography sx={{ fontWeight: 800, fontSize: '1.15rem' }} className="match-teams">
               {match.match ? `${match.match.homeTeam}` : match.title}
            </Typography>
            {match.match && (
               <>
                  <Typography color="text.secondary" variant="body2">
                     {t('matches.versus')}
                  </Typography>
                  <Typography sx={{ fontWeight: 800, fontSize: '1.15rem' }}>{match.match.awayTeam}</Typography>
               </>
            )}
         </Box>
         <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }} className="match-kickoff">
            {start ? start.toLocaleString(DateTime.DATETIME_MED) : t('matches.no_date')}
         </Typography>
         <Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center' }}>
            {t('sessions.with', { name: match.creatorName })}
         </Typography>
         <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mt: 1 }}>
            <Typography sx={{ fontWeight: 800 }}>{formatMoney(match.priceMinor, match.currency)}</Typography>
            <Typography variant="body2" color="primary" sx={{ fontWeight: 700 }}>
               {match.isHost
                  ? t('sessions.your_session')
                  : match.hasTicket
                  ? t('matches.have_pass')
                  : t('matches.get_pass')}
            </Typography>
         </Box>
      </Box>
   );
}

/** Every match that is on or coming, for everybody: search, competition and when it is. */
export default function MatchesPage() {
   const { t } = useTranslation();
   const [matches, setMatches] = useState<DiscoverSession[] | null>(null);
   const [failed, setFailed] = useState(false);
   const [query, setQuery] = useState('');
   const [competition, setCompetition] = useState<string | null>(null);
   const [when, setWhen] = useState<WhenFilter>('all');

   useEffect(() => {
      api.fetchDiscover(MATCH_CATEGORY).then(setMatches, () => setFailed(true));
   }, []);

   const competitions = useMemo(() => competitionsOf(matches ?? []), [matches]);
   const shown = useMemo(
      () => sortMatches(filterMatches(matches ?? [], query, competition, when)),
      [matches, query, competition, when],
   );

   return (
      <Box sx={{ minHeight: '100%' }} id="matches-page">
         <SiteHeader />
         <Container maxWidth="lg" sx={{ py: { xs: 2, sm: 4 } }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 3 }}>
               <Box
                  sx={{
                     width: 52,
                     height: 52,
                     borderRadius: '16px',
                     display: 'grid',
                     placeItems: 'center',
                     background: brand.gradient,
                     color: '#fff',
                     boxShadow: `0 8px 24px ${brand.accentShadow}`,
                  }}
               >
                  <SportsSoccerOutlinedIcon />
               </Box>
               <Box>
                  <Typography variant="h4" sx={{ fontWeight: 800, fontSize: { xs: '1.6rem', sm: '2rem' } }}>
                     {t('matches.page_title')}
                  </Typography>
                  <Typography color="text.secondary" variant="body2">
                     {t('matches.page_subtitle')}
                  </Typography>
               </Box>
            </Box>

            <TextField
               id="matches-search"
               fullWidth
               placeholder={t('matches.search')}
               value={query}
               onChange={(e) => setQuery(e.target.value)}
               slotProps={{
                  input: {
                     startAdornment: (
                        <InputAdornment position="start">
                           <SearchIcon fontSize="small" />
                        </InputAdornment>
                     ),
                  },
               }}
               sx={{ mb: 1.5, maxWidth: 480 }}
            />
            <ToggleButtonGroup
               exclusive
               size="small"
               value={when}
               onChange={(_, value: WhenFilter | null) => value && setWhen(value)}
               sx={{ mb: 1.5, display: 'flex', flexWrap: 'wrap' }}
            >
               {WHEN.map((w) => (
                  <ToggleButton key={w} value={w} id={`matches-when-${w}`}>
                     {t(`matches.when_${w}`)}
                  </ToggleButton>
               ))}
            </ToggleButtonGroup>
            {competitions.length > 0 && (
               <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }} id="matches-competitions">
                  <Chip
                     label={t('matches.all_competitions')}
                     color={competition === null ? 'primary' : 'default'}
                     onClick={() => setCompetition(null)}
                  />
                  {competitions.map((name) => (
                     <Chip
                        key={name}
                        className="match-competition-chip"
                        label={name}
                        color={competition === name ? 'primary' : 'default'}
                        onClick={() => setCompetition(competition === name ? null : name)}
                     />
                  ))}
               </Box>
            )}

            {failed && (
               <Alert severity="error" id="matches-failed">
                  {t('matches.page_failed')}
               </Alert>
            )}
            {!failed && matches === null && (
               <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))' }}>
                  {[0, 1, 2].map((i) => (
                     <Skeleton key={i} variant="rounded" height={210} sx={{ borderRadius: '20px' }} />
                  ))}
               </Box>
            )}
            {matches && matches.length === 0 && (
               <Typography color="text.secondary" id="matches-none">
                  {t('matches.page_none')}
               </Typography>
            )}
            {matches && matches.length > 0 && shown.length === 0 && (
               <Typography color="text.secondary" id="matches-no-match">
                  {t('matches.page_no_match')}{' '}
                  <Button
                     size="small"
                     onClick={() => {
                        setQuery('');
                        setCompetition(null);
                        setWhen('all');
                     }}
                  >
                     {t('matches.clear_filters')}
                  </Button>
               </Typography>
            )}
            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))' }}>
               {shown.map((match) => (
                  <MatchCard key={match.sessionId} match={match} />
               ))}
            </Box>
         </Container>
      </Box>
   );
}
