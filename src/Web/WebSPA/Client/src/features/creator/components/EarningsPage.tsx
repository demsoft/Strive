import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import { Alert, Box, Chip, CircularProgress, Typography } from '@mui/material';
import { DateTime } from 'luxon';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import PageShell from 'src/components/PageShell';
import SectionCard from 'src/components/SectionCard';
import { formatMoney } from 'src/features/sessions/money';
import * as api from 'src/services/api/creators';
import { Earnings } from 'src/services/api/creators';
import { brand } from 'src/theme';
import { requestErrorMessage } from '../errors';

function Stat({ id, label, value, big }: { id: string; label: string; value: string; big?: boolean }) {
   return (
      <Box
         id={id}
         sx={{
            flex: big ? '1 1 100%' : '1 1 160px',
            p: 2.5,
            borderRadius: '20px',
            backgroundColor: brand.surface,
            border: `1px solid ${brand.border}`,
            ...(big && { background: brand.gradientSoft }),
         }}
      >
         <Typography variant="body2" color="text.secondary">
            {label}
         </Typography>
         <Typography sx={{ fontWeight: 800, fontSize: big ? { xs: '2rem', sm: '2.6rem' } : '1.4rem' }}>
            {value}
         </Typography>
      </Box>
   );
}

/**
 * What a creator earned: the share after the commission of the platform, what went back to the fans, which sessions
 * sold, and the latest sales (the buyer is shown masked). The provider pays the share to the bank account itself.
 */
export default function EarningsPage() {
   const { t } = useTranslation();
   const [earnings, setEarnings] = useState<Earnings | null>(null);
   const [error, setError] = useState<string | null>(null);

   useEffect(() => {
      api.fetchMyEarnings().then(setEarnings, (e) => setError(requestErrorMessage(e)));
   }, []);

   const money = (minor: number) => formatMoney(minor, earnings?.currency ?? 'NGN');
   const totals = earnings?.totals;
   const nothingYet = totals && totals.tickets + totals.passes + (totals.videos ?? 0) + totals.refunds === 0;

   return (
      <PageShell
         id="earnings-page"
         icon={<PaymentsOutlinedIcon />}
         title={t('creator.earnings_title')}
         subtitle={t('creator.earnings_subtitle')}
         backFallback="/creator"
      >
         {error && <Alert severity="error">{error}</Alert>}
         {!error && !earnings && (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
               <CircularProgress />
            </Box>
         )}

         {earnings && totals && (
            <>
               <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', mb: 1 }}>
                  <Stat id="earn-net" big label={t('creator.earn_net')} value={money(totals.netMinor)} />
                  <Stat id="earn-gross" label={t('creator.earn_gross')} value={money(totals.grossMinor)} />
                  <Stat id="earn-fee" label={t('creator.earn_fee')} value={money(totals.platformMinor)} />
                  <Stat id="earn-refunded" label={t('creator.earn_refunded')} value={money(totals.refundedMinor)} />
               </Box>
               <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }} id="earn-counts">
                  {t('creator.earn_counts', {
                     tickets: totals.tickets,
                     passes: totals.passes,
                     videos: totals.videos ?? 0,
                  })}
               </Typography>

               {nothingYet ? (
                  <Typography color="text.secondary" id="earn-nothing">
                     {t('creator.earn_nothing')}
                  </Typography>
               ) : (
                  <>
                     <SectionCard title={t('creator.earn_by_session')}>
                        <Box id="earn-sessions">
                           {earnings.sessions.map((row) => (
                              <Box
                                 key={row.sessionId}
                                 className="earn-session"
                                 sx={{
                                    display: 'flex',
                                    justifyContent: 'space-between',
                                    gap: 2,
                                    py: 1.25,
                                    borderBottom: `1px solid ${brand.border}`,
                                 }}
                              >
                                 <Box sx={{ minWidth: 0 }}>
                                    <b>{row.title}</b>
                                    <Typography variant="body2" color="text.secondary">
                                       {t('creator.earn_tickets', { count: row.tickets })}
                                    </Typography>
                                 </Box>
                                 <Box sx={{ textAlign: 'right' }}>
                                    <b>{money(row.netMinor)}</b>
                                 </Box>
                              </Box>
                           ))}
                           {earnings.sessions.length === 0 && (
                              <Typography color="text.secondary">{t('creator.earn_no_sessions')}</Typography>
                           )}
                        </Box>
                     </SectionCard>

                     {earnings.videos && earnings.videos.length > 0 && (
                        <>
                           <SectionCard title={t('creator.earn_by_video')}>
                              <Box id="earn-videos">
                                 {earnings.videos.map((row) => (
                                    <Box
                                       key={row.videoId}
                                       className="earn-video"
                                       sx={{
                                          display: 'flex',
                                          justifyContent: 'space-between',
                                          gap: 2,
                                          py: 1.25,
                                          borderBottom: `1px solid ${brand.border}`,
                                       }}
                                    >
                                       <Box sx={{ minWidth: 0 }}>
                                          <b>{row.title}</b>
                                          <Typography variant="body2" color="text.secondary">
                                             {t('creator.earn_sales', { count: row.sales })}
                                          </Typography>
                                       </Box>
                                       <b>{money(row.netMinor)}</b>
                                    </Box>
                                 ))}
                              </Box>
                           </SectionCard>
                        </>
                     )}

                     <SectionCard title={t('creator.earn_recent')}>
                        <Box id="earn-recent">
                           {earnings.recent.map((sale, index) => (
                              <Box
                                 key={index}
                                 className="earn-sale"
                                 sx={{
                                    display: 'flex',
                                    justifyContent: 'space-between',
                                    gap: 2,
                                    py: 1.25,
                                    borderBottom: `1px solid ${brand.border}`,
                                    opacity: sale.refunded ? 0.6 : 1,
                                 }}
                              >
                                 <Box sx={{ minWidth: 0 }}>
                                    <b>
                                       {sale.kind === 'pass'
                                          ? t('creator.pass_title')
                                          : sale.sessionTitle ?? t('videos.earn_videos')}
                                    </b>
                                    {sale.refunded && (
                                       <Chip
                                          size="small"
                                          label={t('sessions.refunded_chip')}
                                          sx={{ ml: 1 }}
                                          className="earn-refunded"
                                       />
                                    )}
                                    <Typography variant="body2" color="text.secondary">
                                       {DateTime.fromISO(sale.at).toLocaleString(DateTime.DATETIME_MED)} · {sale.buyer}
                                    </Typography>
                                 </Box>
                                 <Box sx={{ textAlign: 'right' }}>
                                    <b>{money(sale.amountMinor)}</b>
                                 </Box>
                              </Box>
                           ))}
                        </Box>
                     </SectionCard>
                  </>
               )}

               <Alert severity="info" icon={false} sx={{ mt: 1, borderRadius: '16px' }} id="earn-payout-note">
                  {t('creator.earn_payout_note')}
               </Alert>
            </>
         )}
      </PageShell>
   );
}
