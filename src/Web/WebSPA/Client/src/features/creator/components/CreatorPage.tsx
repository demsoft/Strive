import {
   Alert,
   AppBar,
   Autocomplete,
   Box,
   Button,
   CircularProgress,
   Container,
   TextField,
   Toolbar,
   Typography,
} from '@mui/material';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import * as api from 'src/services/api/creators';
import { Bank, CreatorProfile, PaymentsConfig } from 'src/services/api/creators';
import { cleanAccountNumber, isCompleteAccountNumber } from '../accountNumber';
import { requestErrorMessage } from '../errors';

type Resolution =
   | { state: 'idle' }
   | { state: 'checking' }
   | { state: 'found'; name: string }
   | { state: 'failed'; message: string };

/**
 * The page where a person becomes a creator: a name, a short text and the bank account the earnings are paid into.
 * The name of the account holder is shown before saving, so that a wrong number is noticed at once.
 */
export default function CreatorPage() {
   const { t } = useTranslation();
   const [config, setConfig] = useState<PaymentsConfig | null>(null);
   const [banks, setBanks] = useState<Bank[]>([]);
   const [profile, setProfile] = useState<CreatorProfile | null | undefined>(undefined);
   const [loadError, setLoadError] = useState<string | null>(null);

   const [displayName, setDisplayName] = useState('');
   const [bio, setBio] = useState('');
   const [bank, setBank] = useState<Bank | null>(null);
   const [accountNumber, setAccountNumber] = useState('');
   const [changeAccount, setChangeAccount] = useState(false);
   const [resolution, setResolution] = useState<Resolution>({ state: 'idle' });
   const [saving, setSaving] = useState(false);
   const [saveError, setSaveError] = useState<string | null>(null);
   const [saved, setSaved] = useState(false);
   const resolveSeq = useRef(0);

   useEffect(() => {
      (async () => {
         try {
            const loadedConfig = await api.fetchPaymentsConfig();
            setConfig(loadedConfig);
            if (!loadedConfig.enabled) return;

            const [loadedBanks, mine] = await Promise.all([api.fetchBanks(), api.fetchMyCreatorProfile()]);
            setBanks(loadedBanks);
            setProfile(mine);
            if (mine) {
               setDisplayName(mine.displayName);
               setBio(mine.bio ?? '');
            }
         } catch (error) {
            setLoadError(requestErrorMessage(error));
         }
      })();
   }, []);

   // look the account up as soon as the bank is chosen and the number is complete
   useEffect(() => {
      if (!bank || !isCompleteAccountNumber(accountNumber)) {
         setResolution({ state: 'idle' });
         return;
      }

      const seq = ++resolveSeq.current;
      setResolution({ state: 'checking' });
      api.resolveAccount(bank.code, accountNumber).then(
         (name) => seq === resolveSeq.current && setResolution({ state: 'found', name }),
         (error) =>
            seq === resolveSeq.current && setResolution({ state: 'failed', message: requestErrorMessage(error) }),
      );
   }, [bank, accountNumber]);

   const needsAccount = !profile || changeAccount;
   const nameOk = displayName.trim().length >= 2;
   const accountOk = !needsAccount || resolution.state === 'found';
   const canSave = nameOk && accountOk && !saving;

   const handleSave = async () => {
      setSaving(true);
      setSaveError(null);
      setSaved(false);
      try {
         const result = await api.saveMyCreatorProfile({
            displayName: displayName.trim(),
            bio: bio.trim() || undefined,
            ...(needsAccount && bank ? { bankCode: bank.code, accountNumber } : {}),
         });
         setProfile(result);
         setChangeAccount(false);
         setAccountNumber('');
         setBank(null);
         setSaved(true);
      } catch (error) {
         setSaveError(requestErrorMessage(error));
      } finally {
         setSaving(false);
      }
   };

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="sm" sx={{ py: 4 }} id="creator-page">
            <Typography variant="h4" gutterBottom>
               {t('creator.title')}
            </Typography>

            {loadError && <Alert severity="error">{loadError}</Alert>}

            {!loadError && !config && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
                  <CircularProgress />
               </Box>
            )}

            {config && !config.enabled && (
               <Alert severity="info" id="creator-not-enabled">
                  {t('creator.not_enabled')}
               </Alert>
            )}

            {config?.enabled && profile !== undefined && (
               <>
                  <Typography color="text.secondary" sx={{ mb: 3 }}>
                     {profile
                        ? t('creator.intro_existing')
                        : t('creator.intro_new', { percent: config.platformCommissionPercent })}
                  </Typography>

                  <TextField
                     id="creator-name"
                     fullWidth
                     label={t('creator.name')}
                     helperText={t('creator.name_hint')}
                     value={displayName}
                     onChange={(e) => setDisplayName(e.target.value)}
                     slotProps={{ htmlInput: { maxLength: 60 } }}
                     sx={{ mb: 2 }}
                  />
                  <TextField
                     id="creator-bio"
                     fullWidth
                     multiline
                     minRows={3}
                     label={t('creator.bio')}
                     helperText={t('creator.bio_hint')}
                     value={bio}
                     onChange={(e) => setBio(e.target.value)}
                     slotProps={{ htmlInput: { maxLength: 500 } }}
                     sx={{ mb: 3 }}
                  />

                  <Typography variant="h6" gutterBottom>
                     {t('creator.payout_title')}
                  </Typography>

                  {profile?.canSell && !changeAccount ? (
                     <Alert
                        id="creator-account-saved"
                        icon={<CheckCircleOutlinedIcon />}
                        severity="success"
                        action={
                           <Button
                              color="inherit"
                              size="small"
                              id="creator-change-account"
                              onClick={() => setChangeAccount(true)}
                           >
                              {t('creator.change_account')}
                           </Button>
                        }
                     >
                        {t('creator.account_saved', {
                           name: profile.accountName,
                           bank: profile.bankName,
                           last4: profile.accountLast4,
                        })}
                     </Alert>
                  ) : (
                     <Box id="creator-account-form">
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                           {t('creator.payout_hint')}
                        </Typography>
                        <Autocomplete
                           id="creator-bank"
                           options={banks}
                           value={bank}
                           onChange={(_, value) => setBank(value)}
                           getOptionLabel={(option) => option.name}
                           isOptionEqualToValue={(a, b) => a.code === b.code}
                           renderInput={(params) => <TextField {...params} label={t('creator.bank')} />}
                           sx={{ mb: 2 }}
                        />
                        <TextField
                           id="creator-account-number"
                           fullWidth
                           label={t('creator.account_number')}
                           value={accountNumber}
                           onChange={(e) => setAccountNumber(cleanAccountNumber(e.target.value))}
                           slotProps={{ htmlInput: { inputMode: 'numeric', autoComplete: 'off' } }}
                           sx={{ mb: 1 }}
                        />
                        <Box sx={{ minHeight: 48, display: 'flex', alignItems: 'center' }} aria-live="polite">
                           {resolution.state === 'checking' && <CircularProgress size={20} />}
                           {resolution.state === 'found' && (
                              <Alert severity="success" id="creator-account-name" sx={{ width: '100%' }}>
                                 {t('creator.account_holder', { name: resolution.name })}
                              </Alert>
                           )}
                           {resolution.state === 'failed' && (
                              <Alert severity="error" id="creator-account-error" sx={{ width: '100%' }}>
                                 {resolution.message}
                              </Alert>
                           )}
                        </Box>
                        {changeAccount && (
                           <Button size="small" onClick={() => setChangeAccount(false)} sx={{ mt: 1 }}>
                              {t('common:cancel')}
                           </Button>
                        )}
                     </Box>
                  )}

                  {saveError && (
                     <Alert severity="error" sx={{ mt: 2 }} id="creator-save-error">
                        {saveError}
                     </Alert>
                  )}
                  {saved && (
                     <Alert severity="success" sx={{ mt: 2 }} id="creator-saved">
                        {t('creator.saved')}
                     </Alert>
                  )}

                  {profile?.canSell && (
                     <Button
                        id="creator-page-link"
                        component={RouterLink}
                        to={`/u/${profile.creatorId}`}
                        variant="outlined"
                        size="large"
                        fullWidth
                        sx={{ mt: 3 }}
                     >
                        {t('creator.view_my_page')}
                     </Button>
                  )}

                  {profile?.canSell && (
                     <Button
                        id="creator-sessions-link"
                        component={RouterLink}
                        to="/creator/sessions"
                        variant="outlined"
                        size="large"
                        fullWidth
                        sx={{ mt: 3 }}
                     >
                        {t('sessions.my_title')}
                     </Button>
                  )}

                  <Button
                     id="creator-save"
                     variant="contained"
                     size="large"
                     fullWidth
                     disabled={!canSave}
                     onClick={handleSave}
                     sx={{ mt: 3 }}
                  >
                     {saving ? (
                        <CircularProgress size={22} color="inherit" />
                     ) : profile ? (
                        t('creator.save')
                     ) : (
                        t('creator.create')
                     )}
                  </Button>
               </>
            )}
         </Container>
      </Box>
   );
}
