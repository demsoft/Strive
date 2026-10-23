import { Alert, Box, Button, CircularProgress, InputAdornment, TextField, Typography } from '@mui/material';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import * as api from 'src/services/api/creators';
import { CreatorProfile } from 'src/services/api/creators';
import { requestErrorMessage } from '../errors';

/** What the server accepts: lower case letters, numbers and single hyphens, 3 to 30 characters. */
export const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
export const isPlausibleSlug = (slug: string) => slug.length >= 3 && slug.length <= 30 && SLUG_PATTERN.test(slug);

type Props = {
   profile: CreatorProfile;
   onSaved: (profile: CreatorProfile) => void;
};

/** Where a creator chooses the short name in the link of the public page (gbedu.example.com/u/ada). */
export default function SlugSetup({ profile, onSaved }: Props) {
   const { t } = useTranslation();
   const [slug, setSlug] = useState(profile.slug ?? '');
   const [saving, setSaving] = useState(false);
   const [error, setError] = useState<string | null>(null);
   const [saved, setSaved] = useState(false);

   // what the server does with capitals and spaces around: forgive them
   const clean = slug.trim().toLowerCase();
   const changed = clean !== (profile.slug ?? '');
   const valid = clean === '' || isPlausibleSlug(clean);
   const host = window.location.host;

   const save = async (value: string | null) => {
      setSaving(true);
      setError(null);
      setSaved(false);
      try {
         const updated = await api.setSlug(value);
         onSaved(updated);
         setSlug(updated.slug ?? '');
         setSaved(true);
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setSaving(false);
   };

   return (
      <Box id="slug-setup" sx={{ mt: 4 }}>
         <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {t('creator.slug_title')}
         </Typography>
         <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {t('creator.slug_text')}
         </Typography>
         <TextField
            id="slug-input"
            fullWidth
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            error={!valid}
            helperText={
               !valid ? t('creator.slug_hint') : clean ? `${host}/u/${clean}` : `${host}/u/${profile.creatorId}`
            }
            slotProps={{
               input: {
                  startAdornment: <InputAdornment position="start">/u/</InputAdornment>,
               },
               htmlInput: { autoCapitalize: 'none', autoCorrect: 'off', spellCheck: false },
            }}
         />
         {error && (
            <Alert severity="error" sx={{ mt: 2 }} id="slug-error">
               {error}
            </Alert>
         )}
         {saved && !error && (
            <Alert severity="success" sx={{ mt: 2 }} id="slug-saved">
               {t('creator.slug_saved')}
            </Alert>
         )}
         {profile.slug && changed && clean !== '' && (
            <Alert severity="warning" icon={false} sx={{ mt: 2 }} id="slug-change-warning">
               {t('creator.slug_change_warning')}
            </Alert>
         )}
         <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
            <Button
               id="slug-save"
               variant="outlined"
               disabled={saving || !changed || !valid || clean === ''}
               onClick={() => save(clean)}
            >
               {saving ? <CircularProgress size={20} color="inherit" /> : t('creator.slug_save')}
            </Button>
            {profile.slug && (
               <Button id="slug-clear" color="inherit" disabled={saving} onClick={() => save(null)}>
                  {t('creator.slug_clear')}
               </Button>
            )}
         </Box>
      </Box>
   );
}
