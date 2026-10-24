import {
   Alert,
   Autocomplete,
   Box,
   Button,
   CircularProgress,
   Container,
   Paper,
   TextField,
   Typography,
} from '@mui/material';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import LiveTvOutlinedIcon from '@mui/icons-material/LiveTvOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import PeopleOutlinedIcon from '@mui/icons-material/PeopleOutlined';
import PublicOutlinedIcon from '@mui/icons-material/PublicOutlined';
import SlideshowOutlinedIcon from '@mui/icons-material/SlideshowOutlined';
import VideoLibraryOutlinedIcon from '@mui/icons-material/VideoLibraryOutlined';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { compactCount } from 'src/features/creators/count';
import SiteHeader from 'src/features/creators/SiteHeader';
import { formatMoney } from 'src/features/sessions/money';
import * as api from 'src/services/api/creators';
import { Bank, CreatorProfile, PaymentsConfig } from 'src/services/api/creators';
import { brand } from 'src/theme';
import ActionCard from '../dashboard/ActionCard';
import ProfileHero from '../dashboard/ProfileHero';
import SetupChecklist, { SetupStep } from '../dashboard/SetupChecklist';
import StatCard from '../dashboard/StatCard';
import useDashboardData from '../dashboard/useDashboardData';
import PassSetup from './PassSetup';
import PhotoSetup from './PhotoSetup';
import SlugSetup from './SlugSetup';
import { cleanAccountNumber, isCompleteAccountNumber } from '../accountNumber';
import { requestErrorMessage } from '../errors';

type Resolution =
   | { state: 'idle' }
   | { state: 'checking' }
   | { state: 'found'; name: string }
   | { state: 'failed'; message: string };

