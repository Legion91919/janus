import { useCallback, useEffect, useState } from 'react';
import { addRow, formatBody, readyToFlip, servedBy, summarise, type BridgeStatus, type CapabilityStatus, type LogRow, type Mode, type Side } from './bridge';

const MODES: Mode[] = ['Legacy', 'Shadow', 'New'];
const PRESETS = ['/customers', '/customers/10042', '/customers/99999', '/schedules/LN-200310', '/arrears'];

export function App() {
  const [status, setStatus] = useState<BridgeStatus | null>(null);
  const [statusError, setStatusError] = useState(false);
  const [rows, setRows] = useState<LogRow[]>([]);
  const [selected, setSelected] = useState<LogRow | null>(null);
  const [busy, setBusy] = useState(false);
  const [flagError, setFlagError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      const r = await fetch('/bridge/status');
      if (!r.ok) throw new Error(String(r.status));
      setStatus(await r.json());
      setStatusError(false);
    } catch {
      setStatusError(true);
    }
  }, []);

  useEffect(() => { void refresh(); const t = setInterval(refresh, 3000); return () => clearInterval(t); }, [refresh]);

  const send = async (path: string) => {
    setBusy(true);
    const started = performance.now();
    try {
      const r = await fetch(path);
      const body = formatBody(r.headers.get('Content-Type'), await r.text());
      const row: LogRow = { id: Date.now(), at: new Date().toLocaleTimeString('en-GB'), path, status: r.status, side: servedBy(r.headers),
        mode: r.headers.get('X-Bridge-Mode') ?? '-', ms: Math.round(performance.now() - started), body };
      setRows((prev) => addRow(prev, row));
      setSelected(row);
    } catch {
      const row: LogRow = { id: Date.now(), at: new Date().toLocaleTimeString('en-GB'), path, status: 0, side: 'unknown', mode: '-', ms: 0, body: 'The facade did not answer.' };
      setRows((prev) => addRow(prev, row));
      setSelected(row);
    } finally {
      setBusy(false);
      void refresh();
    }
  };

  const setMode = async (capability: string, mode: Mode) => {
    setFlagError(null);
    const r = await fetch(`/bridge/flags/${capability}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ mode }) })
      .catch(() => null);
    if (!r || !r.ok) setFlagError(`Could not change ${capability} (${r ? r.status : 'no answer'}). Runtime flags are Development-only; in production edit appsettings.json.`);
    void refresh();
  };

  const counts = summarise(rows);
  return (
    <>
      <header>
        <span className="mark" aria-hidden="true">J</span>
        <strong>Janus</strong>
        <span>&middot; traffic console</span>
        <span style={{ marginLeft: 'auto' }}><BreakerPill status={status} error={statusError} /></span>
      </header>
      <main>
        {flagError && <p className="err" role="alert">{flagError}</p>}
        <section className="cards" aria-label="Capabilities">
          {status
            ? status.capabilities.map((c) => <CapabilityCard key={c.name} capability={c} onModeChange={(m) => setMode(c.name, m)} />)
            : [0, 1, 2].map((i) => <div className="card" key={i}>{statusError ? <p className="err">Status unavailable</p> : <><div className="sk" /><div className="sk" /></>}</div>)}
        </section>
        <RequestBar busy={busy} onSend={send} />
        <p className="muted" aria-live="polite">Served so far: {counts.legacy} by legacy, {counts.new} by new{counts.unknown ? `, ${counts.unknown} unknown` : ''}.</p>
        <div className="grid2">
          <section className="panel" aria-label="Request log"><RequestLog rows={rows} busy={busy} selected={selected} onSelect={setSelected} /></section>
          <section className="panel" aria-label="Response"><ResponsePreview row={selected} /></section>
        </div>
      </main>
    </>
  );
}

function BreakerPill({ status, error }: { status: BridgeStatus | null; error: boolean }) {
  if (error) return <span className="badge bad">&#9888; status unavailable</span>;
  if (!status) return <span className="badge">checking&hellip;</span>;
  const closed = status.breaker === 'Closed';
  return <span className={`badge ${closed ? 'new' : 'bad'}`}>{closed ? '●' : '○'} Old terminal breaker {status.breaker.toLowerCase()}</span>;
}

function CapabilityCard({ capability, onModeChange }: { capability: CapabilityStatus; onModeChange: (m: Mode) => void }) {
  const { name, mode, migrated, shadow } = capability;
  return (
    <div className="card">
      <h2>{name}</h2>
      <div className="seg" role="group" aria-label={`${name} mode`}>
        {MODES.map((m) => (
          <button key={m} aria-pressed={mode === m} disabled={!migrated && m !== 'Legacy'} onClick={() => onModeChange(m)}>{m}</button>
        ))}
      </div>
      {migrated
        ? <p className="muted">Shadow: {shadow.matched} matched, {shadow.mismatched} mismatched{shadow.lastMismatchFields.length ? ` (${shadow.lastMismatchFields.join(', ')})` : ''}.{readyToFlip(capability) ? ' Ready to flip.' : ''}</p>
        : <p className="muted">Not migrated yet. Forwarded to the old terminal.</p>}
    </div>
  );
}

function RequestBar({ busy, onSend }: { busy: boolean; onSend: (path: string) => void }) {
  const [path, setPath] = useState('/customers/10077');
  return (
    <section className="panel bar" aria-label="Send a request">
      {PRESETS.map((p) => <button key={p} disabled={busy} onClick={() => onSend(p)}>GET {p}</button>)}
      <form className="bar" onSubmit={(e) => { e.preventDefault(); onSend(path); }}>
        <label htmlFor="path" className="muted">Path</label>
        <input id="path" value={path} onChange={(e) => setPath(e.target.value)} />
        <button type="submit" disabled={busy}>{busy ? 'Sending' : 'Send'}</button>
      </form>
    </section>
  );
}

export function ServedByBadge({ side }: { side: Side }) {
  if (side === 'legacy') return <span className="badge legacy">&#9636; LEGACY</span>;
  if (side === 'new') return <span className="badge new">&#9670; NEW</span>;
  return <span className="badge unknown">? UNKNOWN</span>;
}

function RequestLog({ rows, busy, selected, onSelect }: { rows: LogRow[]; busy: boolean; selected: LogRow | null; onSelect: (r: LogRow) => void }) {
  if (rows.length === 0) {
    return busy
      ? <><div className="sk" /><div className="sk" /><p className="muted">Waiting for first response</p></>
      : <p className="muted">No requests yet. Send one above.</p>;
  }
  return (
    <table>
      <thead><tr><th>Time</th><th>Path</th><th>Status</th><th>Served by</th><th>Mode</th><th className="num">ms</th></tr></thead>
      <tbody>
        {rows.map((r) => (
          <tr key={r.id} className={selected?.id === r.id ? 'selected' : ''} onClick={() => onSelect(r)} tabIndex={0}
            onKeyDown={(e) => { if (e.key === 'Enter') onSelect(r); }}>
            <td>{r.at}</td><td>{r.path}</td><td>{r.status >= 500 || r.status === 0 ? `${r.status || '-'} failed` : r.status}</td>
            <td><ServedByBadge side={r.side} /></td><td>{r.mode}</td><td className="num">{r.ms}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function ResponsePreview({ row }: { row: LogRow | null }) {
  return (
    <>
      <h2 style={{ fontSize: 'var(--md)', marginTop: 0 }}>Response</h2>
      {row ? <pre aria-label={`Body of ${row.path}`}>{row.body || '(empty)'}</pre> : <p className="muted">Select a row to see the body</p>}
    </>
  );
}
