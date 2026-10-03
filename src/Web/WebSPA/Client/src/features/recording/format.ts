/** 3725 -> "1:02:05", 65 -> "1:05" */
export function formatDuration(totalSeconds: number): string {
   const seconds = Math.max(0, Math.floor(totalSeconds));
   const h = Math.floor(seconds / 3600);
   const m = Math.floor((seconds % 3600) / 60);
   const s = seconds % 60;
   const pad = (x: number) => x.toString().padStart(2, '0');
   return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${m}:${pad(s)}`;
}

/** 1536 -> "1.5 KB", 5242880 -> "5.0 MB" */
export function formatSize(bytes: number): string {
   const units = ['B', 'KB', 'MB', 'GB', 'TB'];
   let value = bytes;
   let unit = 0;
   while (value >= 1024 && unit < units.length - 1) {
      value /= 1024;
      unit++;
   }
   return unit === 0 ? `${value} B` : `${value.toFixed(1)} ${units[unit]}`;
}
