import type { RouterOptions, WebRtcTransportOptions, WorkerSettings } from 'mediasoup/types';
import * as os from 'os';

const config: Config = {
   mediasoup: {
      // each worker is one process (and one cpu core), a small number is enough for the conferences of one server
      numWorkers: Number(process.env.MEDIASOUP_NUM_WORKERS) || Object.keys(os.cpus()).length,
      // When set, every worker listens on ONE udp and tcp port for all of its transports (MEDIASOUP_WEBRTC_SERVER_PORT +
      // the index of the worker) instead of a port per transport from the range below. Needed when the ports are forwarded
      // one by one (Docker Desktop, NAT), 10000 forwarded ports are not practical.
      webRtcServerBasePort: Number(process.env.MEDIASOUP_WEBRTC_SERVER_PORT) || undefined,
      workerSettings: {
         logLevel: 'debug',
         logTags: ['info', 'ice', 'dtls', 'rtp', 'srtp', 'rtcp', 'rtx', 'bwe', 'score', 'simulcast', 'svc', 'sctp'],
         rtcMinPort: Number(process.env.MEDIASOUP_MIN_PORT) || 40000,
         rtcMaxPort: Number(process.env.MEDIASOUP_MAX_PORT) || 49999,
      },
   },
   router: {
      mediaCodecs: [
         {
            kind: 'audio',
            mimeType: 'audio/opus',
            clockRate: 48000,
            channels: 2,
         },
         {
            kind: 'video',
            mimeType: 'video/VP8',
            clockRate: 90000,
            parameters: {
               'x-google-start-bitrate': 1000,
            },
         },
      ],
   },
   webRtcTransport: {
      options: {
         listenIps: [
            {
               ip: process.env.MEDIASOUP_LISTEN_IP || '127.0.0.1',
               announcedIp: process.env.MEDIASOUP_ANNOUNCED_IP,
            },
         ],
         initialAvailableOutgoingBitrate: 1000000,
         enableUdp: true,
         enableTcp: true,
         preferUdp: true,
      },
      // per sending transport: webcam, microphone and screen together. A screen with moving pictures needs more than slides.
      maxIncomingBitrate: Number(process.env.MEDIASOUP_MAX_INCOMING_BITRATE) || 4000000,
   },
   http: {
      port: Number(process.env.HTTP_PORT) || 3000,
   },
   services: {
      conferenceInfoRequestUrl:
         process.env.API_CONFERENCE_MANAGEMENT || 'http://localhost:55104/v1/sfu/{conferenceId}?apiKey=testApiKey',
      rabbitMq: process.env.AMQP_CONNECTION_STRING || 'amqp://localhost:5672',
      tokenSecret: process.env.API_TOKEN_SECRET || 'ae2c687f875a47c99e3a05096badbac6',
   },
};

export default config;

type Config = {
   mediasoup: {
      numWorkers: number;
      webRtcServerBasePort?: number;
      workerSettings: WorkerSettings;
   };
   router: RouterOptions;
   webRtcTransport: {
      options: WebRtcTransportOptions;
      maxIncomingBitrate?: number;
   };
   http: {
      port: number;
   };
   services: {
      conferenceInfoRequestUrl: string;
      rabbitMq: string;
      tokenSecret: string;
   };
};
