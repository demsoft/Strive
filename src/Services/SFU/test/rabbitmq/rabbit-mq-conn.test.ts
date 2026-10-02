import { withFrameMax } from '../../src/rabbitmq/rabbit-mq-conn';

test('withFrameMax adds frameMax if not set', () => {
   expect(withFrameMax('amqp://rabbitmq')).toBe('amqp://rabbitmq?frameMax=131072');
});

test('withFrameMax keeps other parameters', () => {
   expect(withFrameMax('amqp://user:pw@rabbitmq:5672/vhost?heartbeat=10')).toBe(
      'amqp://user:pw@rabbitmq:5672/vhost?heartbeat=10&frameMax=131072',
   );
});

test('withFrameMax keeps an explicit frameMax', () => {
   expect(withFrameMax('amqp://rabbitmq?frameMax=8192')).toBe('amqp://rabbitmq?frameMax=8192');
});
