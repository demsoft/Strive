import { Alert, Box, Button, CircularProgress, TextField, Typography } from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { requestErrorMessage } from 'src/features/creator/errors';
import { isPlausibleEmail, rememberedEmail, rememberEmail } from 'src/features/payments/rememberedEmail';
import { PaymentReturn } from 'src/features/payments/usePaymentReturn';
import { formatMoney, PASS_DAYS } from 'src/features/sessions/money';
import * as api from 'src/services/api/videos';
import { Video } from 'src/services/api/videos';
import { brand } from 'src/theme';

type Props = {
   video: Video;
   /** the access token of the signed in person; none: not signed in */
   accessToken?: string;
   /** the email of the account, if the identity service gave one */
   knownEmail?: string;
   payment: PaymentReturn;
   onSignIn: () => void;
   onSignUp: () => void;
};

/**
 * What stands in place of the player for a video that costs money: the cover with a lock, the price, a way to buy
 * (sign in, an email for the receipt, pay) and the pass that would cover it as well.
 */
export default function VideoBuyBox({ video, accessToken, knownEmail, payment, onSignIn, onSignUp }: Props) {
   const { t } = useTranslation();
   const [email, setEmail] = useState(() => knownEmail ?? rememberedEmail());
   const [paying, setPaying] = useState(false);
   const [error, setError] = useState<string | null>(null);
   const price = formatMoney(video.priceMinor, 'NGN');
   const creator = video.creator;

   const pay = async () => {
      if (!accessToken) return;
      setPaying(true);
      setError(null);
      try {
         const checkout = await api.startVideoCheckout(video.videoId, email.trim(), accessToken);
         rememberEmail(email.trim());
         if (checkout.authorizationUrl) {
            window.location.assign(checkout.authorizationUrl);
            return;
         }
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setPaying(false);
   };

   return (
      <Box id="video-locked">
         <Box
            sx={{
               position: 'relative',
               aspectRatio: '9 / 12',
               maxHeight: '60vh',
               width: '100%',
               borderRadius: '16px',
               overflow: 'hidden',
               backgroundColor: '#000',
               backgroundImage: video.posterUrl ? `url(${video.posterUrl})` : brand.gradient,
               backgroundSize: 'cover',
               backgroundPosition: 'center',
            }}
         >
            <Box
               sx={{
                  position: 'absolute',
                  inset: 0,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  backgroundColor: 'rgba(0,0,0,0.55)',
               }}
            >
               <LockOutlinedIcon sx={{ fontSize: 64, color: '#fff' }} />
            </Box>
         </Box>

         <Box sx={{ mt: 2 }}>
            {payment === 'checking' && (
               <Alert severity="info" icon={<CircularProgress size={18} />} sx={{ mb: 2 }} id="video-payment-checking">
                  {t('sessions.payment_checking')}
               </Alert>
            )}
            {payment === 'waiting' && (
               <Alert severity="info" sx={{ mb: 2 }} id="video-payment-waiting">
                  {t('sessions.payment_waiting')}
               </Alert>
            )}
            {payment === 'failed' && (
               <Alert severity="error" sx={{ mb: 2 }} id="video-payment-failed">
                  {t('videos.payment_failed')}
               </Alert>
            )}

            {!accessToken ? (
               <>
                  <Button id="video-sign-in" variant="contained" size="large" fullWidth onClick={onSignIn}>
                     {t('videos.sign_in_to_buy', { price })}
                  </Button>
                  <Button id="video-sign-up" size="large" fullWidth onClick={onSignUp} sx={{ mt: 1 }}>
                     {t('sessions.sign_up_to_buy')}
                  </Button>
               </>
            ) : (
               <Box id="video-buy">
                  <TextField
                     id="video-email"
                     type="email"
                     fullWidth
                     label={t('sessions.email')}
                     helperText={t('sessions.email_hint')}
                     value={email}
                     onChange={(e) => setEmail(e.target.value)}
                     sx={{ mb: 2 }}
                  />
                  {error && (
                     <Alert severity="error" sx={{ mb: 2 }} id="video-pay-error">
                        {error}
                     </Alert>
                  )}
                  <Button
                     id="video-pay"
                     variant="contained"
                     size="large"
                     fullWidth
                     disabled={paying || !isPlausibleEmail(email)}
                     onClick={pay}
                  >
                     {paying ? <CircularProgress size={22} color="inherit" /> : t('videos.buy', { price })}
                  </Button>
                  <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 1 }}>
                     {t('videos.buy_hint')}
                  </Typography>
               </Box>
            )}

            {creator?.passPriceMinor != null && (
               <Alert severity="info" icon={false} sx={{ mt: 2 }} id="video-pass-hint">
                  <RouterLink to={`/u/${creator.slug || creator.creatorId}`} style={{ color: 'inherit' }}>
                     {t('videos.pass_hint', {
                        name: creator.displayName,
                        days: PASS_DAYS,
                        price: formatMoney(creator.passPriceMinor, creator.currency ?? 'NGN'),
                     })}
                  </RouterLink>
               </Alert>
            )}
         </Box>
      </Box>
   );
}
