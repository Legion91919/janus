import { describe, expect, it } from 'vitest';
import { addRow, formatBody, readyToFlip, servedBy, summarise, type CapabilityStatus, type LogRow } from './bridge';

const row = (id: number, side: LogRow['side']): LogRow =>
  ({ id, at: '09:00:00', path: '/customers', status: 200, side, mode: 'Legacy', ms: 5, body: '' });

describe('servedBy', () => {
  it('reads the facade header case-insensitively', () => {
    expect(servedBy(new Headers({ 'X-Served-By': 'legacy' }))).toBe('legacy');
    expect(servedBy(new Headers({ 'x-served-by': 'NEW' }))).toBe('new');
  });
  it('treats a missing or odd header as unknown, never as a side', () => {
    expect(servedBy(new Headers())).toBe('unknown');
    expect(servedBy(new Headers({ 'X-Served-By': 'both' }))).toBe('unknown');
  });
});

describe('request log', () => {
  it('counts which side served each request, newest first and capped', () => {
    let rows: LogRow[] = [];
    rows = addRow(rows, row(1, 'legacy'), 2);
    rows = addRow(rows, row(2, 'legacy'), 2);
    rows = addRow(rows, row(3, 'new'), 2);
    expect(rows.map((r) => r.id)).toEqual([3, 2]);
    expect(summarise(rows)).toEqual({ legacy: 1, new: 1, unknown: 0 });
  });
  it('pretty-prints JSON but keeps fixed-width legacy text as is', () => {
    expect(formatBody('application/json', '{"a":1}')).toBe('{\n  "a": 1\n}');
    expect(formatBody('text/plain', 'SCH LN-1  001')).toBe('SCH LN-1  001');
  });
});

describe('readyToFlip', () => {
  const cap = (mode: CapabilityStatus['mode'], matched: number, mismatched: number): CapabilityStatus =>
    ({ name: 'customers', mode, migrated: true, shadow: { matched, mismatched, lastMismatchFields: [] } });
  it('needs Shadow mode, enough matches and zero mismatches', () => {
    expect(readyToFlip(cap('Shadow', 12, 0))).toBe(true);
    expect(readyToFlip(cap('Shadow', 12, 1))).toBe(false);
    expect(readyToFlip(cap('Shadow', 3, 0))).toBe(false);
    expect(readyToFlip(cap('Legacy', 50, 0))).toBe(false);
  });
});
