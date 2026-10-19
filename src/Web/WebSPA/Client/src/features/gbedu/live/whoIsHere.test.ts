import { expect, test } from 'vitest';
import { whoIsHere } from './ViewersChip';

const p = (id: string, displayName: string) => ({ id, displayName });

test('the hosts come first, then everybody by name', () => {
   const result = whoIsHere([p('z', 'Zainab'), p('h', 'Ada'), p('b', 'Bola')], ['h']);

   expect(result.map((x) => x.id)).toEqual(['h', 'b', 'z']);
});

test('the recorder is not a person in the room', () => {
   const result = whoIsHere([p('recorder-123', 'Recording'), p('a', 'Ada')], []);

   expect(result.map((x) => x.id)).toEqual(['a']);
});
