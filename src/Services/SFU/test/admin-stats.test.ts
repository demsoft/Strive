import { parseMemAvailable } from '../src/admin-stats';

describe('parseMemAvailable', () => {
   it('reads the available memory in bytes', () => {
      const text = 'MemTotal:       16384000 kB\nMemFree:          200000 kB\nMemAvailable:    8000000 kB\nBuffers: 1 kB\n';

      expect(parseMemAvailable(text)).toBe(8000000 * 1024);
   });

   it('is undefined without the line', () => {
      expect(parseMemAvailable('MemTotal: 1 kB\nMemFree: 1 kB\n')).toBeUndefined();
      expect(parseMemAvailable('')).toBeUndefined();
   });
});
