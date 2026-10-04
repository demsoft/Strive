export function formatBytes(bytes: number | null | undefined): string {
   if (bytes === null || bytes === undefined || !Number.isFinite(bytes)) return '–';

   const units = ['B', 'KB', 'MB', 'GB', 'TB'];
   let value = bytes;
   let unit = 0;
   while (value >= 1000 && unit < units.length - 1) {
      value /= 1000;
      unit++;
   }

   return `${value >= 100 || unit === 0 ? value.toFixed(0) : value.toFixed(1)} ${units[unit]}`;
}

/** 93784 -> "1d 2h", 3700 -> "1h 1m", 90 -> "1m" */
export function formatUptime(seconds: number): string {
   if (!Number.isFinite(seconds) || seconds < 0) return '–';

   const days = Math.floor(seconds / 86400);
   const hours = Math.floor((seconds % 86400) / 3600);
   const minutes = Math.floor((seconds % 3600) / 60);

   if (days > 0) return `${days}d ${hours}h`;
   if (hours > 0) return `${hours}h ${minutes}m`;
   return `${Math.max(1, minutes)}m`;
}

/** How long ago something happened: "just now", "5 min", "2 h", "3 d". */
export function formatAgo(iso: string | null | undefined, now: number = Date.now()): string {
   if (!iso) return '–';

   const seconds = Math.max(0, Math.round((now - Date.parse(iso)) / 1000));
   if (seconds < 60) return 'just now';
   if (seconds < 3600) return `${Math.floor(seconds / 60)} min`;
   if (seconds < 86400) return `${Math.floor(seconds / 3600)} h`;
   return `${Math.floor(seconds / 86400)} d`;
}

export type Tone = 'ok' | 'warning' | 'critical';

/** The color level of a value that gets worse when it grows. */
export function toneFor(value: number | null | undefined, warning: number, critical: number): Tone {
   if (value === null || value === undefined || Number.isNaN(value)) return 'ok';
   if (value >= critical) return 'critical';
   if (value >= warning) return 'warning';
   return 'ok';
}

/** The color level of a value that gets worse when it shrinks (free disk space). */
export function toneForFree(value: number | null | undefined, warning: number, critical: number): Tone {
   if (value === null || value === undefined || Number.isNaN(value)) return 'ok';
   if (value <= critical) return 'critical';
   if (value <= warning) return 'warning';
   return 'ok';
}
