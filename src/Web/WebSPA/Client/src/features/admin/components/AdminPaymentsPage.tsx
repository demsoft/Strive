import { Alert, AppBar, Box, Button, Chip, Container, Paper, Toolbar, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { brand } from 'src/theme';
import { formatMoney } from 'src/features/sessions/money';
import { AdminPayments, fetchAdminPayments, PaymentsPeriod } from '../api';

function Stat({ id, label, value, hint }: { id: string; label: string; value: string; hint?: string }) {
   return (
      <Paper
         id={id}
         sx={{ p: 2.25, flex: '1 1 150px', minWidth: 0, borderRadius: '20px', border: `1px solid ${brand.border}` }}
      >
         <Typography variant="body2" color="text.secondary">
            {label}
         </Typography>
         <Typography variant="h5" sx={{ fontWeight: 800 }}>
            {value}
         </Typography>
         {hint && (
            <Typography variant="caption" color="text.secondary">
               {hint}
            </Typography>
         )}
      </Paper>
   );
}

function Period({
   id,
   title,
   period,
   money,
}: {
   id: string;
   title: string;
   period: PaymentsPeriod;
   money: (n: number) => string;
}) {
   const { t } = useTranslation();
   return (
      <Box id={id} sx={{ mb: 3 }}>
         <Typography variant="overline" color="text.secondary">
            {title}
         </Typography>
         <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', mt: 0.5 }}>
            <Stat id={`${id}-gross`} label={t('admin.pay_gross')} value={money(period.grossMinor)} />
            <Stat id={`${id}-platform`} label={t('admin.pay_platform')} value={money(period.platformMinor)} />
            <Stat id={`${id}-creators`} label={t('admin.pay_creators')} value={money(period.creatorsMinor)} />
            <Stat
               id={`${id}-refunded`}
               label={t('admin.pay_refunded')}
               value={money(period.refundedMinor)}
               hint={t('admin.pay_count_refunds', { count: period.refunds })}
            />
         </Box>
         <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {t('admin.pay_counts', {
               tickets: period.tickets,
               passes: period.passes,
               renewals: period.renewals,
               videos: period.videos ?? 0,
            })}
         </Typography>
      </Box>
   );
}

/** The money on this server, for the administrators: what sold, what needs a person, the latest orders. */
export default function AdminPaymentsPage() {
   const { t } = useTranslation();
   const [data, setData] = useState<AdminPayments | null>(null);
   const [forbidden, setForbidden] = useState(false);
   const [failed, setFailed] = useState(false);

   const load = useCallback(() => {
      fetchAdminPayments().then(
         (payments) => {
            setData(payments);
            setFailed(false);
         },
         (e) => {
            const status = (e as { response?: { status?: number } }).response?.status;
            if (status === 403 || status === 401) setForbidden(true);
            else setFailed(true);
         },
      );
   }, []);

   useEffect(() => {
      load();
   }, [load]);

   const money = (minor: number) => formatMoney(minor, data?.currency ?? 'NGN');
   const when = (iso: string) => DateTime.fromISO(iso).toLocaleString(DateTime.DATETIME_SHORT);

   const header = (
      <AppBar position="static" color="transparent" elevation={0}>
         <Toolbar sx={{ gap: 2 }}>
            <BrandLogo size={28} />
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
               {t('admin.pay_title')}
            </Typography>
            <Box sx={{ flex: 1 }} />
            <Button size="small" startIcon={<RefreshIcon />} onClick={load}>
               {t('admin.refresh')}
            </Button>
            <Button size="small" component={RouterLink} to="/admin" startIcon={<ArrowBackIcon />}>
               {t('admin.title')}
            </Button>
         </Toolbar>
      </AppBar>
   );

   if (forbidden)
      return (
         <>
            {header}
            <Container maxWidth="sm" sx={{ mt: 8 }}>
               <Alert severity="info" id="admin-pay-forbidden">
                  {t('admin.forbidden')}
               </Alert>
            </Container>
         </>
      );

   if (!data)
      return (
         <>
            {header}
            <Container maxWidth="sm" sx={{ mt: 8 }}>
               {failed ? (
                  <Alert severity="error">{t('admin.pay_failed')}</Alert>
               ) : (
                  <Typography color="text.secondary">{t('admin.loading')}</Typography>
               )}
            </Container>
         </>
      );

   const needsAttention = data.refundProblems.length + data.subscriptionProblems.length;

   return (
      <>
         {header}
         <Container maxWidth="lg" sx={{ pt: 3, pb: 6 }} id="admin-payments-page">
            {!data.paymentsEnabled && (
               <Alert severity="warning" sx={{ mb: 2 }} id="admin-pay-disabled">
                  {t('admin.pay_disabled')}
               </Alert>
            )}

            <Paper
               id="admin-pay-attention"
               sx={{
                  p: 2.5,
                  mb: 3,
                  borderRadius: '20px',
                  border: 1,
                  borderColor: needsAttention ? 'warning.main' : 'success.main',
               }}
            >
               <Typography variant="h6" sx={{ fontWeight: 800 }}>
                  {needsAttention ? t('admin.pay_needs_attention', { count: needsAttention }) : t('admin.pay_all_good')}
               </Typography>

               {data.refundProblems.length > 0 && (
                  <Box sx={{ mt: 1.5 }} id="admin-pay-refunds">
                     <Typography variant="subtitle2">{t('admin.pay_refund_problems')}</Typography>
                     {data.refundProblems.map((p) => (
                        <Box
                           key={p.reference}
                           className="refund-problem"
                           sx={{ py: 0.75, borderTop: 1, borderColor: 'divider' }}
                        >
                           <b>{money(p.amountMinor)}</b> · {p.sessionTitle ?? ''} · {p.creatorName} · {p.buyer}
                           <Typography variant="body2" color="text.secondary">
                              {p.reason} · {p.reference} · {when(p.at)}
                           </Typography>
                        </Box>
                     ))}
                     <Typography variant="caption" color="text.secondary">
                        {t('admin.pay_refund_hint')}
                     </Typography>
                  </Box>
               )}

               {data.subscriptionProblems.length > 0 && (
                  <Box sx={{ mt: 1.5 }} id="admin-pay-subscriptions">
                     <Typography variant="subtitle2">{t('admin.pay_subscription_problems')}</Typography>
                     {data.subscriptionProblems.map((p) => (
                        <Box
                           key={p.subscriptionId}
                           className="subscription-problem"
                           sx={{ py: 0.75, borderTop: 1, borderColor: 'divider' }}
                        >
                           <Chip
                              size="small"
                              color={p.status === 'ended' ? 'error' : 'warning'}
                              label={t(`admin.pay_sub_${p.status}`)}
                              sx={{ mr: 1 }}
                           />
                           {p.creatorName} · {p.buyer}
                           <Typography variant="body2" color="text.secondary">
                              {t('admin.pay_failed_attempts', { count: p.failedAttempts })}
                              {p.endedAt
                                 ? ` · ${when(p.endedAt)}`
                                 : ` · ${t('admin.pay_next_try', { date: when(p.nextChargeAt) })}`}
                           </Typography>
                        </Box>
                     ))}
                  </Box>
               )}
            </Paper>

            <Period
               id="admin-pay-week"
               title={t('admin.pay_last_days', { days: data.week.days })}
               period={data.week}
               money={money}
            />
            <Period
               id="admin-pay-month"
               title={t('admin.pay_last_days', { days: data.month.days })}
               period={data.month}
               money={money}
            />

            <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', mb: 3 }}>
               <Stat
                  id="admin-pay-creators-count"
                  label={t('admin.pay_creators_count')}
                  value={String(data.creators)}
               />
               <Stat
                  id="admin-pay-subscriptions-count"
                  label={t('admin.pay_active_subscriptions')}
                  value={String(data.activeSubscriptions)}
                  hint={data.autoRenewEnabled ? undefined : t('admin.pay_auto_renew_off')}
               />
            </Box>

            <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
               {t('admin.pay_recent')}
            </Typography>
            <Paper
               id="admin-pay-recent"
               sx={{ borderRadius: '20px', border: `1px solid ${brand.border}`, overflow: 'hidden' }}
            >
               {data.recent.length === 0 && (
                  <Typography color="text.secondary" sx={{ p: 2 }}>
                     {t('admin.pay_none')}
                  </Typography>
               )}
               {data.recent.map((row, index) => (
                  <Box
                     key={index}
                     className="order-row"
                     sx={{
                        display: 'flex',
                        gap: 2,
                        justifyContent: 'space-between',
                        p: 1.5,
                        borderTop: index ? 1 : 0,
                        borderColor: 'divider',
                     }}
                  >
                     <Box sx={{ minWidth: 0 }}>
                        <Chip size="small" label={t(`admin.pay_kind_${row.kind}`)} sx={{ mr: 1 }} />
                        <Chip
                           size="small"
                           color={row.status === 'paid' ? 'success' : row.status === 'failed' ? 'error' : 'default'}
                           label={row.status}
                           sx={{ mr: 1 }}
                        />
                        <b>{row.sessionTitle ?? row.creatorName}</b>
                        <Typography variant="body2" color="text.secondary">
                           {row.creatorName} · {row.buyer} · {when(row.at)}
                           {row.problem ? ` · ${row.problem}` : ''}
                        </Typography>
                     </Box>
                     <b>{money(row.amountMinor)}</b>
                  </Box>
               ))}
            </Paper>
         </Container>
      </>
   );
}
