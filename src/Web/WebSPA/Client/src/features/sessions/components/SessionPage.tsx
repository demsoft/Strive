import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   CircularProgress,
   Container,
   TextField,
   Toolbar,
   Typography,
} from '@mui/material';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory, useLocation, useParams } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { PaymentStatus, PublicSession } from 'src/services/api/sessions';
import * as api from 'src/services/api/sessions';
import { requestErrorMessage } from 'src/features/creator/errors';
import { categoryGradient, categoryStyle } from 'src/features/gbedu/ui/categories';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { brand } from 'src/theme';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import { formatMoney, PASS_DAYS } from '../money';

const POLL_EVERY_MS = 2000;
const POLL_TIMES = 30;
const EMAIL_KEY = 'gbedu.email';

const rememberedEmail = () => {
   try {
      return window.localStorage.getItem(EMAIL_KEY) ?? '';
   } catch {
      return '';
   }
};

const remember = (email: string) => {
   try {
      window.localStorage.setItem(EMAIL_KEY, email);
   } catch {
      // storage can be blocked
   }
};

type Payment =
   | { state: 'checking' }
   | { state: 'paid' }
   | { state: 'failed'; reason?: string | null }
   | { state: 'waiting' };

/**
 * The page of a paid session, the one a creator shares. Everybody can read it; paying needs a sign in. Coming back from
 * the payment page (?reference=...) the page asks the server until the payment is confirmed, then shows the way in.
 */
