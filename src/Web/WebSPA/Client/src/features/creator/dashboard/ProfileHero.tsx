import { Alert, Box, Button, Chip, IconButton, Tooltip, Typography } from '@mui/material';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { CreatorProfile } from 'src/services/api/creators';
import { creatorPageLink } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import { creatorPhotoUrl } from '../photoUrl';

/** The top of the dashboard: who the creator is for the fans, the link of their page, and whether they can sell yet. */
export default function ProfileHero({ profile }: { profile: CreatorProfile }) {
   const { t } = useTranslation();
   const [copied, setCopied] = useState(false);
   const path = profile.slug || profile.creatorId;
   const link = creatorPageLink(path);
   const shown = link.replace(/^https?:\/\//, '');

   const copy = async () => {
      try {
         await navigator.clipboard.writeText(link);
         setCopied(true);
         setTimeout(() => setCopied(false), 2000);
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
      }
   };

   return (
      <Box
         id="creator-hero"
         sx={{
            position: 'relative',
            overflow: 'hidden',
            borderRadius: '28px',
            p: { xs: 2.5, md: 4 },
            border: `1px solid ${brand.border}`,
            background: `radial-gradient(700px 260px at 0% 0%, ${brand.glowA}, transparent), radial-gradient(600px 240px at 100% 0%, ${brand.glowB}, transparent), ${brand.surface}`,
         }}
      >
         <Box sx={{ display: 'flex', gap: { xs: 2, md: 3 }, alignItems: 'center', flexWrap: 'wrap' }}>
            <Box sx={{ borderRadius: '50%', p: '4px', background: brand.gradient, lineHeight: 0 }}>
               <Box sx={{ borderRadius: '50%', border: `4px solid ${brand.surface}`, lineHeight: 0 }}>
                  <HostAvatar
                     name={profile.displayName}
                     size={88}
                     background={brand.gradient}
                     photoUrl={creatorPhotoUrl(profile.creatorId, profile.photoVersion, 88)}
                  />
               </Box>
            </Box>

            <Box sx={{ flex: 1, minWidth: 220 }}>
               <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 600 }}>
                  {t('dashboard.welcome')}
               </Typography>
               <Typography id="creator-hero-name" variant="h4" sx={{ fontWeight: 800, lineHeight: 1.15 }}>
                  {profile.displayName}
               </Typography>
               {profile.bio && (
                  <Typography
                     color="text.secondary"
                     sx={{
                        mt: 0.5,
                        display: '-webkit-box',
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: 'vertical',
                        overflow: 'hidden',
                     }}
                  >
                     {profile.bio}
                  </Typography>
               )}
               <Box sx={{ display: 'flex', gap: 1, mt: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
                  {profile.canSell ? (
                     <Chip
                        id="creator-status"
                        size="small"
                        color="success"
                        icon={<CheckCircleOutlinedIcon />}
                        label={t('dashboard.status_ready')}
                     />
                  ) : (
                     <Chip id="creator-status" size="small" color="warning" label={t('dashboard.status_setup')} />
                  )}
               </Box>
            </Box>
         </Box>

         <Box
            sx={{
               mt: 2.5,
               display: 'flex',
               gap: 1.5,
               flexWrap: 'wrap',
               alignItems: 'center',
            }}
         >
            <Box
               id="creator-link-pill"
               sx={{
                  flex: '1 1 260px',
                  minWidth: 0,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 0.5,
                  pl: 2,
                  pr: 0.5,
                  py: 0.5,
                  borderRadius: '999px',
                  backgroundColor: 'rgba(255,255,255,0.06)',
                  border: `1px solid ${brand.border}`,
               }}
            >
               <Typography className="creator-link-text" noWrap sx={{ flex: 1, fontWeight: 600 }}>
                  {shown}
               </Typography>
               <Tooltip title={copied ? t('sessions.copied') : t('dashboard.copy_link')}>
                  <IconButton id="creator-copy-link" onClick={copy} aria-label={t('dashboard.copy_link')} size="small">
                     <ContentCopyIcon fontSize="small" />
                  </IconButton>
               </Tooltip>
            </Box>
            {profile.canSell && (
               <Button
                  id="creator-page-link"
                  component={RouterLink}
                  to={`/u/${path}`}
                  variant="contained"
                  endIcon={<OpenInNewIcon />}
                  sx={{ background: brand.gradient, borderRadius: '999px', px: 3, whiteSpace: 'nowrap' }}
               >
                  {t('creator.view_my_page')}
               </Button>
            )}
         </Box>
         {!profile.canSell && (
            <Alert severity="info" sx={{ mt: 2 }} id="creator-hero-setup-hint">
               {t('dashboard.setup_hint')}
            </Alert>
         )}
      </Box>
   );
}
