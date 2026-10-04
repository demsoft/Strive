import {
   Alert,
   AppBar,
   Box,
   Button,
   Chip,
   Container,
   Paper,
   Table,
   TableBody,
   TableCell,
   TableHead,
   TableRow,
   Toolbar,
   ToggleButton,
   ToggleButtonGroup,
   Typography,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import RefreshIcon from '@mui/icons-material/Refresh';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { AdminLevel, AdminOverview, ServiceStatus } from '../api';
import { formatAgo, formatBytes, formatUptime, toneFor, toneForFree } from '../format';
import useAdminData from '../useAdminData';
import Meter, { toneColor } from './Meter';
import Sparkline from './Sparkline';

/** icon only on a phone */
const compactButton = { minWidth: 0, '& .MuiButton-startIcon': { mr: { xs: 0, sm: 1 }, ml: { xs: 0, sm: -0.5 } } };

const agoText = (time: number) => {
   const ago = formatAgo(new Date(time).toISOString());
   return ago === 'just now' ? ago : `${ago} ago`;
};

const RANGES = [
   { hours: 6, label: '6 h' },
   { hours: 24, label: '24 h' },
   { hours: 168, label: '7 d' },
];

function Card({ title, children, id }: { title: string; children: React.ReactNode; id?: string }) {
   return (
      <Paper id={id} sx={{ p: 2.5, height: '100%', minWidth: 0, overflow: 'hidden' }}>
         <Typography variant="overline" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
            {title}
         </Typography>
         {children}
      </Paper>
   );
}

function BigNumber({ value, label }: { value: React.ReactNode; label: string }) {
   return (
      <Box>
         <Typography variant="h4" sx={{ fontWeight: 800, lineHeight: 1.1 }}>
            {value}
         </Typography>
         <Typography variant="body2" color="text.secondary">
            {label}
         </Typography>
      </Box>
   );
}

const levelLabel = (level: AdminLevel) => (level === 'critical' ? 'Needs attention' : level === 'warning' ? 'Attention' : 'All good');

function StatusBanner({ overview }: { overview: AdminOverview }) {
   const { t } = useTranslation();
   const full = overview.capacity.level === 'full';
   const tone = overview.status;
   const color = toneColor(tone);

   return (
      <Paper
         id="admin-status"
         sx={{ p: 2.5, mb: 3, border: `1px solid ${color}`, backgroundColor: `${color}1A` }}
      >
         <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
            <Box sx={{ width: 14, height: 14, borderRadius: '50%', backgroundColor: color, flexShrink: 0 }} />
            <Typography variant="h5" sx={{ fontWeight: 800 }}>
               {full ? t('admin.status_full') : levelLabel(tone)}
            </Typography>
            <Chip
               size="small"
               id="admin-capacity-chip"
               label={t(`admin.capacity_${overview.capacity.level}`)}
               sx={{ ml: 'auto', backgroundColor: `${color}33` }}
            />
         </Box>
         <Typography sx={{ mt: 1 }} color="text.secondary">
            {overview.capacity.summary}
         </Typography>
         {overview.alerts.length > 0 && (
            <Box sx={{ mt: 1.5, display: 'grid', gap: 0.75 }}>
               {overview.alerts.map((alert, i) => (
                  <Alert key={i} severity={alert.level === 'critical' ? 'error' : 'warning'} variant="outlined" sx={{ py: 0 }}>
                     {alert.message}
                  </Alert>
               ))}
            </Box>
         )}
      </Paper>
   );
}

function ServiceRow({ service }: { service: ServiceStatus }) {
   const color = service.status === 'up' ? '#34D399' : service.status === 'degraded' ? '#FBBF24' : '#F87171';
   return (
      <TableRow>
         <TableCell sx={{ width: 28 }}>
            <Box sx={{ width: 10, height: 10, borderRadius: '50%', backgroundColor: color }} />
         </TableCell>
         <TableCell sx={{ fontWeight: 600 }}>{service.name}</TableCell>
         <TableCell color="text.secondary">{service.detail ?? (service.status === 'up' ? 'running' : service.status)}</TableCell>
         <TableCell align="right">{service.latencyMs === null ? '' : `${service.latencyMs} ms`}</TableCell>
      </TableRow>
   );
}

export default function AdminPage() {
   const { t } = useTranslation();
   const [hours, setHours] = useState(24);
   const { overview, history, forbidden, error, loadedAt, refresh } = useAdminData(hours);

   const header = (
      <AppBar position="static" color="transparent" elevation={0}>
         <Toolbar sx={{ gap: 2 }}>
            <BrandLogo size={28} />
            <Typography variant="h6" sx={{ fontWeight: 700, whiteSpace: 'nowrap' }}>
               {t('admin.title')}
            </Typography>
            <Box sx={{ flex: 1 }} />
            {loadedAt && (
               <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'none', sm: 'block' } }}>
                  {t('admin.updated', { time: agoText(loadedAt) })}
               </Typography>
            )}
            <Button size="small" startIcon={<RefreshIcon />} onClick={refresh} aria-label={t('admin.refresh')} sx={compactButton}>
               <Box component="span" sx={{ display: { xs: 'none', sm: 'inline' } }}>
                  {t('admin.refresh')}
               </Box>
            </Button>
            <Button
               size="small"
               component={RouterLink}
               to="/"
               startIcon={<ArrowBackIcon />}
               aria-label={t('admin.back')}
               sx={compactButton}
            >
               <Box component="span" sx={{ display: { xs: 'none', sm: 'inline' } }}>
                  {t('admin.back')}
               </Box>
            </Button>
         </Toolbar>
      </AppBar>
   );

   if (forbidden) {
      return (
         <>
            {header}
            <Container maxWidth="sm" sx={{ mt: 8 }}>
               <Alert severity="info" id="admin-forbidden">
                  {t('admin.forbidden')}
               </Alert>
            </Container>
         </>
      );
   }

   if (!overview) {
      return (
         <>
            {header}
            <Container maxWidth="sm" sx={{ mt: 8 }}>
               {error ? <Alert severity="error">{error}</Alert> : <Typography color="text.secondary">{t('admin.loading')}</Typography>}
            </Container>
         </>
      );
   }

   const th = overview.thresholds;
   const { capacity, sfu, accounts, recorder } = overview;
   const diskFree = recorder?.disk && recorder.disk.totalBytes > 0 ? (100 * recorder.disk.freeBytes) / recorder.disk.totalBytes : null;
   const labels = history.length
      ? [new Date(history[0].t).toLocaleString([], { weekday: 'short', hour: '2-digit', minute: '2-digit' }), t('admin.now')]
      : [undefined, undefined];
   const maxSignups = Math.max(1, ...(accounts?.signups.map((x) => x.count) ?? [1]));

   return (
      <>
         {header}
         <Container maxWidth="lg" sx={{ pt: 3, pb: 6 }} id="admin-page">
            {error && (
               <Alert severity="warning" sx={{ mb: 2 }}>
                  {error}
               </Alert>
            )}
            <StatusBanner overview={overview} />

            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, mb: 2 }}>
               <Card title={t('admin.now_title')} id="admin-now">
                  <Box sx={{ display: 'flex', gap: 4 }}>
                     <BigNumber value={overview.conferences.participants} label={t('admin.people')} />
                     <BigNumber value={overview.conferences.open} label={t('admin.conferences')} />
                     {accounts && <BigNumber value={accounts.totalUsers} label={t('admin.accounts')} />}
                  </Box>
                  {sfu && (
                     <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 2 }}>
                        {sfu.totals.producers} {t('admin.streams_up')} · {sfu.totals.consumers} {t('admin.streams_down')} ·{' '}
                        {t('admin.uptime')} {formatUptime(sfu.uptimeSeconds)}
                     </Typography>
                  )}
               </Card>

               <Card title={t('admin.capacity_title')} id="admin-capacity">
                  {sfu ? (
                     <>
                        {sfu.workers.map((worker) => (
                           <Meter
                              key={worker.index}
                              label={`${t('admin.worker')} ${worker.index + 1}`}
                              percent={worker.cpuPercent}
                              tone={toneFor(worker.cpuPercent, th.workerCpuWarningPercent, th.workerCpuCriticalPercent)}
                           />
                        ))}
                        <Typography variant="caption" color="text.secondary">
                           {t('admin.worker_hint')}
                        </Typography>
                     </>
                  ) : (
                     <Typography color="text.secondary">{t('admin.no_media_numbers')}</Typography>
                  )}
               </Card>

               <Card title={t('admin.resources_title')} id="admin-resources">
                  {sfu ? (
                     <>
                        <Meter
                           label={t('admin.memory')}
                           percent={capacity.memoryUsedPercent}
                           tone={toneFor(capacity.memoryUsedPercent, th.memoryWarningPercent, th.memoryCriticalPercent)}
                           valueText={`${formatBytes(sfu.system.memoryTotalBytes - sfu.system.memoryAvailableBytes)} / ${formatBytes(sfu.system.memoryTotalBytes)}`}
                        />
                        <Meter
                           label={t('admin.processor_load')}
                           percent={capacity.loadPerCore === null ? null : capacity.loadPerCore * 100}
                           tone={toneFor(capacity.loadPerCore, th.loadPerCoreWarning, th.loadPerCoreCritical)}
                           valueText={`${sfu.system.loadAverage1m.toFixed(1)} / ${sfu.system.cpuCores} ${t('admin.cores')}`}
                        />
                        {recorder && diskFree !== null && recorder.disk && (
                           <Meter
                              label={t('admin.recorder_disk')}
                              percent={100 - diskFree}
                              tone={toneForFree(diskFree, th.diskFreeWarningPercent, th.diskFreeCriticalPercent)}
                              valueText={`${formatBytes(recorder.disk.freeBytes)} ${t('admin.free')}`}
                           />
                        )}
                     </>
                  ) : (
                     <Typography color="text.secondary">{t('admin.no_media_numbers')}</Typography>
                  )}
                  <Typography variant="caption" color="text.secondary">
                     {t('admin.resources_hint')}
                  </Typography>
               </Card>
            </Box>

            <Paper sx={{ p: 2.5, mb: 2 }} id="admin-history">
               <Box sx={{ display: 'flex', alignItems: 'center', mb: 1.5 }}>
                  <Typography variant="overline" color="text.secondary">
                     {t('admin.history_title')}
                  </Typography>
                  <Box sx={{ flex: 1 }} />
                  <ToggleButtonGroup size="small" exclusive value={hours} onChange={(_, v) => v && setHours(v)}>
                     {RANGES.map((range) => (
                        <ToggleButton key={range.hours} value={range.hours}>
                           {range.label}
                        </ToggleButton>
                     ))}
                  </ToggleButtonGroup>
               </Box>
               {history.length < 2 ? (
                  <Typography color="text.secondary">{t('admin.history_empty')}</Typography>
               ) : (
                  <Box sx={{ display: 'grid', gap: 3, gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' } }}>
                     <Sparkline
                        title={t('admin.people')}
                        values={history.map((x) => x.participants)}
                        color="#7C5CFF"
                        currentText={`${history[history.length - 1].participants} · max ${Math.max(...history.map((x) => x.participants))}`}
                        firstLabel={labels[0]}
                        lastLabel={labels[1]}
                     />
                     <Sparkline
                        title={t('admin.busiest_worker')}
                        values={history.map((x) => x.maxWorkerCpu)}
                        max={100}
                        color="#22D3EE"
                        currentText={`${history[history.length - 1].maxWorkerCpu.toFixed(0)}%`}
                        lines={[
                           { value: th.workerCpuWarningPercent, color: '#FBBF24' },
                           { value: th.workerCpuCriticalPercent, color: '#F87171' },
                        ]}
                        firstLabel={labels[0]}
                        lastLabel={labels[1]}
                     />
                     <Sparkline
                        title={t('admin.memory')}
                        values={history.map((x) => x.memoryUsedPercent)}
                        max={100}
                        color="#34D399"
                        currentText={
                           history[history.length - 1].memoryUsedPercent === null
                              ? '–'
                              : `${history[history.length - 1].memoryUsedPercent!.toFixed(0)}%`
                        }
                        lines={[
                           { value: th.memoryWarningPercent, color: '#FBBF24' },
                           { value: th.memoryCriticalPercent, color: '#F87171' },
                        ]}
                        firstLabel={labels[0]}
                        lastLabel={labels[1]}
                     />
                  </Box>
               )}
            </Paper>

            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', md: '2fr 1fr' }, mb: 2 }}>
               <Card title={t('admin.live_conferences')} id="admin-conferences">
                  {overview.conferences.list.length === 0 ? (
                     <Typography color="text.secondary">{t('admin.no_conferences')}</Typography>
                  ) : (
                     <Box sx={{ overflowX: 'auto' }}>
                     <Table size="small">
                        <TableHead>
                           <TableRow>
                              <TableCell>{t('admin.conference')}</TableCell>
                              <TableCell align="right">{t('admin.people')}</TableCell>
                              <TableCell align="right">{t('admin.streams')}</TableCell>
                              <TableCell align="right">{t('admin.open_for')}</TableCell>
                           </TableRow>
                        </TableHead>
                        <TableBody>
                           {overview.conferences.list.map((conference) => (
                              <TableRow key={conference.conferenceId}>
                                 <TableCell>
                                    <Typography sx={{ fontWeight: 600 }}>{conference.name || conference.conferenceId}</Typography>
                                    {conference.name && (
                                       <Typography variant="caption" color="text.secondary">
                                          {conference.conferenceId}
                                       </Typography>
                                    )}
                                 </TableCell>
                                 <TableCell align="right">{conference.participants}</TableCell>
                                 <TableCell align="right">
                                    {conference.producers === null ? '–' : `${conference.producers} / ${conference.consumers}`}
                                 </TableCell>
                                 <TableCell align="right">{formatAgo(conference.openedAt)}</TableCell>
                              </TableRow>
                           ))}
                        </TableBody>
                     </Table>
                     </Box>
                  )}
               </Card>

               <Card title={t('admin.services_title')} id="admin-services">
                  <Table size="small">
                     <TableBody>
                        {overview.services.map((service) => (
                           <ServiceRow key={service.name} service={service} />
                        ))}
                     </TableBody>
                  </Table>
                  {recorder && (
                     <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1 }}>
                        {t('admin.recordings_running', { active: recorder.active, max: recorder.maxConcurrent ?? '–' })}
                     </Typography>
                  )}
               </Card>
            </Box>

            {accounts && (
               <Card title={t('admin.accounts_title')} id="admin-accounts">
                  <Box sx={{ display: 'flex', gap: 4, flexWrap: 'wrap', mb: 2 }}>
                     <BigNumber value={accounts.totalUsers} label={t('admin.accounts')} />
                     <BigNumber value={accounts.confirmedUsers} label={t('admin.confirmed')} />
                     <BigNumber value={accounts.withPassword} label={t('admin.with_password')} />
                     <BigNumber value={accounts.withGoogle} label={t('admin.with_google')} />
                  </Box>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                     {t('admin.signups_14')}
                  </Typography>
                  <Box sx={{ display: 'flex', alignItems: 'flex-end', gap: 0.5, height: 64 }} role="img" aria-label={t('admin.signups_14')}>
                     {accounts.signups.map((day) => (
                        <Box
                           key={day.date}
                           title={`${day.date}: ${day.count}`}
                           sx={{
                              flex: 1,
                              height: `${Math.max(4, (day.count / maxSignups) * 100)}%`,
                              borderRadius: 0.5,
                              backgroundColor: day.count > 0 ? '#7C5CFF' : 'rgba(255,255,255,0.08)',
                           }}
                        />
                     ))}
                  </Box>
               </Card>
            )}
         </Container>
      </>
   );
}