export default function SessionPage() {
   const { t } = useTranslation();
   const auth = useAuth();
   const history = useHistory();
   const location = useLocation();
   const { sessionId } = useParams<{ sessionId: string }>();
   const accessToken = auth.user?.access_token;
   const reference = new URLSearchParams(location.search).get('reference');

   const [session, setSession] = useState<PublicSession | null>(null);
   const [notFound, setNotFound] = useState(false);
   const [loadError, setLoadError] = useState<string | null>(null);
   const [email, setEmail] = useState(() => (auth.user?.profile?.email as string | undefined) ?? rememberedEmail());
   const [paying, setPaying] = useState(false);
   const [payError, setPayError] = useState<string | null>(null);
   const [payment, setPayment] = useState<Payment | null>(null);
   const polling = useRef(0);

   const load = useCallback(async () => {
      try {
         setSession(await api.fetchSession(sessionId, accessToken));
         setLoadError(null);
      } catch (error) {
         if ((error as { response?: { status?: number } }).response?.status === 404) setNotFound(true);
         else setLoadError(requestErrorMessage(error));
      }
   }, [sessionId, accessToken]);

   useEffect(() => {
      // wait until it is known whether the person is signed in: the answer depends on it
      if (auth.isLoading) return;
      load();
   }, [auth.isLoading, load]);

   // back from the payment page: ask until the server knows
   useEffect(() => {
      if (!reference || !accessToken) return;

      const id = ++polling.current;
      setPayment({ state: 'checking' });
      (async () => {
         for (let i = 0; i < POLL_TIMES && id === polling.current; i++) {
            try {
               const status: PaymentStatus = await api.fetchPayment(reference, accessToken);
               if (status.status === 'paid') {
                  setPayment({ state: 'paid' });
                  await load();
                  return;
               }
               if (status.status === 'failed') {
                  setPayment({ state: 'failed', reason: status.failureReason });
                  return;
               }
            } catch {
               // the next try
            }
            await new Promise((resolve) => setTimeout(resolve, POLL_EVERY_MS));
         }
         if (id === polling.current) setPayment({ state: 'waiting' });
      })();

      return () => {
         polling.current++;
      };
   }, [reference, accessToken, load]);

   const handlePay = async () => {
      if (!session || !accessToken) return;
      setPaying(true);
      setPayError(null);
      try {
         const checkout = await api.startCheckout(session.sessionId, email.trim(), accessToken);
         remember(email.trim());
         if (checkout.alreadyHasTicket) {
            await load();
         } else if (checkout.authorizationUrl) {
            window.location.assign(checkout.authorizationUrl);
            return;
         }
      } catch (error) {
         setPayError(requestErrorMessage(error));
      }
      setPaying(false);
   };

   const handleSignIn = () => auth.signinRedirect({ state: { url: location.pathname } });
   const handleSignUp = () => auth.signinRedirect({ state: { url: location.pathname }, prompt: 'create' });
   const joinRoom = () => session?.conferenceId && history.push(`/c/${session.conferenceId}`);

   const CategoryIcon = categoryStyle(session?.category ?? 'music').Icon;
   const startsAt = session?.startsAt ? DateTime.fromISO(session.startsAt).toLocaleString(DateTime.DATETIME_MED) : null;

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="sm" sx={{ py: 4 }} id="session-page">
            {!session && !notFound && !loadError && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
                  <CircularProgress />
               </Box>
            )}
            {notFound && (
               <Alert severity="warning" id="session-not-found">
                  {t('sessions.not_found')}
               </Alert>
            )}
            {loadError && <Alert severity="error">{loadError}</Alert>}

            {session && (
               <>
                  <Box
                     className="session-hero"
                     sx={{
                        borderRadius: '24px',
                        overflow: 'hidden',
                        mb: 3,
                        backgroundColor: brand.surface,
                        border: `1px solid ${brand.border}`,
                     }}
                  >
                     <Box
                        sx={{
                           p: 3,
                           color: '#fff',
                           background: categoryGradient(session.category),
                           display: 'flex',
                           alignItems: 'center',
                           justifyContent: 'space-between',
                           gap: 2,
                        }}
                     >
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                           <CategoryIcon sx={{ fontSize: 40 }} />
                           <Typography sx={{ fontWeight: 800, letterSpacing: 0.3 }}>
                              {t(`sessions.category_${session.category}`)}
                           </Typography>
                        </Box>
                        <Chip
                           size="small"
                           label={formatMoney(session.priceMinor, session.currency)}
                           sx={{ bgcolor: 'rgba(0, 0, 0, 0.28)', color: '#fff', fontWeight: 800 }}
                        />
                     </Box>
                     <Box sx={{ p: 3 }}>
                        <Typography
                           variant="h4"
                           gutterBottom
                           id="session-title"
                           sx={{ fontSize: { xs: '1.7rem', sm: '2.1rem' } }}
                        >
                           {session.title}
                        </Typography>
                        {session.isLive && (
                           <Chip
                              size="small"
                              color="error"
                              label={t('sessions.live_now')}
                              id="session-live"
                              sx={{ mb: 1 }}
                           />
                        )}
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, my: 2 }} id="session-host">
                           <HostAvatar
                              name={session.creator.displayName}
                              size={44}
                              background={categoryGradient(session.category)}
                              photoUrl={creatorPhotoUrl(session.creator.creatorId, session.creator.photoVersion, 44)}
                           />
                           <Box sx={{ minWidth: 0 }}>
                              <Typography
                                 component={RouterLink}
                                 to={`/u/${session.creator.slug || session.creator.creatorId}`}
                                 id="session-host-link"
                                 sx={{
                                    fontWeight: 700,
                                    color: 'inherit',
                                    textDecoration: 'none',
                                    '&:hover': { textDecoration: 'underline' },
                                 }}
                              >
                                 {t('sessions.hosted_by', { name: session.creator.displayName })}
                              </Typography>
                              {session.creator.bio && (
                                 <Typography variant="body2" color="text.secondary">
                                    {session.creator.bio}
                                 </Typography>
                              )}
                           </Box>
                        </Box>
                        {startsAt && (
                           <Typography sx={{ fontWeight: 600 }} id="session-starts">
                              {startsAt}
                           </Typography>
                        )}
                        {session.description && (
                           <Typography color="text.secondary" sx={{ mt: 2, whiteSpace: 'pre-wrap' }}>
                              {session.description}
                           </Typography>
                        )}
                        <Box component="ul" sx={{ listStyle: 'none', p: 0, m: 0, mt: 2.5, display: 'grid', gap: 0.75 }}>
                           {['perk_live', 'perk_phone', 'perk_rejoin'].map((perk) => (
                              <Box
                                 component="li"
                                 key={perk}
                                 sx={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 1,
                                    color: 'text.secondary',
                                    fontSize: 14,
                                 }}
                              >
                                 <CheckCircleOutlinedIcon sx={{ fontSize: 18, color: 'success.main' }} />
                                 {t(`sessions.${perk}`)}
                              </Box>
                           ))}
                        </Box>
                     </Box>
                  </Box>

                  {payment?.state === 'checking' && (
                     <Alert severity="info" icon={<CircularProgress size={18} />} id="payment-checking" sx={{ mb: 2 }}>
                        {t('sessions.payment_checking')}
                     </Alert>
                  )}
                  {payment?.state === 'waiting' && (
                     <Alert
                        severity="info"
                        id="payment-waiting"
                        sx={{ mb: 2 }}
                        action={
                           <Button color="inherit" size="small" onClick={() => history.go(0)}>
                              {t('sessions.check_again')}
                           </Button>
                        }
                     >
                        {t('sessions.payment_waiting')}
                     </Alert>
                  )}
                  {payment?.state === 'failed' && (
                     <Alert severity="error" id="payment-failed" sx={{ mb: 2 }}>
                        {t('sessions.payment_failed')}
                     </Alert>
                  )}

                  {session.status === 'cancelled' ? (
                     <Alert severity="warning" id="session-cancelled">
                        {t('sessions.cancelled')}
                     </Alert>
                  ) : session.isHost ? (
                     <Button id="session-open-room" variant="contained" size="large" fullWidth onClick={joinRoom}>
                        {t('sessions.open_room')}
                     </Button>
                  ) : session.hasTicket ? (
                     <>
                        <Alert severity="success" id="session-has-ticket" sx={{ mb: 2 }}>
                           {t('sessions.has_ticket')}
                        </Alert>
                        <Button id="session-join" variant="contained" size="large" fullWidth onClick={joinRoom}>
                           {t('sessions.join')}
                        </Button>
                     </>
                  ) : !accessToken ? (
                     <Box>
                        <Button id="session-sign-in" variant="contained" size="large" fullWidth onClick={handleSignIn}>
                           {t('sessions.sign_in_to_buy', { price: formatMoney(session.priceMinor, session.currency) })}
                        </Button>
                        <Button id="session-sign-up" size="large" fullWidth onClick={handleSignUp} sx={{ mt: 1 }}>
                           {t('sessions.sign_up_to_buy')}
                        </Button>
                     </Box>
                  ) : (
                     <Box id="session-buy">
                        {session.creator.passPriceMinor != null && (
                           <Alert severity="info" icon={false} id="session-pass-hint" sx={{ mb: 2 }}>
                              <RouterLink
                                 to={`/u/${session.creator.slug || session.creator.creatorId}`}
                                 style={{ color: 'inherit' }}
                              >
                                 {t('creator.pass_or', {
                                    name: session.creator.displayName,
                                    days: PASS_DAYS,
                                    price: formatMoney(session.creator.passPriceMinor, session.currency),
                                 })}
                              </RouterLink>
                           </Alert>
                        )}
                        <TextField
                           id="session-email"
                           type="email"
                           fullWidth
                           label={t('sessions.email')}
                           helperText={t('sessions.email_hint')}
                           value={email}
                           onChange={(e) => setEmail(e.target.value)}
                           sx={{ mb: 2 }}
                        />
                        {payError && (
                           <Alert severity="error" sx={{ mb: 2 }} id="session-pay-error">
                              {payError}
                           </Alert>
                        )}
                        <Button
                           id="session-pay"
                           variant="contained"
                           size="large"
                           fullWidth
                           disabled={paying || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email.trim())}
                           onClick={handlePay}
                        >
                           {paying ? (
                              <CircularProgress size={22} color="inherit" />
                           ) : (
                              t('sessions.pay', { price: formatMoney(session.priceMinor, session.currency) })
                           )}
                        </Button>
                        <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }}>
                           {t('sessions.pay_hint')}
                        </Typography>
                     </Box>
                  )}
               </>
            )}
         </Container>
      </Box>
   );
}
