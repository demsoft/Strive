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
import { brand } from 'src/theme';
import { decideOrganizer, fetchOrganizers, OrganizerItem } from '../api';

/** The creators that asked to publish football matches: approve them, or turn them down (also takes back an approval). */
export default function AdminOrganizersPage() {
   const { t } = useTranslation();
   const [items, setItems] = useState<OrganizerItem[] | null>(null);
   const [forbidden, setForbidden] = useState(false);
   const [failed, setFailed] = useState(false);
   const [deciding, setDeciding] = useState<{ item: OrganizerItem; approve: boolean } | null>(null);
   const [note, setNote] = useState('');
   const [actionError, setActionError] = useState<string | null>(null);

   const load = useCallback(() => {
      fetchOrganizers().then(
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

   const confirm = async () => {
      if (!deciding) return;
      setActionError(null);
      try {
         await decideOrganizer(deciding.item.creatorId, deciding.approve, note.trim());
         setDeciding(null);
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
               {t('matches.organizers_title')}
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
               <Alert severity="info" id="admin-organizers-forbidden">
                  {t('admin.forbidden')}
               </Alert>
            </Container>
         </>
      );

   return (
      <>
         {header}
         <Container maxWidth="md" sx={{ pt: 3, pb: 6 }} id="admin-organizers-page">
            {failed && (
               <Alert severity="error" sx={{ mb: 2 }}>
                  {t('matches.organizers_failed')}
               </Alert>
            )}
            {items === null && !failed && <Typography color="text.secondary">{t('admin.loading')}</Typography>}
            {items?.length === 0 && (
               <Alert severity="success" id="admin-organizers-none">
                  {t('matches.organizers_none')}
               </Alert>
            )}
            {actionError && (
               <Alert severity="error" sx={{ mb: 2 }} id="admin-organizers-error">
                  {actionError}
               </Alert>
            )}

            {items?.map((item) => (
               <Paper
                  key={item.creatorId}
                  className="organizer"
                  sx={{ p: 2, mb: 2, borderRadius: '20px', border: `1px solid ${brand.border}` }}
               >
                  <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                     <b>{item.organizationName}</b>
                     <Chip
                        size="small"
                        className={`organizer-status organizer-status-${item.status}`}
                        color={
                           item.status === 'approved' ? 'success' : item.status === 'rejected' ? 'error' : 'warning'
                        }
                        label={t(`matches.organizer_status_${item.status}`)}
                     />
                  </Box>
                  <Typography variant="body2" color="text.secondary">
                     {item.displayName}
                     {item.requestedAt && ` · ${DateTime.fromISO(item.requestedAt).toLocaleString(DateTime.DATE_MED)}`}
                     {' · '}
                     {t('matches.organizers_matches', { count: item.matches })}
                  </Typography>
                  <Typography sx={{ mt: 1, whiteSpace: 'pre-wrap' }} className="organizer-about">
                     {item.about}
                  </Typography>
                  {item.decisionNote && (
                     <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                        “{item.decisionNote}”
                     </Typography>
                  )}
                  <Box sx={{ mt: 1.5, display: 'flex', gap: 1 }}>
                     {item.status !== 'approved' && (
                        <Button
                           size="small"
                           variant="contained"
                           className="organizer-approve"
                           onClick={() => setDeciding({ item, approve: true })}
                        >
                           {t('matches.organizers_approve')}
                        </Button>
                     )}
                     {item.status !== 'rejected' && (
                        <Button
                           size="small"
                           color="error"
                           className="organizer-reject"
                           onClick={() => setDeciding({ item, approve: false })}
                        >
                           {item.status === 'approved'
                              ? t('matches.organizers_revoke')
                              : t('matches.organizers_reject')}
                        </Button>
                     )}
                  </Box>
               </Paper>
            ))}
         </Container>

         <Dialog open={deciding !== null} onClose={() => setDeciding(null)} maxWidth="sm" fullWidth>
            <DialogTitle>
               {deciding?.approve ? t('matches.organizers_approve') : t('matches.organizers_reject')}
            </DialogTitle>
            <DialogContent>
               <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {deciding?.approve ? t('matches.organizers_approve_text') : t('matches.organizers_reject_text')}
               </Typography>
               <TextField
                  id="organizer-note"
                  fullWidth
                  multiline
                  minRows={2}
                  label={t('matches.organizers_note')}
                  value={note}
                  onChange={(e) => setNote(e.target.value.slice(0, 300))}
               />
               <Button
                  id="organizer-confirm"
                  variant="contained"
                  color={deciding?.approve ? 'primary' : 'error'}
                  fullWidth
                  sx={{ mt: 2 }}
                  onClick={confirm}
               >
                  {t('matches.organizers_confirm')}
               </Button>
            </DialogContent>
         </Dialog>
      </>
   );
}
