import { isRecorderParticipant, parseRecorderToken } from './recorder';

const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
const jwt = (payload: object) => `${b64({ alg: 'HS256' })}.${b64(payload)}.signature`;

test('recorder participants are recognized by the prefix', () => {
   expect(isRecorderParticipant('recorder-abc')).toBe(true);
   expect(isRecorderParticipant('56696E63656E74')).toBe(false);
   expect(isRecorderParticipant('my-recorder-abc')).toBe(false);
});

test('parseRecorderToken reads the token and the participant id from the fragment', () => {
   const token = jwt({ sub: 'recorder-r1', conference_id: 'c' });

   expect(parseRecorderToken(`#token=${token}`)).toEqual({ token, participantId: 'recorder-r1' });
   expect(parseRecorderToken(`token=${token}`)).toEqual({ token, participantId: 'recorder-r1' });
});

test('parseRecorderToken rejects missing or broken tokens', () => {
   expect(parseRecorderToken('')).toBeNull();
   expect(parseRecorderToken('#other=1')).toBeNull();
   expect(parseRecorderToken('#token=not-a-jwt')).toBeNull();
   expect(parseRecorderToken(`#token=${jwt({ name: 'x' })}`)).toBeNull();
});
