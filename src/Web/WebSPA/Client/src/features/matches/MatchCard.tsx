import { Box, Chip, Typography } from '@mui/material';
import { DateTime } from 'luxon';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { formatMoney } from 'src/features/sessions/money';
import { DiscoverSession } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import Scoreboard from './Scoreboard';

/** One match as a card: the competition, the teams or the score, the kick-off, the price and the way to the page. */
export default function MatchCard({ match }: { match: DiscoverSession }) {
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
         {match.match && match.match.period && match.match.period !== 'upcoming' ? (
            <Scoreboard match={match.match} compact />
         ) : (
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
         )}
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
