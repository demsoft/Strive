import { Alert, Box, Button, CircularProgress, Typography } from '@mui/material';
import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import * as api from 'src/services/api/creators';
import { CreatorProfile } from 'src/services/api/creators';
import { brand } from 'src/theme';
import { requestErrorMessage } from '../errors';
import { creatorPhotoUrl } from '../photoUrl';
import { isAcceptedPhoto, shrinkToSquareJpeg } from '../resizePhoto';

type Props = {
   profile: CreatorProfile;
   onSaved: (profile: CreatorProfile) => void;
};

/** Where a creator chooses a photo. It is cut square and shrunk in the browser before it is sent. */
export default function PhotoSetup({ profile, onSaved }: Props) {
   const { t } = useTranslation();
   const input = useRef<HTMLInputElement>(null);
   const [busy, setBusy] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const choose = async (event: React.ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      event.target.value = ''; // the same file can be chosen again
      if (!file) return;

      setError(null);
      if (!isAcceptedPhoto(file)) {
         setError(t('photo.wrong_file'));
         return;
      }

      setBusy(true);
      try {
         onSaved(await api.uploadPhoto(await shrinkToSquareJpeg(file)));
      } catch (e) {
         // a picture that cannot be read is the likely case where the browser (not the server) fails
         setError(
            e instanceof Error && ['canvas', 'toBlob'].includes(e.message)
               ? t('photo.unreadable')
               : requestErrorMessage(e),
         );
      }
      setBusy(false);
   };

   const remove = async () => {
      setBusy(true);
      setError(null);
      try {
         onSaved(await api.removePhoto());
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setBusy(false);
   };

   return (
      <Box id="photo-setup" sx={{ mt: 4 }}>
         <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {t('photo.title')}
         </Typography>
         <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {t('photo.text')}
         </Typography>
         <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
            <HostAvatar
               name={profile.displayName}
               size={72}
               background={brand.gradient}
               photoUrl={creatorPhotoUrl(profile.creatorId, profile.photoVersion, 72)}
            />
            <input
               ref={input}
               id="photo-input"
               type="file"
               accept="image/jpeg,image/png,image/webp"
               hidden
               onChange={choose}
            />
            <Button id="photo-choose" variant="outlined" disabled={busy} onClick={() => input.current?.click()}>
               {busy ? (
                  <CircularProgress size={20} color="inherit" />
               ) : profile.photoVersion ? (
                  t('photo.change')
               ) : (
                  t('photo.choose')
               )}
            </Button>
            {profile.photoVersion && (
               <Button id="photo-remove" color="inherit" disabled={busy} onClick={remove}>
                  {t('photo.remove')}
               </Button>
            )}
         </Box>
         {error && (
            <Alert severity="error" sx={{ mt: 2 }} id="photo-error">
               {error}
            </Alert>
         )}
      </Box>
   );
}
