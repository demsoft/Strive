import {
   Alert,
   Box,
   Button,
   CircularProgress,
   Dialog,
   DialogActions,
   DialogContent,
   DialogTitle,
   TextField,
   Typography,
} from '@mui/material';
import MailOutlinedIcon from '@mui/icons-material/MailOutlined';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { requestErrorMessage } from 'src/features/creator/errors';
import { isPlausibleEmail, rememberedEmail, rememberEmail } from 'src/features/payments/rememberedEmail';
import * as api from 'src/services/api/follows';
import { NoticeState } from 'src/services/api/follows';

type Props = {
   creatorId: string;
   creatorName: string;
   state: NoticeState;
   onChange: (state: NoticeState) => void;
   /** the email of the account, if the identity service gave one */
   knownEmail?: string;
};

/** What a follower sets about mail from a creator: ask for it (the address is confirmed by mail), see it, stop it. */
export default function NoticeSettings({ creatorId, creatorName, state, onChange, knownEmail }: Props) {
   const { t } = useTranslation();
   const [open, setOpen] = useState(false);
   const [email, setEmail] = useState(() => knownEmail ?? rememberedEmail());
   const [sending, setSending] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const send = async () => {
      const address = email.trim();
      if (!isPlausibleEmail(address)) return setError(t('social.notice_invalid'));

      setSending(true);
      setError(null);
      try {
         await api.startEmailNotices(creatorId, address);
         rememberEmail(address);
         onChange('pending');
         setOpen(false);
      } catch (e) {
         setError(requestErrorMessage(e) || t('social.notice_failed'));
      }
      setSending(false);
   };

   const stop = async () => {
      try {
         await api.stopEmailNotices(creatorId);
         onChange('off');
      } catch {
         // it stays as it was; pressing again tries again
      }
   };

   return (
      <Box id="notice-settings" sx={{ mb: 3 }}>
         {state === 'off' && (
            <Button id="notice-ask" startIcon={<MailOutlinedIcon />} variant="outlined" onClick={() => setOpen(true)}>
               {t('social.notice_ask', { name: creatorName })}
            </Button>
         )}
         {state === 'pending' && (
            <Alert
               id="notice-pending"
               severity="info"
               action={
                  <Button color="inherit" size="small" onClick={() => setOpen(true)}>
                     {t('social.notice_other_address')}
                  </Button>
               }
            >
               {t('social.notice_pending')}
            </Alert>
         )}
         {state === 'on' && (
            <Box id="notice-on" sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
               <Typography color="text.secondary">{t('social.notice_on', { name: creatorName })}</Typography>
               <Button id="notice-stop" size="small" color="inherit" onClick={stop}>
                  {t('social.notice_stop')}
               </Button>
            </Box>
         )}

         <Dialog open={open} onClose={() => setOpen(false)} maxWidth="xs" fullWidth>
            <DialogTitle>{t('social.notice_title', { name: creatorName })}</DialogTitle>
            <DialogContent>
               <Typography color="text.secondary" sx={{ mb: 2 }}>
                  {t('social.notice_text', { name: creatorName })}
               </Typography>
               <TextField
                  id="notice-email"
                  fullWidth
                  type="email"
                  label={t('social.notice_email')}
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
               />
               {error && (
                  <Alert severity="error" id="notice-error" sx={{ mt: 2 }}>
                     {error}
                  </Alert>
               )}
            </DialogContent>
            <DialogActions>
               <Button onClick={() => setOpen(false)}>{t('social.notice_cancel')}</Button>
               <Button id="notice-send" variant="contained" disabled={sending} onClick={send}>
                  {sending ? <CircularProgress size={20} color="inherit" /> : t('social.notice_send')}
               </Button>
            </DialogActions>
         </Dialog>
      </Box>
   );
}
