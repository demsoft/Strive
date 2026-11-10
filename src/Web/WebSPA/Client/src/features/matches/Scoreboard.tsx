import { Box, Typography } from '@mui/material';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Match } from 'src/services/api/sessions';
import { brand } from 'src/theme';

type Props = {
   match: Match;
   /** a smaller scoreboard for cards */
   compact?: boolean;
};

/** The two teams with the score between them and the period of the match. Before it starts: the teams only. */
export default function Scoreboard({ match, compact }: Props) {
   const { t } = useTranslation();
   const started = match.period != null && match.period !== 'upcoming';
   const live = match.period === '1h' || match.period === '2h';

   const team = (name: string, align: 'left' | 'right') => (
      <Typography
         noWrap
         sx={{
            flex: 1,
            minWidth: 0,
            fontWeight: 800,
            textAlign: align,
            fontSize: compact ? '0.95rem' : { xs: '1rem', sm: '1.25rem' },
         }}
      >
         {name}
      </Typography>
   );

   return (
      <Box
         className="scoreboard"
         sx={{
            display: 'flex',
            alignItems: 'center',
            gap: compact ? 1 : 2,
            px: compact ? 1.5 : 2.5,
            py: compact ? 1 : 1.25,
            borderRadius: '16px',
            background: brand.gradientSoft,
            border: `1px solid ${brand.border}`,
         }}
      >
         {team(match.homeTeam, 'right')}
         <Box sx={{ textAlign: 'center', minWidth: compact ? 70 : 110 }}>
            <Typography
               className="scoreboard-score"
               sx={{ fontWeight: 900, lineHeight: 1, fontSize: compact ? '1.3rem' : { xs: '1.6rem', sm: '2rem' } }}
            >
               {started ? `${match.homeScore ?? 0} : ${match.awayScore ?? 0}` : t('matches.versus')}
            </Typography>
            {started && (
               <Typography
                  className="scoreboard-period"
                  variant="caption"
                  sx={{ fontWeight: 700, color: live ? 'error.main' : 'text.secondary', letterSpacing: 0.4 }}
               >
                  {live && '● '}
                  {t(`matches.period_${match.period}`)}
               </Typography>
            )}
         </Box>
         {team(match.awayTeam, 'left')}
      </Box>
   );
}
