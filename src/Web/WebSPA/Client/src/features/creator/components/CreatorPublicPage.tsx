import { Alert, AppBar, Box, Button, CircularProgress, Container, Toolbar, Typography } from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { SessionTile } from 'src/features/gbedu/DiscoverSessions';
import { creatorPhotoUrl } from '../photoUrl';
import PassCard from './PassCard';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import * as api from 'src/services/api/sessions';
import { CreatorPage } from 'src/services/api/sessions';
import { brand } from 'src/theme';

/**
 * The page of a creator, the one link to share: who they are and the sessions they have on. Everybody can read it;
 * a session on it leads to its own page, where the ticket is bought.
 */
export default function CreatorPublicPage() {
   const { t } = useTranslation();
   const { creatorId } = useParams<{ creatorId: string }>();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const [page, setPage] = useState<CreatorPage | null>(null);
   const [notFound, setNotFound] = useState(false);
   const [copied, setCopied] = useState(false);

   const load = useCallback(
      () => api.fetchCreatorPage(creatorId, token).then(setPage, () => setNotFound(true)),
      [creatorId, token],
   );

   useEffect(() => {
      if (auth.isLoading) return;
      load();
   }, [auth.isLoading, load]);

   const copy = async () => {
      const link = api.creatorPageLink(page?.creator.slug || creatorId);
      try {
         await navigator.clipboard.writeText(link);
         setCopied(true);
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
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

         <Container maxWidth="md" sx={{ py: { xs: 2, sm: 4 } }} id="creator-public-page">
            {notFound && <Alert severity="error">{t('creator.page_not_found')}</Alert>}
            {!notFound && !page && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
                  <CircularProgress />
               </Box>
            )}

            {page && (
               <>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 2.5, flexWrap: 'wrap', mb: 4 }}>
                     <HostAvatar
                        name={page.creator.displayName}
                        size={80}
                        background={brand.gradient}
                        photoUrl={creatorPhotoUrl(page.creator.creatorId, page.creator.photoVersion, 80)}
                     />
                     <Box sx={{ flex: 1, minWidth: 220 }}>
                        <Typography variant="h3" id="creator-name" sx={{ fontSize: { xs: '1.9rem', sm: '2.5rem' } }}>
                           {page.creator.displayName}
                        </Typography>
                        {page.creator.bio && (
                           <Typography color="text.secondary" sx={{ mt: 0.5 }} id="creator-bio">
                              {page.creator.bio}
                           </Typography>
                        )}
                     </Box>
                     <Button id="creator-share" variant="outlined" startIcon={<ContentCopyIcon />} onClick={copy}>
                        {copied ? t('sessions.copied') : t('creator.copy_page_link')}
                     </Button>
                  </Box>

                  <PassCard page={page} onPaid={load} />

                  <Typography variant="h5" sx={{ fontWeight: 800, mb: 2 }}>
                     {t('creator.page_sessions')}
                  </Typography>
                  {page.sessions.length === 0 ? (
                     <Typography color="text.secondary" id="creator-nothing-on">
                        {t('creator.page_nothing_on', { name: page.creator.displayName })}
                     </Typography>
                  ) : (
                     <Box
                        sx={{
                           display: 'grid',
                           gap: 2,
                           gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)' },
                        }}
                     >
                        {page.sessions.map((session) => (
                           <SessionTile key={session.sessionId} session={session} />
                        ))}
                     </Box>
                  )}
               </>
            )}
         </Container>
      </Box>
   );
}
