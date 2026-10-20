import { Alert, Box, Button, CircularProgress, TextField, Typography } from '@mui/material';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatMoney, PASS_DAYS, parsePriceToMinor } from 'src/features/sessions/money';
import * as api from 'src/services/api/creators';
import { CreatorProfile } from 'src/services/api/creators';
import { requestErrorMessage } from '../errors';

type Props = {
   profile: CreatorProfile;
   onSaved: (profile: CreatorProfile) => void;
};

/** Where a creator sets what the pass costs (or stops selling it). */
export default function PassSetup({ profile, onSaved }: Props) {
   const { t } = useTranslation();
   const [price, setPrice] = useState(() => (profile.passPriceMinor ? String(profile.passPriceMinor / 100) : ''));
   const [saving, setSaving] = useState(false);
   const [error, setError] = useState<string | null>(null);
   const [saved, setSaved] = useState(false);

   const minor = parsePriceToMinor(price);
   const selling = profile.passPriceMinor != null;

   const save = async (value: number | null) => {
      setSaving(true);
      setError(null);
      setSaved(false);
      try {
         const updated = await api.setPassPrice(value);
         onSaved(updated);
         if (value === null) setPrice('');
         setSaved(true);
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setSaving(false);
   };

   return (
      <Box id="pass-setup" sx={{ mt: 4 }}>
         <Typography variant="h6" sx={{ fontWeight: 700 }}>
            {t('creator.pass_setup_title')}
            {selling && ` · ${formatMoney(profile.passPriceMinor!, profile.currency)}`}
         </Typography>
         <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {t('creator.pass_setup_text', { days: PASS_DAYS })}
         </Typography>
         <TextField
            id="pass-price"
            fullWidth
            label={t('creator.pass_price')}
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            helperText={price && minor === null ? t('sessions.price_range') : undefined}
            error={Boolean(price) && minor === null}
            slotProps={{ htmlInput: { inputMode: 'decimal' } }}
         />
         {error && (
            <Alert severity="error" sx={{ mt: 2 }} id="pass-setup-error">
               {error}
            </Alert>
         )}
         {saved && !error && (
            <Alert severity="success" sx={{ mt: 2 }} id="pass-setup-saved">
               {t('creator.pass_saved')}
            </Alert>
         )}
         <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
            <Button id="pass-save" variant="outlined" disabled={saving || minor === null} onClick={() => save(minor)}>
               {saving ? <CircularProgress size={20} color="inherit" /> : t('creator.pass_save')}
            </Button>
            {selling && (
               <Button id="pass-stop" color="inherit" disabled={saving} onClick={() => save(null)}>
                  {t('creator.pass_stop')}
               </Button>
            )}
         </Box>
      </Box>
   );
}
