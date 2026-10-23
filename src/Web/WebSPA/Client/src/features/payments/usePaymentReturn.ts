import { useEffect, useRef, useState } from 'react';
import { fetchPayment } from 'src/services/api/sessions';

export type PaymentReturn = 'checking' | 'paid' | 'failed' | 'waiting' | null;

const POLL_EVERY_MS = 2000;
const POLL_TIMES = 30;

/**
 * Coming back from the payment page (?reference=...): asks the server until it knows how the payment ended. `onPaid`
 * is called once when it was paid (to load what was bought). Nothing happens without a reference or a signed in person.
 */
export default function usePaymentReturn(
   reference: string | null,
   accessToken: string | undefined,
   onPaid: () => void,
): PaymentReturn {
   const [state, setState] = useState<PaymentReturn>(null);
   const polling = useRef(0);
   const paid = useRef(onPaid);
   paid.current = onPaid;

   useEffect(() => {
      if (!reference || !accessToken) return;

      const id = ++polling.current;
      setState('checking');
      (async () => {
         for (let i = 0; i < POLL_TIMES && id === polling.current; i++) {
            try {
               const status = await fetchPayment(reference, accessToken);
               if (status.status === 'paid') {
                  setState('paid');
                  paid.current();
                  return;
               }
               if (status.status === 'failed') {
                  setState('failed');
                  return;
               }
            } catch {
               // the next try
            }
            await new Promise((resolve) => setTimeout(resolve, POLL_EVERY_MS));
         }
         if (id === polling.current) setState('waiting');
      })();

      return () => {
         polling.current++;
      };
   }, [reference, accessToken]);

   return state;
}
