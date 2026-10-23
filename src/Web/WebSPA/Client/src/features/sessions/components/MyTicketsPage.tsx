import { Alert, AppBar, Box, Button, CircularProgress, Container, Tab, Tabs, Toolbar, Typography } from '@mui/material';
import ConfirmationNumberOutlinedIcon from '@mui/icons-material/ConfirmationNumberOutlined';
import { DateTime } from 'luxon';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/sessions';
import { Pass, Ticket } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import TicketCard from './TicketCard';

/** A session may run over its time: it is over this long after it was meant to end (unless the room is still open). */
const GRACE_MINUTES = 60;

/** Upcoming: not called off and not over. Past: everything else. A session whose room is open is never past. */
export function splitTickets(tickets: Ticket[], now = DateTime.now()) {
   const upcoming: Ticket[] = [];
   const past: Ticket[] = [];
   for (const ticket of tickets) {
      const endsAt = ticket.startsAt
         ? DateTime.fromISO(ticket.startsAt).plus({ minutes: (ticket.durationMinutes ?? 60) + GRACE_MINUTES })
         : null;
      const over = ticket.status === 'cancelled' || (!ticket.isLive && endsAt !== null && endsAt < now);
      (over ? past : upcoming).push(ticket);
   }

   // the next session first
   upcoming.sort((a, b) => (a.startsAt ?? '9999').localeCompare(b.startsAt ?? '9999'));
   return { upcoming, past };
}

/** The tickets a person bought, as tickets, with a way into the room of every session. */
export default function MyTicketsPage() {
   const { t } = useTranslation();
   const history = useHistory();
   const [tickets, setTickets] = useState<Ticket[] | null>(null);
   const [error, setError] = useState<string | null>(null);
   const [tab, setTab] = useState<'upcoming' | 'past'>('upcoming');
   const [passes, setPasses] = useState<Pass[]>([]);

   useEffect(() => {
      api.fetchMyTickets().then(setTickets, (e) => setError(requestErrorMessage(e)));
      // the passes are a plus: the page works without them
      api.fetchMyPasses().then(setPasses, () => undefined);
   }, []);

   const stopRenewing = async (pass: Pass) => {
      if (!pass.subscriptionId) return;
      if (!window.confirm(t('creator.pass_stop_confirm', { name: pass.creatorName }))) return;
      try {
         await api.cancelPassSubscription(pass.subscriptionId);
         setPasses(await api.fetchMyPasses());
      } catch (e) {
         setError(requestErrorMessage(e));
      }
   };

   const { upcoming, past } = splitTickets(tickets ?? []);
   const shown = tab === 'upcoming' ? upcoming : past;

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar sx={{ gap: 2 }}>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="md" sx={{ py: { xs: 2, sm: 4 } }} id="my-tickets-page">
            <Box sx={{ mb: 3 }}>
               <Typography variant="h3" sx={{ fontSize: { xs: '2rem', sm: '2.6rem' } }}>
                  <span
                     style={{
                        backgroundImage: brand.gradient,
                        WebkitBackgroundClip: 'text',
                        backgroundClip: 'text',
                        color: 'transparent',
                        WebkitTextFillColor: 'transparent',
                     }}
                  >
                     {t('sessions.tickets_title')}
                  </span>
               </Typography>
               <Typography color="text.secondary" sx={{ mt: 0.5 }}>
                  {t('sessions.tickets_subtitle')}
               </Typography>
            </Box>

            {passes.length > 0 && (
               <Box id="my-passes" sx={{ mb: 3 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                     {t('creator.my_passes')}
                  </Typography>
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                     {passes.map((pass) => (
                        <Box key={pass.creatorId}>
                           <Box
                              component={RouterLink}
                              to={`/u/${pass.creatorSlug || pass.creatorId}`}
                              className="my-pass"
                              sx={{
                                 display: 'flex',
                                 justifyContent: 'space-between',
                                 gap: 2,
                                 p: 1.5,
                                 borderRadius: '14px',
                                 textDecoration: 'none',
                                 color: 'inherit',
                                 backgroundColor: brand.surface,
                                 border: `1px solid ${brand.border}`,
                              }}
                           >
                              <b>{pass.creatorName}</b>
                              <span>
                                 {t('creator.pass_until', {
                                    date: DateTime.fromISO(pass.validUntil).toLocaleString(DateTime.DATE_MED),
                                 })}
                              </span>
                           </Box>
                           {pass.subscriptionId && (
                              <Box
                                 className="my-pass-renews"
                                 sx={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 1,
                                    px: 1.5,
                                    pt: 0.5,
                                    flexWrap: 'wrap',
                                 }}
                              >
                                 <Typography variant="body2" color="text.secondary">
                                    {t('creator.pass_renews_on', {
                                       date: DateTime.fromISO(pass.renewsAt ?? pass.validUntil).toLocaleString(
                                          DateTime.DATE_MED,
                                       ),
                                    })}
                                 </Typography>
                                 <Button
                                    size="small"
                                    color="inherit"
                                    className="stop-renewing"
                                    onClick={() => stopRenewing(pass)}
                                 >
                                    {t('creator.pass_stop_renewing')}
                                 </Button>
                              </Box>
                           )}
                        </Box>
                     ))}
                  </Box>
               </Box>
            )}

            {error && <Alert severity="error">{error}</Alert>}
            {!error && tickets === null && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
                  <CircularProgress />
               </Box>
            )}

            {tickets && tickets.length > 0 && (
               <>
                  <Tabs
                     value={tab}
                     onChange={(_, value) => setTab(value)}
                     sx={{
                        mb: 2,
                        minHeight: 40,
                        '& .MuiTab-root': { minHeight: 40, textTransform: 'none', fontWeight: 700 },
                     }}
                  >
                     <Tab
                        value="upcoming"
                        id="tickets-tab-upcoming"
                        label={`${t('sessions.tab_upcoming')} · ${upcoming.length}`}
                     />
                     <Tab value="past" id="tickets-tab-past" label={`${t('sessions.tab_past')} · ${past.length}`} />
                  </Tabs>

                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                     {shown.map((ticket) => (
                        <TicketCard
                           key={ticket.sessionId}
                           ticket={ticket}
                           past={tab === 'past'}
                           onJoin={() => history.push(`/c/${ticket.conferenceId}`)}
                        />
                     ))}
                     {shown.length === 0 && (
                        <Typography color="text.secondary" id="tickets-tab-empty" sx={{ py: 3 }}>
                           {tab === 'upcoming' ? t('sessions.no_upcoming') : t('sessions.no_past')}
                        </Typography>
                     )}
                  </Box>
               </>
            )}

            {tickets?.length === 0 && (
               <Box
                  id="no-tickets"
                  sx={{
                     textAlign: 'center',
                     p: { xs: 4, sm: 6 },
                     borderRadius: '24px',
                     backgroundColor: brand.surface,
                     border: `1px dashed ${brand.border}`,
                  }}
               >
                  <ConfirmationNumberOutlinedIcon sx={{ fontSize: 56, color: 'primary.main', mb: 1 }} />
                  <Typography variant="h5" gutterBottom>
                     {t('sessions.no_tickets_title')}
                  </Typography>
                  <Typography color="text.secondary" sx={{ maxWidth: 420, mx: 'auto', mb: 3 }}>
                     {t('sessions.no_tickets')}
                  </Typography>
                  <Button component={RouterLink} to="/" variant="contained" size="large" id="find-session">
                     {t('sessions.find_session')}
                  </Button>
               </Box>
            )}
         </Container>
      </Box>
   );
}
