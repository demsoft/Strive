import { expect, test } from 'vitest';
import { levelToPercent, peakLevel } from './InstrumentLevelMeter';

test('peakLevel is the loudest sample, whatever its sign', () => {
   expect(peakLevel(new Float32Array([0.1, -0.5, 0.3]))).toBeCloseTo(0.5);
   expect(peakLevel(new Float32Array([0, 0]))).toBe(0);
});

test('silence is empty and full scale is full', () => {
   expect(levelToPercent(0)).toBe(0);
   expect(levelToPercent(1)).toBe(100);
   expect(levelToPercent(0.0001)).toBe(0);
});

test('half the volume is 6 decibels lower', () => {
   expect(levelToPercent(0.5)).toBeCloseTo(90, 0);
});
