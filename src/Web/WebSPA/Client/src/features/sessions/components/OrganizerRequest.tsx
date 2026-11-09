import { Alert, Box, Button, CircularProgress, TextField, Typography } from '@mui/material';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/creators';
import { CreatorProfile } from 'src/services/api/creators';

type Props = { profile: CreatorProfile; onChanged: (profile: CreatorProfile) => void };

/**
 * Arena: only organizers that an administrator approved publish matches. A creator tells who they speak for and what
 * they want to stream, and sees the answer here.
 */
export default function OrganizerRequest({ profile, onChanged }: Props) {
   const { t } = useTranslation();
   const [name, setName] = useState(profile.organizationName ?? '');
   const [about, setAbout] = useState('');
   const [sending, setSending] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const status = profile.organizerStatus ?? 'none';

   const send = async () => {
      setSending(true);
      setError(null);
      try {
         onChanged(await api.requestOrganizer(name.trim(), about.trim()));
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setSending(false);
   };

   if (status === 'requested')
      return (
         <Alert severity="info" id="organizer-requested">
            {t('matches.organizer_waiting', { name: profile.organizationName })}
         </Alert>
      );

   return (
      <Box id="organizer-request">
         {status === 'rejected' && (
            <Alert severity="warning" sx={{ mb: 2 }} id="organizer-rejected">
               {t('matches.organizer_rejected')}
               {profile.organizerDecisionNote ? ` ${profile.organizerDecisionNote}` : ''}
            </Alert>
         )}
         <Typography sx={{ fontWeight: 700 }}>{t('matches.organizer_title')}</Typography>
         <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {t('matches.organizer_text')}
         </Typography>
         <TextField
            id="organizer-name"
            fullWidth
            label={t('matches.organizer_name')}
            value={name}
            onChange={(e) => setName(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 80 } }}
            sx={{ mb: 2 }}
         />
         <TextField
            id="organizer-about"
            fullWidth
            multiline
            minRows={3}
            label={t('matches.organizer_about')}
            helperText={t('matches.organizer_about_hint')}
            value={about}
            onChange={(e) => setAbout(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 600 } }}
            sx={{ mb: 2 }}
         />
         {error && (
            <Alert severity="error" sx={{ mb: 2 }} id="organizer-error">
               {error}
            </Alert>
         )}
         <Button
            id="organizer-send"
            variant="contained"
            size="large"
            fullWidth
            disabled={name.trim().length < 2 || about.trim().length < 10 || sending}
            onClick={send}
         >
            {sending ? <CircularProgress size={22} color="inherit" /> : t('matches.organizer_send')}
         </Button>
      </Box>
   );
}
