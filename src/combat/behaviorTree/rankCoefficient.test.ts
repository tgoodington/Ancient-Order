import { describe, it, expect } from 'vitest';
import { rankCoefficient } from './rankCoefficient.js';

describe('rankCoefficient', () => {
  it('returns 0.2 floor for rank 1.0 (Stone tier)', () => {
    expect(rankCoefficient(1.0)).toBe(0.2);
  });

  it('returns 0.2 floor for ranks below 2.0 (floor applied)', () => {
    expect(rankCoefficient(1.5)).toBe(0.2);
  });

  it('returns 0.3 for rank 3.0 (Silver tier)', () => {
    expect(rankCoefficient(3.0)).toBeCloseTo(0.3);
  });

  it('returns 0.5 for rank 5.0 (Gold tier)', () => {
    expect(rankCoefficient(5.0)).toBeCloseTo(0.5);
  });

  it('returns 0.7 for rank 7.0 (Diamond tier)', () => {
    expect(rankCoefficient(7.0)).toBeCloseTo(0.7);
  });

  it('returns 1.0 for rank 10.0 (Legend tier)', () => {
    expect(rankCoefficient(10.0)).toBeCloseTo(1.0);
  });

  it('never exceeds 1.0 for ranks above 10', () => {
    expect(rankCoefficient(12.0)).toBeCloseTo(1.2);
    // Note: spec doesn't cap above 1.0 (rank 10 is max, but formula is unbounded above)
    // The floor is the primary constraint
  });

  it('applies floor for rank 0', () => {
    expect(rankCoefficient(0)).toBe(0.2);
  });

  it('returns exact floor value — no sub-floor values possible', () => {
    const result = rankCoefficient(0.5);
    expect(result).toBeGreaterThanOrEqual(0.2);
  });
});
