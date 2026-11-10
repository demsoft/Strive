import { Box, Typography } from '@mui/material';
import { DateTime } from 'luxon';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { MatchResult } from 'src/services/api/sessions';
import { brand } from 'src/theme';

/** The matches of a club or league that are over, with the final score. Nothing is shown without any. */
export default function ClubResults({ results }: { results: MatchResult[] }) {
   const { t } = useTranslation();
   if (results.length === 0) return null;

   return (
      <Box id="club-results" sx={{ mb: 4 }}>
         <Typography variant="h5" sx={{ fontWeight: 800, mb: 2 }}>
            {t('arena_home.results')}
         </Typography>
         <Box sx={{ display: 'grid', gap: 1 }}>
            {results.map((result) => (
               <Box
                  key={result.sessionId}
                  className="club-result"
                  component={RouterLink}
                  to={`/s/${result.sessionId}`}
                  sx={{
                     display: 'flex',
                     alignItems: 'center',
                     gap: 1.5,
                     p: 1.5,
                     borderRadius: '16px',
                     textDecoration: 'none',
                     color: 'inherit',
                     backgroundColor: brand.surface,
                     border: `1px solid ${brand.border}`,
                  }}
               >
                  <Typography noWrap sx={{ flex: 1, textAlign: 'right', fontWeight: 700 }}>
                     {result.match.homeTeam}
                  </Typography>
                  <Typography className="club-result-score" sx={{ fontWeight: 900, minWidth: 60, textAlign: 'center' }}>
                     {result.match.homeScore ?? 0} : {result.match.awayScore ?? 0}
                  </Typography>
                  <Typography noWrap sx={{ flex: 1, fontWeight: 700 }}>
                     {result.match.awayTeam}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'none', sm: 'block' } }}>
                     {result.match.competition ? `${result.match.competition} · ` : ''}
                     {result.startsAt ? DateTime.fromISO(result.startsAt).toLocaleString(DateTime.DATE_MED) : ''}
                  </Typography>
               </Box>
            ))}
         </Box>
      </Box>
   );
}
