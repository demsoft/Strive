import { describe, expect, test } from 'vitest';
import { adaptToHost, isOtherHost } from './app-host';

const production = {
   frontendUrl: 'https://meet.example.com',
   identityUrl: 'https://meet.example.com/account',
   other: 1,
};

describe('adaptToHost', () => {
   test('the main address stays as it is', () => {
      expect(adaptToHost(production, 'https://meet.example.com')).toBe(production);
   });

   test('on another host the app and the sign in pages are reached through that host', () => {
      const result = adaptToHost(production, 'https://gbedu.example.com');

      expect(result.frontendUrl).toBe('https://gbedu.example.com');
      expect(result.identityUrl).toBe('https://gbedu.example.com/account');
      expect(result.other).toBe(1);
   });

   test('in development the sign in pages are on a host of their own: nothing changes', () => {
      const development = { frontendUrl: 'http://localhost:55103', identityUrl: 'http://localhost:55105' };

      expect(adaptToHost(development, 'http://127.0.0.1:55103')).toBe(development);
   });

   test('addresses that are not valid are left alone', () => {
      const broken = { frontendUrl: '', identityUrl: '' };

      expect(adaptToHost(broken, 'https://gbedu.example.com')).toBe(broken);
   });
});

describe('isOtherHost', () => {
   test('tells a second brand from the main one', () => {
      expect(isOtherHost(production, 'https://gbedu.example.com')).toBe(true);
      expect(isOtherHost(production, 'https://meet.example.com')).toBe(false);
   });
});
