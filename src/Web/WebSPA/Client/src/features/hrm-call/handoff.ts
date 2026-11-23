export type HrmCallSession = {
   organizationId: string;
   conferenceId: string;
   token: string;
};

/**
 * Receive a call credential from the containing HRM window, never a URL or browser storage.
 * parentOrigin must come from trusted deployment configuration, not a URL parameter or message.
 * The server must still verify the credential and current call membership before joining.
 */
export function receiveHrmCallSession({
   parentOrigin,
   organizationId,
   conferenceId,
   onSession,
   onTimeout,
   timeoutMs = 15000,
   channel = 'call',
}: {
   parentOrigin: string;
   organizationId: string;
   conferenceId: string;
   onSession: (session: HrmCallSession) => void;
   onTimeout: () => void;
   timeoutMs?: number;
   channel?: 'call' | 'meeting';
}): () => void {
   const origin = new URL(parentOrigin);
   if (origin.protocol !== 'https:' || origin.origin !== parentOrigin || !organizationId || !conferenceId)
      throw new Error('A configured HTTPS parent origin and exact call scope are required.');
   if (window.parent === window) throw new Error('An HRM call must be embedded.');

   const nonce = crypto.randomUUID();
   const parent = window.parent;
   let active = true;
   let timer: ReturnType<typeof setTimeout>;
   const dispose = () => {
      active = false;
      clearTimeout(timer);
      window.removeEventListener('message', receive);
   };
   const receive = (event: MessageEvent) => {
      if (!active || event.source !== parent || event.origin !== parentOrigin) return;
      const message = event.data;
      if (
         !message ||
         message.type !== `goshrm.${channel}.session` ||
         message.nonce !== nonce ||
         message.organizationId !== organizationId ||
         message.conferenceId !== conferenceId ||
         typeof message.token !== 'string' ||
         !message.token.trim() ||
         message.token.length > 8192
      )
         return;

      dispose();
      onSession({ organizationId, conferenceId, token: message.token });
   };

   window.addEventListener('message', receive);
   timer = setTimeout(() => {
      dispose();
      onTimeout();
   }, timeoutMs);
   parent.postMessage({ type: `goshrm.${channel}.ready`, nonce, organizationId, conferenceId }, parentOrigin);
   return dispose;
}
