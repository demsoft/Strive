import { Alert, Box, Button, Checkbox, CircularProgress, FormControlLabel, TextField, Typography } from '@mui/material';
import ConfirmationNumberOutlinedIcon from '@mui/icons-material/ConfirmationNumberOutlined';
import { DateTime } from 'luxon';
import React, { useEffect, useRef, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router-dom';
import { formatMoney, PASS_DAYS } from 'src/features/sessions/money';
import * as api from 'src/services/api/sessions';
import { CreatorPage } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import { requestErrorMessage } from '../errors';

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

type Payment = 'checking' | 'paid' | 'failed' | 'waiting' | null;

type Props = {
   page: CreatorPage;
   /** called when the pass was paid, to load the page again */
   onPaid: () => void;
};

/**
 * What a creator offers as a pass, and the way to buy it: sign in, an email for the receipt, pay. Coming back from the
 * payment page (?reference=...) it asks the server until the payment is confirmed.
 */
export default function PassCard({ page, onPaid }: Props) {
   const { t } = useTranslation();
   const auth = useAuth();
   const location = useLocation();
   const accessToken = auth.user?.access_token;
   const reference = new URLSearchParams(location.search).get('reference');
   const { creator } = page;

   const [email, setEmail] = useState(() => (auth.user?.profile?.email as string | undefined) ?? rememberedEmail());
   const [autoRenew, setAutoRenew] = useState(false);
   const [paying, setPaying] = useState(false);
   const [payError, setPayError] = useState<string | null>(null);
   const [payment, setPayment] = useState<Payment>(null);
   const polling = useRef(0);

   useEffect(() => {
      if (!reference || !accessToken) return;

      const id = ++polling.current;
      setPayment('checking');
      (async () => {
         for (let i = 0; i < POLL_TIMES && id === polling.current; i++) {
            try {
               const status = await api.fetchPayment(reference, accessToken);
               if (status.status === 'paid') {
                  setPayment('paid');
                  onPaid();
                  return;
               }
               if (status.status === 'failed') {
                  setPayment('failed');
                  return;
               }
            } catch {
               // the next try
            }
            await new Promise((resolve) => setTimeout(resolve, POLL_EVERY_MS));
         }
         if (id === polling.current) setPayment('waiting');
      })();

      return () => {
         polling.current++;
      };
   }, [reference, accessToken]);

   if (creator.passPriceMinor == null) return null;
   const price = formatMoney(creator.passPriceMinor, creator.currency ?? 'NGN');
   const until = page.passValidUntil ? DateTime.fromISO(page.passValidUntil).toLocaleString(DateTime.DATE_MED) : '';

   const handlePay = async () => {
      if (!accessToken) return;
      setPaying(true);
      setPayError(null);
      try {
         const checkout = await api.startPassCheckout(creator.creatorId, email.trim(), accessToken, autoRenew);
         remember(email.trim());
         if (checkout.authorizationUrl) {
            window.location.assign(checkout.authorizationUrl);
            return;
         }
      } catch (error) {
         setPayError(requestErrorMessage(error));
      }
      setPaying(false);
   };

   const signIn = () => auth.signinRedirect({ state: { url: location.pathname } });
   const signUp = () => auth.signinRedirect({ state: { url: location.pathname }, prompt: 'create' });

   return (
      <Box
         id="pass-card"
         sx={{
            mb: 4,
            p: 3,
            borderRadius: '20px',
            border: `1px solid ${brand.border}`,
            background: 'linear-gradient(135deg, rgba(255,159,28,0.14), rgba(255,77,109,0.14))',
         }}
      >
         <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1 }}>
            <ConfirmationNumberOutlinedIcon color="primary" />
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
               {t('creator.pass_title')} · {price}
            </Typography>
         </Box>
         <Typography color="text.secondary" sx={{ mb: 2 }}>
            {t('creator.pass_blurb', { name: creator.displayName, days: PASS_DAYS })}
         </Typography>

         {payment === 'checking' && (
            <Alert severity="info" icon={<CircularProgress size={18} />} id="pass-checking" sx={{ mb: 2 }}>
               {t('sessions.payment_checking')}
            </Alert>
         )}
         {payment === 'paid' && (
            <Alert severity="success" id="pass-paid" sx={{ mb: 2 }}>
               {t('creator.pass_paid')}
            </Alert>
         )}
         {payment === 'waiting' && (
            <Alert severity="info" id="pass-waiting" sx={{ mb: 2 }}>
               {t('sessions.payment_waiting')}
            </Alert>
         )}
         {payment === 'failed' && (
            <Alert severity="error" id="pass-failed" sx={{ mb: 2 }}>
               {t('creator.pass_failed')}
            </Alert>
         )}

         {page.hasPass && (
            <Alert severity="success" id="pass-active" sx={{ mb: 2 }}>
               {t('creator.pass_active', { date: until })}
               {page.passRenewsAt && (
                  <span id="pass-renews">
                     {' '}
                     {t('creator.pass_renews_on', {
                        date: DateTime.fromISO(page.passRenewsAt).toLocaleString(DateTime.DATE_MED),
                     })}
                  </span>
               )}
            </Alert>
         )}

         {!accessToken ? (
            <Box>
               <Button id="pass-sign-in" variant="contained" size="large" fullWidth onClick={signIn}>
                  {t('creator.pass_buy', { days: PASS_DAYS, price })}
               </Button>
               <Button id="pass-sign-up" size="large" fullWidth onClick={signUp} sx={{ mt: 1 }}>
                  {t('sessions.sign_up_to_buy')}
               </Button>
            </Box>
         ) : (
            <Box id="pass-buy">
               <TextField
                  id="pass-email"
                  type="email"
                  fullWidth
                  label={t('sessions.email')}
                  helperText={t('sessions.email_hint')}
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  sx={{ mb: 2 }}
               />
               {page.autoRenewAvailable && !page.passRenewsAt && (
                  <FormControlLabel
                     sx={{ mb: 2, alignItems: 'flex-start' }}
                     control={
                        <Checkbox
                           id="pass-auto-renew"
                           checked={autoRenew}
                           onChange={(e) => setAutoRenew(e.target.checked)}
                           sx={{ pt: 0.5 }}
                        />
                     }
                     label={
                        <span>
                           <b>{t('creator.pass_auto_renew', { days: PASS_DAYS })}</b>
                           <Typography
                              variant="body2"
                              color="text.secondary"
                              component="span"
                              sx={{ display: 'block' }}
                           >
                              {t('creator.pass_auto_renew_hint')}
                           </Typography>
                        </span>
                     }
                  />
               )}
               {payError && (
                  <Alert severity="error" sx={{ mb: 2 }} id="pass-pay-error">
                     {payError}
                  </Alert>
               )}
               <Button
                  id="pass-pay"
                  variant="contained"
                  size="large"
                  fullWidth
                  disabled={paying || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email.trim())}
                  onClick={handlePay}
               >
                  {paying ? (
                     <CircularProgress size={22} color="inherit" />
                  ) : page.hasPass ? (
                     t('creator.pass_extend', { days: PASS_DAYS, price })
                  ) : (
                     t('creator.pass_buy', { days: PASS_DAYS, price })
                  )}
               </Button>
            </Box>
         )}
      </Box>
   );
}
