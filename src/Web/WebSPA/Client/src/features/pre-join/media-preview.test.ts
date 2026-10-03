import { buildConstraints } from './media-preview';

test('default device if none selected', () => {
   expect(buildConstraints('video')).toEqual({ audio: false, video: true });
   expect(buildConstraints('audio')).toEqual({ audio: true, video: false });
});

test('selected local device is requested exactly', () => {
   expect(buildConstraints('video', { type: 'local', deviceId: 'cam1' })).toEqual({
      audio: false,
      video: { deviceId: { exact: 'cam1' } },
   });
   expect(buildConstraints('audio', { type: 'local', deviceId: 'mic1' })).toEqual({
      audio: { deviceId: { exact: 'mic1' } },
      video: false,
   });
});

test('equipment devices are not opened locally', () => {
   expect(buildConstraints('video', { type: 'equipment', deviceId: 'x', connectionId: 'c' })).toEqual({
      audio: false,
      video: true,
   });
});
