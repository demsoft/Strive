import { Alert, Box, Chip, CircularProgress, InputAdornment, TextField, Typography } from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { DateTime } from 'luxon';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { formatMoney, SESSION_CATEGORIES } from 'src/features/sessions/money';
import * as api from 'src/services/api/sessions';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import { DiscoverSession } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import { categoryGradient, categoryStyle } from './ui/categories';
import CategoryBadge from './ui/CategoryBadge';
import HostAvatar from './ui/HostAvatar';

/**
 * The sessions that match what the person typed (in the title, the text, or the name of the host: no capitals, no
 * accents, every word has to be found) and the chosen category (none: all).
 */
export function filterSessions(sessions: DiscoverSession[], query: string, category: string | null) {
   const normalize = (text: string) =>
      text
         .normalize('NFD')
         .replace(/[\u0300-\u036f]/g, '')
         .toLowerCase();
   const words = normalize(query).split(/\s+/).filter(Boolean);

   return sessions.filter((session) => {
      if (category && session.category !== category) return false;
      const haystack = normalize(`${session.title} ${session.description ?? ''} ${session.creatorName}`);
      return words.every((word) => haystack.includes(word));
   });
}

export function SessionTile({ session }: { session: DiscoverSession }) {
   const { t } = useTranslation();
   const { Icon } = categoryStyle(session.category);
   const live = session.isLive;
   const when = session.startsAt ? DateTime.fromISO(session.startsAt) : null;

   return (
      <Box
         className="discover-session"
         component={RouterLink}
         to={`/s/${session.sessionId}`}
         sx={{
            display: 'flex',
            flexDirection: 'column',
            borderRadius: '20px',
            overflow: 'hidden',
            textDecoration: 'none',
            color: 'inherit',
            backgroundColor: brand.surface,
            border: `1px solid ${brand.border}`,
            transition: 'transform 120ms ease',
            '&:hover': { transform: 'translateY(-2px)' },
         }}
      >
         <Box
            sx={{
               height: 72,
               display: 'flex',
               alignItems: 'center',
               justifyContent: 'space-between',
               px: 2,
               background: categoryGradient(session.category),
               color: '#fff',
            }}
         >
            <Icon sx={{ fontSize: 36 }} />
            {live && <Chip size="small" color="error" label={t('sessions.live_now')} className="discover-live" />}
         </Box>
         <Box sx={{ p: 2, display: 'flex', flexDirection: 'column', gap: 1, flex: 1 }}>
            <Box>
               <CategoryBadge category={session.category} />
            </Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.25 }}>
               {session.title}
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
               <HostAvatar
                  name={session.creatorName}
                  size={22}
                  background={categoryGradient(session.category)}
                  photoUrl={creatorPhotoUrl(session.creatorId, session.creatorPhotoVersion)}
               />
               <Typography variant="body2" color="text.secondary">
                  {t('sessions.with', { name: session.creatorName })}
               </Typography>
            </Box>
            {when && (
               <Typography variant="body2" color="text.secondary">
                  {when.toLocaleString(DateTime.DATETIME_MED)}
               </Typography>
            )}
            <Box sx={{ flex: 1 }} />
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mt: 1 }}>
               <Typography sx={{ fontWeight: 800 }}>{formatMoney(session.priceMinor, session.currency)}</Typography>
               <Typography variant="body2" color="primary" sx={{ fontWeight: 700 }}>
                  {session.isHost
                     ? t('sessions.your_session')
                     : session.hasTicket
                     ? t('sessions.view_ticket')
                     : t('sessions.get_ticket')}
               </Typography>
            </Box>
         </Box>
      </Box>
   );
}

/** What is on: the live sessions and the coming ones, each with a way to the page where the ticket is bought. */
export default function DiscoverSessions() {
   const { t } = useTranslation();
   const [sessions, setSessions] = useState<DiscoverSession[] | null>(null);
   const [failed, setFailed] = useState(false);
   const [query, setQuery] = useState('');
   const [category, setCategory] = useState<string | null>(null);

   useEffect(() => {
      api.fetchDiscover().then(setSessions, () => setFailed(true));
   }, []);

   // live ones first, then by start
   const sorted = filterSessions(sessions ?? [], query, category).sort(
      (a, b) => Number(b.isLive) - Number(a.isLive) || (a.startsAt ?? '9999').localeCompare(b.startsAt ?? '9999'),
   );

   return (
      <Box id="discover" sx={{ maxWidth: 1180, width: '100%', mx: 'auto', py: 4 }}>
         <Typography variant="h4" sx={{ fontWeight: 800 }}>
            {t('sessions.discover_title')}
         </Typography>
         <Typography color="text.secondary" sx={{ mb: 3 }}>
            {t('sessions.discover_subtitle')}
         </Typography>

         {failed && <Alert severity="error">{t('sessions.discover_failed')}</Alert>}
         {!failed && sessions === null && (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
               <CircularProgress />
            </Box>
         )}
         {sessions?.length === 0 && (
            <Typography color="text.secondary" id="discover-empty">
               {t('sessions.discover_none')}
            </Typography>
         )}
         {sessions && sessions.length > 0 && (
            <Box sx={{ mb: 3 }} id="discover-filters">
               <TextField
                  id="discover-search"
                  fullWidth
                  size="small"
                  placeholder={t('sessions.search')}
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
               <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  <Chip
                     id="discover-cat-all"
                     label={t('sessions.filter_all')}
                     color={category === null ? 'primary' : 'default'}
                     onClick={() => setCategory(null)}
                  />
                  {SESSION_CATEGORIES.map((c) => (
                     <Chip
                        key={c}
                        id={`discover-cat-${c}`}
                        label={t(`sessions.category_${c}`)}
                        color={category === c ? 'primary' : 'default'}
                        onClick={() => setCategory(category === c ? null : c)}
                     />
                  ))}
               </Box>
            </Box>
         )}
         {sessions && sessions.length > 0 && sorted.length === 0 && (
            <Typography color="text.secondary" id="discover-no-match">
               {t('sessions.no_match')}
            </Typography>
         )}
         {sorted.length > 0 && (
            <Box
               sx={{
                  display: 'grid',
                  gap: 2,
                  gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' },
               }}
            >
               {sorted.map((session) => (
                  <SessionTile key={session.sessionId} session={session} />
               ))}
            </Box>
         )}
      </Box>
   );
}
