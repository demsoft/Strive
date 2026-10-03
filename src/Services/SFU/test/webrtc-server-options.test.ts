import { buildWebRtcServerOptions } from '../src/webrtc-server-options';

describe('buildWebRtcServerOptions', () => {
   it('listens on one udp and one tcp port per worker', () => {
      const options = buildWebRtcServerOptions(40000, 2, '0.0.0.0', '203.0.113.7');

      expect(options.listenInfos).toEqual([
         { protocol: 'udp', ip: '0.0.0.0', announcedAddress: '203.0.113.7', port: 40002 },
         { protocol: 'tcp', ip: '0.0.0.0', announcedAddress: '203.0.113.7', port: 40002 },
      ]);
   });

   it('uses different ports for different workers', () => {
      const ports = [0, 1, 2, 3].map((i) => (buildWebRtcServerOptions(40000, i, '0.0.0.0').listenInfos[0] as any).port);

      expect(new Set(ports).size).toBe(4);
   });

   it('announces nothing when no public address is given', () => {
      const options = buildWebRtcServerOptions(40000, 0, '127.0.0.1', '');

      expect((options.listenInfos[0] as any).announcedAddress).toBeUndefined();
   });
});
