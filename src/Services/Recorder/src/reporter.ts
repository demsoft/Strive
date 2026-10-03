import { Config } from './config';
import { Logger } from './logger';
import { Report, Reporter } from './types';

const SECRET_HEADER = 'X-Recorder-Secret';

/** Tells the Strive server what happens, retrying for a while: the final report must not get lost. */
export class HttpReporter implements Reporter {
   constructor(
      private readonly config: Pick<Config, 'striveApiUrl' | 'sharedSecret'>,
      private readonly log: Logger,
      private readonly attempts = 6,
      private readonly delayMs = 2000,
      private readonly fetchFn: typeof fetch = fetch,
   ) {}

   async report(recordingId: string, report: Report): Promise<void> {
      const url = `${this.config.striveApiUrl}/internal/recorder/${encodeURIComponent(recordingId)}/report`;

      for (let attempt = 1; ; attempt++) {
         try {
            const response = await this.fetchFn(url, {
               method: 'POST',
               headers: { 'Content-Type': 'application/json', [SECRET_HEADER]: this.config.sharedSecret },
               body: JSON.stringify(report),
            });

            if (response.ok) return;

            // the server does not know the recording or the secret is wrong: trying again does not help
            if (response.status === 401 || response.status === 404) {
               this.log.error('The server refused the report', { recordingId, status: response.status });
               return;
            }

            throw new Error(`status ${response.status}`);
         } catch (error) {
            if (attempt >= this.attempts) {
               this.log.error('Could not report to the server, giving up', {
                  recordingId,
                  event: report.event,
                  error: String(error),
               });
               return;
            }

            this.log.warn('Reporting failed, retrying', { recordingId, attempt, error: String(error) });
            await new Promise((resolve) => setTimeout(resolve, this.delayMs));
         }
      }
   }
}
