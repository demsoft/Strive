import { describe, expect, it } from 'vitest';
import { onCloseConferenceResult } from './sagas';

const run = (payload: unknown) => [...(function* () {
   const generator = onCloseConferenceResult({ type: 'x', payload } as never);
   let step = generator.next();
   while (!step.done) {
      yield step.value;
      step = generator.next();
   }
})()];

describe('closing the conference', () => {
   it('is not an error when the connection is closed before the answer arrives', () => {
      expect(run({ success: false, error: { code: 'UI/Signal_Error', message: 'Invocation canceled', type: 'InternalServerError' } })).toEqual([]);
   });

   it('is not an error when it worked', () => {
      expect(run({ success: true })).toEqual([]);
   });

   it('shows other errors, for example no permission', () => {
      const effects = run({ success: false, error: { code: 'PermissionDenied', message: 'no', type: 'Forbidden' } });

      expect(effects).toHaveLength(1);
      expect(JSON.stringify(effects[0])).toContain('showMessage');
   });
});
