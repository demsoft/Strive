import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   Container,
   Dialog,
   DialogContent,
   DialogTitle,
   Paper,
   TextField,
   Toolbar,
   Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { formatMoney } from 'src/features/sessions/money';
import { brand } from 'src/theme';
import { fetchSessionReports, keepSession, SessionReportItem, takeDownSession } from '../api';

/**
 * The sessions (matches) that people reported, with what the organizer declared about the right to stream them.
 * Reports of the owner of the rights come first. A moderator keeps the session or takes it down: it is called off and
 * every ticket is refunded.
 */
export default function AdminMatchesPage() {
   const { t } = useTranslation();
   const [items, setItems] = useState<SessionReportItem[] | null>(null);
   const [forbidden, setForbidden] = useState(false);
   const [failed, setFailed] = useState(false);
   const [taking, setTaking] = useState<SessionReportItem | null>(null);
   const [note, setNote] = useState('');
   const [actionError, setActionError] = useState<string | null>(null);

   const load = useCallback(() => {
      fetchSessionReports().then(
         (list) => {
            setItems(list);
            setFailed(false);
         },
         (e) => {
            const status = (e as { response?: { status?: number } }).response?.status;
            if (status === 403 || status === 401) setForbidden(true);
            else setFailed(true);
         },
      );
   }, []);

   useEffect(() => {
      load();
   }, [load]);

   const decide = async (action: () => Promise<void>) => {
      setActionError(null);
      try {
         await action();
         setTaking(null);
         setNote('');
         load();
      } catch {
         setActionError(t('matches.admin_action_failed'));
      }
   };

   const header = (
      <AppBar position="static" color="transparent" elevation={0}>
         <Toolbar sx={{ gap: 2 }}>
            <BrandLogo size={28} />
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
               {t('matches.admin_title')}
            </Typography>
            <Box sx={{ flex: 1 }} />
            <Button size="small" startIcon={<RefreshIcon />} onClick={load}>
               {t('admin.refresh')}
            </Button>
            <Button size="small" component={RouterLink} to="/admin" startIcon={<ArrowBackIcon />}>
               {t('admin.title')}
            </Button>
         </Toolbar>
      </AppBar>
   );

   if (forbidden)
      return (
         <>
            {header}
            <Container maxWidth="sm" sx={{ mt: 8 }}>
               <Alert severity="info" id="admin-matches-forbidden">
                  {t('admin.forbidden')}
               </Alert>
            </Container>
         </>
      );

   return (
      <>
         {header}
         <Container maxWidth="md" sx={{ pt: 3, pb: 6 }} id="admin-matches-page">
            {failed && (
               <Alert severity="error" sx={{ mb: 2 }}>
                  {t('matches.admin_failed')}
               </Alert>
            )}
            {items === null && !failed && <Typography color="text.secondary">{t('admin.loading')}</Typography>}
            {items?.length === 0 && (
               <Alert severity="success" id="admin-matches-none">
                  {t('matches.admin_none')}
               </Alert>
            )}
            {actionError && (
               <Alert severity="error" sx={{ mb: 2 }} id="admin-matches-error">
                  {actionError}
               </Alert>
            )}

            {items?.map((item) => (
               <Paper
                  key={item.sessionId}
                  className="reported-session"
                  sx={{ p: 2, mb: 2, borderRadius: '20px', border: `1px solid ${brand.border}` }}
               >
                  <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                     <b>{item.title}</b>
                     {item.reasons['rightsholder'] > 0 && (
                        <Chip size="small" color="error" label={t('matches.admin_rights_holder')} />
                     )}
                     <Chip size="small" label={t('matches.admin_reports', { count: item.openReports })} />
                  </Box>
                  <Typography variant="body2" color="text.secondary">
                     {item.creatorName}
                     {item.match?.competition ? ` · ${item.match.competition}` : ''}
                     {' · '}
                     {formatMoney(item.priceMinor, 'NGN')} · {t('matches.admin_tickets', { count: item.tickets })}
                  </Typography>
                  <Typography variant="body2" sx={{ mt: 1 }} className="session-rights">
                     {t('matches.admin_declared', {
                        basis: t(`matches.basis_short_${item.rightsBasis ?? 'own'}`),
                     })}
                     {item.rightsNote ? `: ${item.rightsNote}` : ''}
                  </Typography>
                  <Box sx={{ mt: 1, display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
                     {Object.entries(item.reasons).map(([reason, count]) => (
                        <Chip
                           key={reason}
                           size="small"
                           variant="outlined"
                           label={`${t(`matches.reason_${reason}`, reason)} · ${count}`}
                        />
                     ))}
                  </Box>
                  {item.details.map((d, i) => (
                     <Typography key={i} variant="body2" color="text.secondary" sx={{ mt: 0.75 }}>
                        “{d.text}” · {DateTime.fromISO(d.at).toLocaleString(DateTime.DATE_MED)}
                     </Typography>
                  ))}
                  <Box sx={{ mt: 1.5, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                     <Button
                        size="small"
                        component={RouterLink}
                        to={`/s/${item.sessionId}`}
                        target="_blank"
                        className="session-open"
                     >
                        {t('matches.admin_open')}
                     </Button>
                     <Button
                        size="small"
                        color="inherit"
                        className="session-keep"
                        onClick={() => decide(() => keepSession(item.sessionId))}
                     >
                        {t('matches.admin_keep')}
                     </Button>
                     <Button
                        size="small"
                        color="error"
                        className="session-takedown"
                        onClick={() => {
                           setTaking(item);
                           setNote('');
                        }}
                     >
                        {t('matches.admin_takedown')}
                     </Button>
                  </Box>
               </Paper>
            ))}
         </Container>

         <Dialog open={taking !== null} onClose={() => setTaking(null)} maxWidth="sm" fullWidth>
            <DialogTitle>{t('matches.admin_takedown')}</DialogTitle>
            <DialogContent>
               <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {t('matches.admin_takedown_text', { count: taking?.tickets ?? 0 })}
               </Typography>
               <TextField
                  id="takedown-note"
                  fullWidth
                  multiline
                  minRows={2}
                  label={t('matches.admin_takedown_note')}
                  value={note}
                  onChange={(e) => setNote(e.target.value.slice(0, 500))}
               />
               <Button
                  id="takedown-confirm"
                  variant="contained"
                  color="error"
                  fullWidth
                  sx={{ mt: 2 }}
                  onClick={() => taking && decide(() => takeDownSession(taking.sessionId, note.trim()))}
               >
                  {t('matches.admin_takedown_confirm')}
               </Button>
            </DialogContent>
         </Dialog>
      </>
   );
}
