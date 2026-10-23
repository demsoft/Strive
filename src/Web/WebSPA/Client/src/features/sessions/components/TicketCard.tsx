import { Box, Button, Chip, Typography } from '@mui/material';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import { DateTime } from 'luxon';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { categoryGradient, categoryStyle } from 'src/features/gbedu/ui/categories';
import CategoryBadge from 'src/features/gbedu/ui/CategoryBadge';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { Ticket } from 'src/services/api/sessions';
import { brand } from 'src/theme';

type Props = {
   ticket: Ticket;
   onJoin: () => void;
   /** a ticket of a session that is over is shown quietly */
   past?: boolean;
};

/**
 * A ticket: a coloured band with the icon of the category, the session on the right, and a notch between the two like
 * a real ticket has. The way into the room is the button.
 */
export default function TicketCard({ ticket, onJoin, past }: Props) {
   const { t } = useTranslation();
   const { Icon } = categoryStyle(ticket.category);
   const cancelled = ticket.status === 'cancelled';
   const when = ticket.startsAt ? DateTime.fromISO(ticket.startsAt) : null;

   return (
      <Box
         className="my-ticket"
         sx={{
            position: 'relative',
            display: 'flex',
            flexDirection: { xs: 'column', sm: 'row' },
            borderRadius: '20px',
            overflow: 'hidden',
            backgroundColor: brand.surface,
            border: `1px solid ${brand.border}`,
            opacity: past || cancelled ? 0.7 : 1,
            boxShadow: past ? 'none' : '0 14px 40px rgba(0, 0, 0, 0.35)',
            transition: 'transform 120ms ease, box-shadow 120ms ease',
            '&:hover': past || cancelled ? undefined : { transform: 'translateY(-2px)' },
         }}
      >
         <Box
            sx={{
               flex: { sm: '0 0 112px' },
               minHeight: { xs: 64, sm: 'auto' },
               display: 'flex',
               alignItems: 'center',
               justifyContent: 'center',
               background: cancelled ? 'rgba(255, 255, 255, 0.08)' : categoryGradient(ticket.category),
               color: '#fff',
            }}
         >
            <Icon sx={{ fontSize: 44, opacity: 0.95 }} />
         </Box>

         {/* the notches between the band and the ticket */}
         <Box
            aria-hidden
            sx={{
               position: 'absolute',
               display: { xs: 'none', sm: 'block' },
               left: 112 - 10,
               top: -10,
               bottom: -10,
               width: 20,
               background: `radial-gradient(circle at 10px 0, ${brand.background} 10px, transparent 11px) top / 20px 22px no-repeat, radial-gradient(circle at 10px 22px, ${brand.background} 10px, transparent 11px) bottom / 20px 22px no-repeat`,
               pointerEvents: 'none',
            }}
         />

         <Box sx={{ flex: 1, minWidth: 0, p: 2.5, display: 'flex', flexDirection: 'column', gap: 1 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
               <CategoryBadge category={ticket.category} />
               {cancelled && <Chip size="small" color="warning" label={t('sessions.cancelled_chip')} />}
               {ticket.isLive && !cancelled && (
                  <Chip size="small" color="error" label={t('sessions.live_now')} className="ticket-live" />
               )}
               {ticket.refunded && (
                  <Chip size="small" color="success" label={t('sessions.refunded_chip')} className="ticket-refunded" />
               )}
            </Box>
            <Typography variant="h6" sx={{ fontWeight: 700, lineHeight: 1.25 }} className="ticket-title">
               {ticket.title}
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
               <HostAvatar
                  name={ticket.creatorName}
                  size={24}
                  background={categoryGradient(ticket.category)}
                  photoUrl={creatorPhotoUrl(ticket.creatorId, ticket.creatorPhotoVersion)}
               />
               <Typography variant="body2" color="text.secondary">
                  {t('sessions.with', { name: ticket.creatorName })}
               </Typography>
            </Box>
            {when && (
               <Typography variant="body2" color="text.secondary" className="ticket-when">
                  {when.toLocaleString(DateTime.DATETIME_MED)}
               </Typography>
            )}
         </Box>

         {!cancelled && (
            <Box
               sx={{ p: 2.5, display: 'flex', alignItems: 'center', justifyContent: { xs: 'stretch', sm: 'center' } }}
            >
               <Button
                  variant={past ? 'outlined' : 'contained'}
                  size="large"
                  fullWidth
                  endIcon={<ArrowForwardIcon />}
                  onClick={onJoin}
                  className="ticket-join"
                  sx={{ minWidth: 150 }}
               >
                  {t('sessions.join')}
               </Button>
            </Box>
         )}
      </Box>
   );
}
