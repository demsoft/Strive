import Axios from 'axios';
import appSettings from 'src/config';

export type AdminLevel = 'ok' | 'warning' | 'critical';

export type AdminAlert = { level: AdminLevel; message: string };

export type Capacity = {
   /** ok, warning or full (no room for more people) */
   level: 'ok' | 'warning' | 'full';
   summary: string;
   workerCount: number;
   maxWorkerCpuPercent: number;
   averageWorkerCpuPercent: number;
   memoryUsedPercent: number | null;
   loadPerCore: number | null;
   participants: number;
};

export type ConferenceSummary = {
   conferenceId: string;
   name: string | null;
   participants: number;
   openedAt: string | null;
   connections: number | null;
   producers: number | null;
   consumers: number | null;
};

export type ServiceStatus = {
   name: string;
   status: 'up' | 'down' | 'degraded';
   detail: string | null;
   latencyMs: number | null;
};

export type SfuStats = {
   uptimeSeconds: number;
   workers: { index: number; pid: number; cpuPercent: number; memoryKb: number }[];
   totals: { conferences: number; participants: number; connections: number; producers: number; consumers: number };
   system: {
      cpuCores: number;
      loadAverage1m: number;
      memoryTotalBytes: number;
      memoryAvailableBytes: number;
      processMemoryBytes: number;
   };
};

export type AccountStats = {
   totalUsers: number;
   confirmedUsers: number;
   withPassword: number;
   withGoogle: number;
   signups: { date: string; count: number }[];
};

export type RecorderStats = {
   active: number;
   maxConcurrent: number | null;
   disk: { freeBytes: number; totalBytes: number } | null;
};

export type Thresholds = {
   workerCpuWarningPercent: number;
   workerCpuCriticalPercent: number;
   memoryWarningPercent: number;
   memoryCriticalPercent: number;
   loadPerCoreWarning: number;
   loadPerCoreCritical: number;
   diskFreeWarningPercent: number;
   diskFreeCriticalPercent: number;
};

export type AdminOverview = {
   generatedAt: string;
   status: AdminLevel;
   alerts: AdminAlert[];
   capacity: Capacity;
   conferences: { open: number; participants: number; list: ConferenceSummary[] };
   sfu: SfuStats | null;
   services: ServiceStatus[];
   accounts: AccountStats | null;
   recorder: RecorderStats | null;
   thresholds: Thresholds;
};

export type AdminSample = {
   t: string;
   participants: number;
   conferences: number;
   maxWorkerCpu: number;
   averageWorkerCpu: number;
   memoryUsedPercent: number | null;
   loadPerCore: number | null;
   alerts: number;
};

/** The address of the API, whether the configured url ends with a slash or not. */
export const adminUrl = (path: string) => `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/admin/${path}`;

export async function fetchOverview(): Promise<AdminOverview> {
   const response = await Axios.get<AdminOverview>(adminUrl('overview'));
   return response.data;
}

export async function fetchHistory(hours: number): Promise<AdminSample[]> {
   const response = await Axios.get<{ samples: AdminSample[] }>(adminUrl('history'), { params: { hours } });
   return response.data.samples;
}

export type PaymentsPeriod = {
   days: number;
   grossMinor: number;
   platformMinor: number;
   creatorsMinor: number;
   refundedMinor: number;
   tickets: number;
   passes: number;
   renewals: number;
   refunds: number;
   videos?: number;
};

export type OrderRow = {
   at: string;
   kind: 'ticket' | 'pass' | 'renewal' | 'video';
   status: string;
   creatorName: string;
   sessionTitle?: string | null;
   amountMinor: number;
   buyer: string;
   problem?: string | null;
};

export type RefundProblem = {
   reference: string;
   creatorName: string;
   sessionTitle?: string | null;
   amountMinor: number;
   buyer: string;
   reason?: string | null;
   at: string;
};

export type SubscriptionProblem = {
   subscriptionId: string;
   creatorName: string;
   buyer: string;
   status: string;
   failedAttempts: number;
   nextChargeAt: string;
   endedAt?: string | null;
};

export type AdminPayments = {
   currency: string;
   paymentsEnabled: boolean;
   autoRenewEnabled: boolean;
   creators: number;
   activeSubscriptions: number;
   week: PaymentsPeriod;
   month: PaymentsPeriod;
   recent: OrderRow[];
   refundProblems: RefundProblem[];
   subscriptionProblems: SubscriptionProblem[];
};

export async function fetchAdminPayments(): Promise<AdminPayments> {
   return (await Axios.get<AdminPayments>(adminUrl('payments'))).data;
}
