export type Logger = {
   info(message: string, data?: Record<string, unknown>): void;
   warn(message: string, data?: Record<string, unknown>): void;
   error(message: string, data?: Record<string, unknown>): void;
};

/** One JSON object per line, easy to read by docker logs and log collectors. Never logs tokens or secrets. */
export function createLogger(context: Record<string, unknown> = {}): Logger {
   const write = (level: string, message: string, data?: Record<string, unknown>) =>
      console.log(JSON.stringify({ time: new Date().toISOString(), level, message, ...context, ...data }));

   return {
      info: (message, data) => write('info', message, data),
      warn: (message, data) => write('warn', message, data),
      error: (message, data) => write('error', message, data),
   };
}

export const silentLogger: Logger = { info: () => undefined, warn: () => undefined, error: () => undefined };