/**
 * The page of a creator. A new one becomes a creator here: a name, a short text and the bank account the earnings are
 * paid into (the name of the account holder is shown before saving, so that a wrong number is noticed at once). After
 * that it is the dashboard: who they are for the fans, what is left to set up, the numbers, where to go, and the settings.
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
   const data = useDashboardData(profile?.creatorId, !!profile?.canSell);

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

   const scrollTo = (id: string) =>
      document.getElementById(id)?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });

   /** the name, the text and the bank account: the same form for a new creator and for the settings */
   const form = (
      <>
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

         <Typography variant="h6" sx={{ fontWeight: 700 }} gutterBottom>
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

         <Button
            id="creator-save"
            variant="contained"
            size="large"
            fullWidth
            disabled={!canSave}
            onClick={handleSave}
            sx={{ mt: 3, ...(canSave && { background: brand.gradient }) }}
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
   );

   const card = { p: { xs: 2.5, md: 3 }, borderRadius: '24px', border: `1px solid ${brand.border}` } as const;
   // the setup parts bring their own space above them: inside a card there is none
   const inCard = { '& > [id$="-setup"]': { mt: 0 } } as const;

   // what is left to do (the things about videos and sessions only once they are counted)
   const steps: SetupStep[] = profile
      ? [
           {
              id: 'bank',
              title: t('dashboard.step_bank'),
              text: t('dashboard.step_bank_text'),
              done: profile.canSell,
              scrollTo: 'creator-payout',
              action: t('dashboard.step_do'),
           },
           {
              id: 'photo',
              title: t('dashboard.step_photo'),
              text: t('dashboard.step_photo_text'),
              done: !!profile.photoVersion,
              scrollTo: 'photo-setup',
              action: t('dashboard.step_do'),
           },
           ...(data.videos
              ? [
                   {
                      id: 'video',
                      title: t('dashboard.step_video'),
                      text: t('dashboard.step_video_text'),
                      done: data.videos.total > 0,
                      to: '/creator/videos',
                      action: t('dashboard.step_upload'),
                   },
                ]
              : []),
           ...(profile.canSell && data.sessions
              ? [
                   {
                      id: 'session',
                      title: t('dashboard.step_session'),
                      text: t('dashboard.step_session_text'),
                      done: data.sessions.total > 0,
                      to: '/creator/sessions',
                      action: t('dashboard.step_publish'),
                   },
                ]
              : []),
           ...(profile.canSell
              ? [
                   {
                      id: 'slug',
                      title: t('dashboard.step_slug'),
                      text: t('dashboard.step_slug_text'),
                      done: !!profile.slug,
                      scrollTo: 'slug-setup',
                      action: t('dashboard.step_do'),
                   },
                   {
                      id: 'pass',
                      title: t('dashboard.step_pass'),
                      text: t('dashboard.step_pass_text'),
                      done: profile.passPriceMinor != null,
                      scrollTo: 'pass-setup',
                      action: t('dashboard.step_do'),
                      optional: true,
                   },
                ]
              : []),
        ]
      : [];

   const loading = data.loading;
   const lockedText = t('dashboard.locked');

   return (
      <Box sx={{ minHeight: '100%' }}>
         {/* the dashboard has its own card for the followers */}
         <SiteHeader withoutConnections />

         <Container maxWidth="lg" sx={{ py: { xs: 3, md: 5 } }} id="creator-page">
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

            {/* a new creator: one card, the way in */}
            {config?.enabled && profile === null && (
               <Box sx={{ maxWidth: 560, mx: 'auto' }} id="creator-onboarding">
                  <Typography variant="h3" sx={{ fontSize: { xs: '2rem', md: '2.6rem' } }} gutterBottom>
                     {t('creator.title')}
                  </Typography>
                  <Typography color="text.secondary" sx={{ mb: 3 }}>
                     {t('creator.intro_new', { percent: config.platformCommissionPercent })}
                  </Typography>
                  <Paper id="creator-payout" sx={card}>
                     {form}
                  </Paper>
               </Box>
            )}

            {config?.enabled && profile && (
               <Box sx={{ display: 'flex', flexDirection: 'column', gap: { xs: 2.5, md: 3 } }} id="creator-dashboard">
                  <ProfileHero profile={profile} />

                  <SetupChecklist steps={steps} />

                  <Box
                     id="creator-stats"
                     sx={{
                        display: 'grid',
                        gap: 2,
                        gridTemplateColumns: { xs: 'repeat(2, 1fr)', md: 'repeat(4, 1fr)' },
                     }}
                  >
                     <StatCard
                        id="stat-followers"
                        label={t('dashboard.stat_followers')}
                        icon={<PeopleOutlinedIcon fontSize="small" />}
                        value={loading ? undefined : data.followers === undefined ? null : compactCount(data.followers)}
                        hint={t('dashboard.stat_followers_hint')}
                        to="/connections?tab=followers"
                     />
                     <StatCard
                        id="stat-videos"
                        label={t('dashboard.stat_videos')}
                        icon={<VideoLibraryOutlinedIcon fontSize="small" />}
                        value={loading ? undefined : data.videos ? String(data.videos.ready) : null}
                        hint={
                           data.videos && data.videos.total > data.videos.ready
                              ? t('dashboard.stat_videos_more', { count: data.videos.total - data.videos.ready })
                              : t('dashboard.stat_videos_hint')
                        }
                        to="/creator/videos"
                     />
                     <StatCard
                        id="stat-sessions"
                        label={t('dashboard.stat_sessions')}
                        icon={<LiveTvOutlinedIcon fontSize="small" />}
                        value={loading ? undefined : data.sessions ? String(data.sessions.active) : null}
                        hint={t('dashboard.stat_sessions_hint')}
                        to="/creator/sessions"
                     />
                     <StatCard
                        id="stat-earnings"
                        label={t('dashboard.stat_earnings')}
                        icon={<PaymentsOutlinedIcon fontSize="small" />}
                        value={
                           !profile.canSell
                              ? null
                              : loading
                              ? undefined
                              : data.earnings
                              ? formatMoney(data.earnings.netMinor, data.earnings.currency)
                              : null
                        }
                        hint={
                           !profile.canSell
                              ? lockedText
                              : data.earnings
                              ? t('dashboard.stat_earnings_hint', { count: data.earnings.sales })
                              : undefined
                        }
                        to={profile.canSell ? '/creator/earnings' : undefined}
                     />
                  </Box>

                  <Box>
                     <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>
                        {t('dashboard.actions_title')}
                     </Typography>
                     <Box
                        id="creator-actions"
                        sx={{
                           display: 'grid',
                           gap: 2,
                           gridTemplateColumns: { xs: '1fr', md: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' },
                        }}
                     >
                        <ActionCard
                           id="creator-sessions-link"
                           primary
                           title={t('sessions.my_title')}
                           text={t('dashboard.action_sessions')}
                           icon={<LiveTvOutlinedIcon />}
                           to="/creator/sessions"
                           locked={!profile.canSell}
                           lockedText={lockedText}
                           onLockedClick={() => scrollTo('creator-payout')}
                        />
                        <ActionCard
                           id="creator-videos-link"
                           primary
                           title={t('videos.title')}
                           text={t('dashboard.action_videos')}
                           icon={<VideoLibraryOutlinedIcon />}
                           to="/creator/videos"
                        />
                        <ActionCard
                           id="creator-earnings-link"
                           title={t('creator.earnings_title')}
                           text={t('dashboard.action_earnings')}
                           icon={<PaymentsOutlinedIcon />}
                           to="/creator/earnings"
                           locked={!profile.canSell}
                           lockedText={lockedText}
                           onLockedClick={() => scrollTo('creator-payout')}
                        />
                        <ActionCard
                           id="creator-followers-link"
                           title={t('dashboard.action_followers_title')}
                           text={t('dashboard.action_followers')}
                           icon={<PeopleOutlinedIcon />}
                           to="/connections?tab=followers"
                        />
                        {profile.canSell && (
                           <ActionCard
                              id="creator-public-link"
                              title={t('dashboard.action_page_title')}
                              text={t('dashboard.action_page')}
                              icon={<PublicOutlinedIcon />}
                              to={`/u/${profile.slug || profile.creatorId}`}
                           />
                        )}
                        <ActionCard
                           id="creator-reels-link"
                           title={t('creators.nav_reels')}
                           text={t('dashboard.action_reels')}
                           icon={<SlideshowOutlinedIcon />}
                           to="/reels"
                        />
                     </Box>
                  </Box>

                  <Box>
                     <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>
                        {t('dashboard.settings_title')}
                     </Typography>
                     <Box
                        sx={{
                           display: 'grid',
                           gap: 2.5,
                           gridTemplateColumns: { xs: '1fr', md: 'minmax(0, 1.3fr) minmax(0, 1fr)' },
                           alignItems: 'start',
                        }}
                     >
                        <Paper id="creator-payout" sx={card}>
                           <Typography variant="overline" color="text.secondary">
                              {t('dashboard.settings_profile')}
                           </Typography>
                           <Box sx={{ mt: 1 }}>{form}</Box>
                        </Paper>

                        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
                           <Paper sx={{ ...card, ...inCard }}>
                              <PhotoSetup profile={profile} onSaved={setProfile} />
                           </Paper>
                           {profile.canSell && (
                              <Paper sx={{ ...card, ...inCard }}>
                                 <SlugSetup profile={profile} onSaved={setProfile} />
                              </Paper>
                           )}
                           {profile.canSell && (
                              <Paper sx={{ ...card, ...inCard }}>
                                 <PassSetup profile={profile} onSaved={setProfile} />
                              </Paper>
                           )}
                        </Box>
                     </Box>
                  </Box>
               </Box>
            )}
         </Container>
      </Box>
   );
}
