// Pure logic for the traffic console: kept free of React so it is easy to test.

export type Side = 'legacy' | 'new' | 'unknown';
export type Mode = 'Legacy' | 'Shadow' | 'New';

export interface ShadowCounts { matched: number; mismatched: number; lastMismatchFields: string[] }
export interface CapabilityStatus { name: string; mode: Mode; migrated: boolean; shadow: ShadowCounts }
export interface BridgeStatus { breaker: 'Closed' | 'Open' | 'HalfOpen'; capabilities: CapabilityStatus[] }

export interface LogRow {
  id: number;
  at: string;
  path: string;
  status: number;
  side: Side;
  mode: string;
  ms: number;
  body: string;
}

/** Reads which side served a response from the facade's X-Served-By header. */
export function servedBy(headers: Pick<Headers, 'get'>): Side {
  const value = headers.get('X-Served-By')?.trim().toLowerCase();
  return value === 'legacy' || value === 'new' ? value : 'unknown';
}

/** Pretty-prints JSON bodies; leaves the old terminal's fixed-width text untouched. */
export function formatBody(contentType: string | null, text: string): string {
  if (contentType?.includes('json')) {
    try { return JSON.stringify(JSON.parse(text), null, 2); } catch { return text; }
  }
  return text;
}

/** Newest first, capped so the log stays readable. */
export function addRow(rows: LogRow[], row: LogRow, max = 50): LogRow[] {
  return [row, ...rows].slice(0, max);
}

/** How many requests each side served: the number that proves traffic moved after a flip. */
export function summarise(rows: LogRow[]): Record<Side, number> {
  return rows.reduce<Record<Side, number>>((acc, r) => ({ ...acc, [r.side]: acc[r.side] + 1 }),
    { legacy: 0, new: 0, unknown: 0 });
}

/** Shadow is ready to flip to New when it has compared enough requests and found no difference. */
export function readyToFlip(c: CapabilityStatus, minimumMatches = 10): boolean {
  return c.mode === 'Shadow' && c.shadow.mismatched === 0 && c.shadow.matched >= minimumMatches;
}
