import { Alert, AppBar, Box, Button, CircularProgress, Container, Toolbar, Typography } from '@mui/material';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import * as api from 'src/services/api/follows';
import { NoticeAnswer } from 'src/services/api/follows';

/** The page behind the links in the mails to followers: /n/confirm/:token and /n/stop/:token. */
export default function NoticePage() {
   const { t } = useTranslation();
   const { action, token } = useParams<{ action: string; token: string }>();
   const [answer, setAnswer] = useState<NoticeAnswer | null>(null);
   const [invalid, setInvalid] = useState(false);

   useEffect(() => {
      // asked by the page, not by the link itself: a program that opens the links of a mail changes nothing
      const call = action === 'confirm' ? api.confirmNotice : action === 'stop' ? api.stopNotice : null;
      if (!call) return setInvalid(true);
      let cancelled = false;
      call(token).then(
         (result) => !cancelled && setAnswer(result),
         () => !cancelled && setInvalid(true),
      );
      return () => {
         cancelled = true;
      };
   }, [action, token]);

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>
         <Container maxWidth="sm" sx={{ py: 6 }} id="notice-page">
            {invalid && (
               <Alert severity="error" id="notice-invalid">
                  {t('social.notice_page_invalid')}
               </Alert>
            )}
            {!invalid && !answer && (
               <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                  <CircularProgress size={24} />
                  <Typography color="text.secondary">{t('social.notice_page_working')}</Typography>
               </Box>
            )}
            {answer && (
               <Alert severity="success" id="notice-done">
                  {answer.status === 'on'
                     ? t('social.notice_page_confirmed', { name: answer.creatorName })
                     : t('social.notice_page_stopped', { name: answer.creatorName })}
               </Alert>
            )}
            <Button component={RouterLink} to="/" sx={{ mt: 3 }}>
               {t('social.notice_page_home')}
            </Button>
         </Container>
      </Box>
   );
}
