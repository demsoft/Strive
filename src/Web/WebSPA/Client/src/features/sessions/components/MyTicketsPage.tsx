import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   CircularProgress,
   Container,
   List,
   ListItem,
   ListItemText,
   Toolbar,
   Typography,
} from '@mui/material';
import { DateTime } from 'luxon';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/sessions';
import { Ticket } from 'src/services/api/sessions';

/** The tickets a person bought, with a way into the room of every session. */
export default function MyTicketsPage() {
   const { t } = useTranslation();
   const history = useHistory();
   const [tickets, setTickets] = useState<Ticket[] | null>(null);
   const [error, setError] = useState<string | null>(null);

   useEffect(() => {
      api.fetchMyTickets().then(setTickets, (e) => setError(requestErrorMessage(e)));
   }, []);

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="sm" sx={{ py: 4 }} id="my-tickets-page">
            <Typography variant="h4" gutterBottom>
               {t('sessions.tickets_title')}
            </Typography>
            {error && <Alert severity="error">{error}</Alert>}
            {!error && tickets === null && <CircularProgress />}
            {tickets?.length === 0 && (
               <Typography color="text.secondary" id="no-tickets">
                  {t('sessions.no_tickets')}
               </Typography>
            )}
            <List disablePadding>
               {tickets?.map((ticket) => (
                  <ListItem
                     key={ticket.sessionId}
                     divider
                     className="my-ticket"
                     secondaryAction={
                        ticket.status === 'cancelled' ? (
                           <Chip size="small" color="warning" label={t('sessions.cancelled_chip')} />
                        ) : (
                           <Button
                              variant="contained"
                              size="small"
                              onClick={() => history.push(`/c/${ticket.conferenceId}`)}
                           >
                              {t('sessions.join')}
                           </Button>
                        )
                     }
                  >
                     <ListItemText
                        primary={<b>{ticket.title}</b>}
                        secondary={[
                           ticket.creatorName,
                           t(`sessions.category_${ticket.category}`),
                           ticket.startsAt
                              ? DateTime.fromISO(ticket.startsAt).toLocaleString(DateTime.DATETIME_MED)
                              : null,
                        ]
                           .filter(Boolean)
                           .join(' · ')}
                     />
                  </ListItem>
               ))}
            </List>
         </Container>
      </Box>
   );
}
