import { EventEmitter } from 'events';
import { retry } from '@lifeomic/attempt';
import amqp, { Channel } from 'amqplib';
import Logger from '../utils/logger';

const logger = new Logger('RabbitMqConnection');

/**
 * RabbitMQ 4 rejects connections with a frame size below 8192, older amqplib versions negotiate 4096.
 * Use 128 KiB (the RabbitMQ default) unless the url sets frameMax explicitly.
 */
export function withFrameMax(url: string): string {
   const parsed = new URL(url);
   if (!parsed.searchParams.has('frameMax')) parsed.searchParams.set('frameMax', '131072');
   return parsed.toString();
}

export type RabbitChannel = {
   pub: Channel;
   sub: Channel;
};

/**
 * Manages the connection to RabbitMq
 */
export default class RabbitMqConn extends EventEmitter {
   private cachedChannel: RabbitChannel | undefined;

   /**
    * @param url the rabbit mq url
    */
   constructor(private url: string) {
      super();
   }

   /**
    * Try to connect to rabbit mq and return the channel. Only one channel is required as node is single threaded.
    */
   public async getChannel(): Promise<RabbitChannel> {
      if (this.cachedChannel) return this.cachedChannel;

      try {
         this.cachedChannel = await retry(
            async () => {
               const onError = () => {
                  logger.error('Connection failed');
                  this.cachedChannel = undefined;
                  this.getChannel(); // reconnect
               };

               const conn = await amqp.connect(withFrameMax(this.url));
               conn.on('error', onError);

               const pub = await conn.createChannel();
               pub.on('close', onError);

               const sub = await conn.createChannel();
               sub.on('close', onError);

               this.emit('reset');

               return { pub, sub };
            },
            {
               maxAttempts: 20,
               factor: 2,
               jitter: true,
               maxDelay: 5000,
               delay: 200,
               handleError: (err) => logger.warn('Creating rabbit mq channel failed: %s', err),
            },
         );
      } catch (error) {
         this.emit('fatal', error);
         throw error;
      }

      if (!this.cachedChannel) throw new Error('Failed to connect to RabbitMq');
      return this.cachedChannel;
   }
}
