import { describe, expect, test } from 'vitest';
import { UploadError, uploadToConverter } from './videos';

class FakeRequest {
   upload: { onprogress?: (e: { lengthComputable: boolean; loaded: number; total: number }) => void } = {};
   onload?: () => void;
   onerror?: () => void;
   onabort?: () => void;
   status = 0;
   responseText = '';
   opened?: { method: string; url: string };
   sent?: FormData;
   aborted = false;

   open(method: string, url: string) {
      this.opened = { method, url };
   }
   send(form: FormData) {
      this.sent = form;
   }
   abort() {
      this.aborted = true;
      this.onabort?.();
   }
}

const ticket = { url: 'https://converter.test/upload', fields: { public_id: 'x/1', timestamp: '1', signature: 'sig' } };
const file = new File(['video bytes'], 'reel.mp4', { type: 'video/mp4' });
const start = (request: FakeRequest, progress: (f: number) => void = () => undefined) =>
   uploadToConverter(ticket, file, progress, () => request as unknown as XMLHttpRequest);

describe('uploadToConverter', () => {
   test('posts every field of the ticket in order, then the file, to the address of the ticket', async () => {
      const request = new FakeRequest();
      const { promise } = start(request);

      expect(request.opened).toEqual({ method: 'POST', url: 'https://converter.test/upload' });
      expect([...request.sent!.keys()]).toEqual(['public_id', 'timestamp', 'signature', 'file']);
      expect(request.sent!.get('signature')).toBe('sig');
      expect((request.sent!.get('file') as File).name).toBe('reel.mp4');

      request.status = 200;
      request.onload!();
      await expect(promise).resolves.toBeUndefined();
   });

   test('reports how far it is, never more than all', () => {
      const request = new FakeRequest();
      const seen: number[] = [];
      start(request, (f) => seen.push(f));

      request.upload.onprogress!({ lengthComputable: true, loaded: 25, total: 100 });
      request.upload.onprogress!({ lengthComputable: true, loaded: 150, total: 100 });
      request.upload.onprogress!({ lengthComputable: false, loaded: 5, total: 0 });

      expect(seen).toEqual([0.25, 1]);
   });

   test('the message of the converter is passed on when it refuses', async () => {
      const request = new FakeRequest();
      const { promise } = start(request);

      request.status = 400;
      request.responseText = JSON.stringify({ error: { message: 'File size too large. Maximum is 104857600.' } });
      request.onload!();

      await expect(promise).rejects.toThrow('File size too large. Maximum is 104857600.');
      await expect(promise).rejects.toBeInstanceOf(UploadError);
   });

   test('an answer that is not json, or a lost connection, gives an error without a message', async () => {
      const refused = new FakeRequest();
      const first = start(refused);
      refused.status = 502;
      refused.responseText = '<html>bad gateway</html>';
      refused.onload!();
      await expect(first.promise).rejects.toMatchObject({ message: '' });

      const offline = new FakeRequest();
      const second = start(offline);
      offline.onerror!();
      await expect(second.promise).rejects.toMatchObject({ message: '' });
   });

   test('it can be cancelled', async () => {
      const request = new FakeRequest();
      const { promise, abort } = start(request);

      abort();

      expect(request.aborted).toBe(true);
      await expect(promise).rejects.toMatchObject({ message: 'aborted' });
   });
});
