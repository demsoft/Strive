import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   CircularProgress,
   Container,
   IconButton,
   List,
   ListItem,
   ListItemText,
   MenuItem,
   TextField,
   Toolbar,
   Tooltip,
   Typography,
} from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/sessions';
import { Session } from 'src/services/api/sessions';
import { formatMoney, isPriceInRange, parsePriceToMinor, SESSION_CATEGORIES } from '../money';

/** For creators: publish a session (a price and a link to share) and see the sessions that were published. */
export default function MySessionsPage() {
   const { t } = useTranslation();
   const history = useHistory();
   const [sessions, setSessions] = useState<Session[] | null>(null);
   const [loadError, setLoadError] = useState<string | null>(null);

   const [title, setTitle] = useState('');
   const [category, setCategory] = useState<string>('teach');
   const [description, setDescription] = useState('');
   const [price, setPrice] = useState('');
   const [startsAt, setStartsAt] = useState('');
   const [saving, setSaving] = useState(false);
   const [saveError, setSaveError] = useState<string | null>(null);
   const [created, setCreated] = useState<Session | null>(null);
   const [copied, setCopied] = useState<string | null>(null);

   const load = useCallback(async () => {
      try {
         setSessions(await api.fetchMySessions());
         setLoadError(null);
      } catch (error) {
         setLoadError(requestErrorMessage(error));
      }
   }, []);

   useEffect(() => {
      load();
   }, [load]);

   const priceMinor = parsePriceToMinor(price);
   const priceOk = priceMinor !== null && isPriceInRange(priceMinor);
   const canPublish = title.trim().length >= 3 && priceOk && !saving;

   const handlePublish = async () => {
      if (priceMinor === null) return;
      setSaving(true);
      setSaveError(null);
      try {
         const session = await api.createSession({
            title: title.trim(),
            description: description.trim() || undefined,
            category,
            priceMinor,
            startsAt: startsAt ? new Date(startsAt).toISOString() : undefined,
         });
         setCreated(session);
         setTitle('');
         setDescription('');
         setPrice('');
         setStartsAt('');
         await load();
      } catch (error) {
         setSaveError(requestErrorMessage(error));
      } finally {
         setSaving(false);
      }
   };

   const copy = async (sessionId: string) => {
      const link = api.sessionLink(sessionId);
      try {
         await navigator.clipboard.writeText(link);
         setCopied(sessionId);
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
      }
   };

   const cancel = async (session: Session) => {
      if (!window.confirm(t('sessions.cancel_confirm', { title: session.title }))) return;
      await api.cancelSession(session.sessionId);
      await load();
   };

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar sx={{ gap: 2 }}>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
               <Box sx={{ flex: 1 }} />
               <Button color="inherit" component={RouterLink} to="/creator">
                  {t('creator.title')}
               </Button>
            </Toolbar>
         </AppBar>

         <Container maxWidth="sm" sx={{ py: 4 }} id="my-sessions-page">
            <Typography variant="h4" gutterBottom>
               {t('sessions.my_title')}
            </Typography>

            <Typography variant="h6" sx={{ mt: 3 }} gutterBottom>
               {t('sessions.new_title')}
            </Typography>
            <TextField
               id="session-form-title"
               fullWidth
               label={t('sessions.title')}
               value={title}
               onChange={(e) => setTitle(e.target.value)}
               slotProps={{ htmlInput: { maxLength: 100 } }}
               sx={{ mb: 2 }}
            />
            <TextField
               id="session-form-category"
               select
               fullWidth
               label={t('sessions.category')}
               value={category}
               onChange={(e) => setCategory(e.target.value)}
               sx={{ mb: 2 }}
            >
               {SESSION_CATEGORIES.map((c) => (
                  <MenuItem key={c} value={c}>
                     {t(`sessions.category_${c}`)}
                  </MenuItem>
               ))}
            </TextField>
            <TextField
               id="session-form-description"
               fullWidth
               multiline
               minRows={3}
               label={t('sessions.description')}
               value={description}
               onChange={(e) => setDescription(e.target.value)}
               slotProps={{ htmlInput: { maxLength: 2000 } }}
               sx={{ mb: 2 }}
            />
            <TextField
               id="session-form-price"
               fullWidth
               label={t('sessions.price')}
               value={price}
               onChange={(e) => setPrice(e.target.value)}
               error={price !== '' && !priceOk}
               helperText={price !== '' && !priceOk ? t('sessions.price_range') : t('sessions.price_hint')}
               slotProps={{ htmlInput: { inputMode: 'decimal' } }}
               sx={{ mb: 2 }}
            />
            <TextField
               id="session-form-starts"
               fullWidth
               type="datetime-local"
               label={t('sessions.starts')}
               helperText={t('sessions.starts_hint')}
               value={startsAt}
               onChange={(e) => setStartsAt(e.target.value)}
               slotProps={{ inputLabel: { shrink: true } }}
               sx={{ mb: 2 }}
            />
            {saveError && (
               <Alert severity="error" sx={{ mb: 2 }} id="session-form-error">
                  {saveError}
               </Alert>
            )}
            {created && (
               <Alert
                  severity="success"
                  sx={{ mb: 2 }}
                  id="session-created"
                  action={
                     <Button color="inherit" size="small" onClick={() => copy(created.sessionId)}>
                        {t('sessions.copy_link')}
                     </Button>
                  }
               >
                  {t('sessions.created', { title: created.title })}
               </Alert>
            )}
            <Button
               id="session-publish"
               variant="contained"
               size="large"
               fullWidth
               disabled={!canPublish}
               onClick={handlePublish}
            >
               {saving ? <CircularProgress size={22} color="inherit" /> : t('sessions.publish')}
            </Button>

            <Typography variant="h6" sx={{ mt: 5 }} gutterBottom>
               {t('sessions.published_title')}
            </Typography>
            {loadError && <Alert severity="error">{loadError}</Alert>}
            {!loadError && sessions === null && <CircularProgress />}
            {sessions?.length === 0 && (
               <Typography color="text.secondary" id="no-sessions">
                  {t('sessions.none_yet')}
               </Typography>
            )}
            <List disablePadding>
               {sessions?.map((session) => (
                  <ListItem
                     key={session.sessionId}
                     divider
                     className="my-session"
                     secondaryAction={
                        <>
                           <Tooltip
                              title={copied === session.sessionId ? t('sessions.copied') : t('sessions.copy_link')}
                           >
                              <IconButton aria-label={t('sessions.copy_link')} onClick={() => copy(session.sessionId)}>
                                 <ContentCopyIcon fontSize="small" />
                              </IconButton>
                           </Tooltip>
                        </>
                     }
                  >
                     <ListItemText
                        primary={
                           <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                              <b>{session.title}</b>
                              {session.status === 'cancelled' && (
                                 <Chip size="small" color="warning" label={t('sessions.cancelled_chip')} />
                              )}
                           </Box>
                        }
                        secondary={
                           <>
                              {formatMoney(session.priceMinor, session.currency)} ·{' '}
                              {t(`sessions.category_${session.category}`)}
                              {session.startsAt &&
                                 ` · ${DateTime.fromISO(session.startsAt).toLocaleString(DateTime.DATETIME_MED)}`}
                              {session.status === 'published' && (
                                 <Box sx={{ mt: 0.5, display: 'flex', gap: 1 }}>
                                    <Button size="small" onClick={() => history.push(`/c/${session.conferenceId}`)}>
                                       {t('sessions.open_room')}
                                    </Button>
                                    <Button size="small" color="inherit" onClick={() => cancel(session)}>
                                       {t('sessions.cancel')}
                                    </Button>
                                 </Box>
                              )}
                           </>
                        }
                        slotProps={{ secondary: { component: 'div' } }}
                     />
                  </ListItem>
               ))}
            </List>
         </Container>
      </Box>
   );
}
