export type Config = {
   port: number;
   /** Shared with the Strive server: every request in both directions must carry it. */
   sharedSecret: string;
   /** Where the Strive API listens for the reports of the recorder, e.g. http://strive */
   striveApiUrl: string;
   /** The web app the recorder opens, e.g. https://localhost */
   webUrl: string;
   workDir: string;
   storage: {
      endpoint?: string;
      bucket: string;
      accessKeyId: string;
      secretAccessKey: string;
      region: string;
      forcePathStyle: boolean;
   };
   video: { width: number; height: number; fps: number; crf: number };
   maxConcurrentRecordings: number;
   /** development: the web app uses a self-signed certificate */
   ignoreHttpsErrors: boolean;
   /** development: chromium host resolver rules, e.g. "MAP *.localhost traefik, MAP localhost traefik" */
   hostResolverRules?: string;
};

export class ConfigError extends Error {}

function int(env: NodeJS.ProcessEnv, name: string, fallback: number, min: number, max: number): number {
   const raw = env[name];
   if (raw === undefined || raw === '') return fallback;

   const value = Number(raw);
   if (!Number.isInteger(value) || value < min || value > max) {
      throw new ConfigError(`${name} must be an integer between ${min} and ${max}, got "${raw}"`);
   }
   return value;
}

/** Read the configuration from environment variables, report everything that is missing at once. */
export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
   const missing = [
      'RECORDER_SHARED_SECRET',
      'STRIVE_API_URL',
      'WEB_URL',
      'STORAGE_BUCKET',
      'STORAGE_ACCESS_KEY_ID',
      'STORAGE_SECRET_ACCESS_KEY',
   ].filter((name) => !env[name]);

   if (missing.length > 0) throw new ConfigError(`Missing environment variables: ${missing.join(', ')}`);

   const sharedSecret = env.RECORDER_SHARED_SECRET as string;
   if (sharedSecret.length < 16) throw new ConfigError('RECORDER_SHARED_SECRET must be at least 16 characters');

   return {
      port: int(env, 'PORT', 3000, 1, 65535),
      sharedSecret,
      striveApiUrl: (env.STRIVE_API_URL as string).replace(/\/+$/, ''),
      webUrl: (env.WEB_URL as string).replace(/\/+$/, ''),
      workDir: env.WORK_DIR || '/data/recordings',
      storage: {
         endpoint: env.STORAGE_ENDPOINT || undefined,
         bucket: env.STORAGE_BUCKET as string,
         accessKeyId: env.STORAGE_ACCESS_KEY_ID as string,
         secretAccessKey: env.STORAGE_SECRET_ACCESS_KEY as string,
         region: env.STORAGE_REGION || 'auto',
         forcePathStyle: env.STORAGE_FORCE_PATH_STYLE === 'true',
      },
      video: {
         width: int(env, 'VIDEO_WIDTH', 1280, 320, 3840),
         height: int(env, 'VIDEO_HEIGHT', 720, 240, 2160),
         fps: int(env, 'VIDEO_FPS', 25, 5, 60),
         crf: int(env, 'VIDEO_CRF', 26, 15, 40),
      },
      maxConcurrentRecordings: int(env, 'MAX_CONCURRENT_RECORDINGS', 2, 1, 32),
      ignoreHttpsErrors: env.IGNORE_HTTPS_ERRORS === 'true',
      hostResolverRules: env.HOST_RESOLVER_RULES || undefined,
   };
}
