import { Alert, Box, Button, Chip, Skeleton, Typography } from '@mui/material';
import VerifiedOutlinedIcon from '@mui/icons-material/VerifiedOutlined';
import { DateTime } from 'luxon';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory } from 'react-router-dom';
import Scoreboard from 'src/features/matches/Scoreboard';
import ScoreboardControls from 'src/features/matches/ScoreboardControls';
import OrganizerRequest from 'src/features/sessions/components/OrganizerRequest';
import { CreatorProfile } from 'src/services/api/creators';
import * as api from 'src/services/api/sessions';
import { Match, Session } from 'src/services/api/sessions';
import { brand } from 'src/theme';

/** The match to work on now: one that is on (it has a start time soon or no time), else the next to kick off. */
export function nextMatchOf(sessions: Session[]): Session | null {
   const matches = sessions.filter((x) => x.match && x.status === 'published');
   // a match whose score is not at full time is the one in hand; then by start
   const open = matches.filter((x) => x.match?.period !== 'ft');
   return (
      [...open].sort((a, b) => (a.startsAt ?? '9999').localeCompare(b.startsAt ?? '9999'))[0] ??
      [...matches].sort((a, b) => (b.startsAt ?? '').localeCompare(a.startsAt ?? ''))[0] ??
      null
   );
}

type Props = { profile: CreatorProfile; onProfileChanged: (profile: CreatorProfile) => void };

/**
 * The top of the dashboard of an organizer (Arena): whether they may publish matches, and the match in hand with the
 * way into its room and the buttons for its score.
 */
export default function MatchdayPanel({ profile, onProfileChanged }: Props) {
   const { t } = useTranslation();
   const history = useHistory();
   const [sessions, setSessions] = useState<Session[] | null>(null);
   const [failed, setFailed] = useState(false);
   const [match, setMatch] = useState<Match | null>(null);

   const approved = profile.organizerStatus === 'approved';

   useEffect(() => {
      if (!approved) return;
      api.fetchMySessions().then(
         (list) => {
            setSessions(list);
            setMatch(nextMatchOf(list)?.match ?? null);
         },
         () => setFailed(true),
      );
   }, [approved]);

   if (!approved)
      return (
         <Box
            id="matchday-approval"
            sx={{ p: 3, borderRadius: '24px', border: `1px solid ${brand.border}`, background: brand.gradientSoft }}
         >
            <OrganizerRequest profile={profile} onChanged={onProfileChanged} />
         </Box>
      );

   const current = sessions ? nextMatchOf(sessions) : null;
   const start = current?.startsAt ? DateTime.fromISO(current.startsAt) : null;

   return (
      <Box
         id="matchday-panel"
         sx={{
            p: { xs: 2.5, sm: 3 },
            borderRadius: '24px',
            border: `1px solid ${brand.border}`,
            background: brand.gradientSoft,
         }}
      >
         <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5, flexWrap: 'wrap' }}>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
               {t('arena_home.desk')}
            </Typography>
            <Chip
               size="small"
               color="success"
               icon={<VerifiedOutlinedIcon />}
               label={t('matches.organizer_status_approved')}
               id="organizer-approved"
            />
         </Box>

         {failed && <Alert severity="error">{t('matches.page_failed')}</Alert>}
         {!failed && sessions === null && <Skeleton variant="rounded" height={90} sx={{ borderRadius: '16px' }} />}

         {sessions && !current && (
            <Box id="matchday-none">
               <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {t('arena_home.desk_none')}
               </Typography>
               <Button component={RouterLink} to="/creator/sessions" variant="contained" id="desk-new-match">
                  {t('arena_home.desk_new')}
               </Button>
            </Box>
         )}

         {current && match && (
            <Box id="matchday-current">
               <Scoreboard match={match} />
               <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                  {current.match?.competition ? `${current.match.competition} · ` : ''}
                  {start ? start.toLocaleString(DateTime.DATETIME_MED) : t('matches.no_date')}
               </Typography>
               <ScoreboardControls sessionId={current.sessionId} match={match} onChanged={setMatch} />
               <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 1 }}>
                  <Button
                     id="desk-open-room"
                     variant="contained"
                     onClick={() => history.push(`/c/${current.conferenceId}`)}
                  >
                     {t('sessions.open_room')}
                  </Button>
                  <Button id="desk-open-page" component={RouterLink} to={`/s/${current.sessionId}`} color="inherit">
                     {t('arena_home.desk_page')}
                  </Button>
                  <Button id="desk-new-match" component={RouterLink} to="/creator/sessions" color="inherit">
                     {t('arena_home.desk_new')}
                  </Button>
               </Box>
            </Box>
         )}
      </Box>
   );
}
