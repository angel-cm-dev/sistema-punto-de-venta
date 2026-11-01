import { useState } from 'react';
import { Lock, Unlock } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { CashSession, CashSessionListItem, Paged } from '../lib/types';
import { formatDateTime, formatMoney, paymentLabels } from '../lib/format';
import { useAsync } from '../hooks/useAsync';
import { useAuth } from '../context/AuthContext';
import { Alert, Badge, EmptyState, ErrorState, Field, LoadingState, Modal, PageHeader, Pagination, Spinner } from '../components/ui';

function DifferenceBadge({ value }: { value: number | null }) {
  if (value === null) return <span className="text-slate-400">—</span>;
  if (value === 0) return <Badge tone="green">Cuadrada</Badge>;
  return <Badge tone={value > 0 ? 'sky' : 'red'}>{value > 0 ? `Sobra ${formatMoney(value)}` : `Falta ${formatMoney(-value)}`}</Badge>;
}

export default function CashPage() {
  const { isAdmin } = useAuth();
  const current = useAsync(() => api.get<CashSession | undefined>('/api/cash-sessions/current'), []);
  const [page, setPage] = useState(1);
  const history = useAsync(
    () => api.get<Paged<CashSessionListItem>>('/api/cash-sessions', { page, pageSize: 10, sortBy: 'openedAt', sortDir: 'desc' }),
    [page],
  );

  const [amount, setAmount] = useState('100');
  const [counted, setCounted] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [closed, setClosed] = useState<CashSession | null>(null);

  const refresh = () => {
    current.reload();
    history.reload();
  };

  const openCash = async () => {
    const value = Number(amount);
    if (Number.isNaN(value) || value < 0) {
      setError('El fondo de caja debe ser un monto mayor o igual a cero.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await api.post('/api/cash-sessions/open', { openingAmount: value });
      refresh();
    } catch (e) {
      setError(e instanceof ApiError ? e.userMessage : 'No se pudo abrir la caja.');
    } finally {
      setBusy(false);
    }
  };

  const closeCash = async () => {
    if (!current.data) return;
    const value = Number(counted);
    if (counted === '' || Number.isNaN(value) || value < 0) {
      setError('Ingrese el efectivo contado en caja.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      setClosed(await api.post<CashSession>(`/api/cash-sessions/${current.data.id}/close`, { countedCash: value, notes }));
      setCounted('');
      setNotes('');
      refresh();
    } catch (e) {
      setError(e instanceof ApiError ? e.userMessage : 'No se pudo cerrar la caja.');
    } finally {
      setBusy(false);
    }
  };

  const session = current.data;
  return (
    <>
      <PageHeader title="Caja" subtitle="Apertura, cierre y arqueo del turno" />
      {current.loading && !session && <LoadingState />}
      {current.error && <ErrorState message={current.error} onRetry={current.reload} />}

      {!current.loading && !current.error && (
        <div className="grid gap-6 lg:grid-cols-3">
          {!session ? (
            <section className="card space-y-4 p-6">
              <h2 className="flex items-center gap-2 font-semibold">
                <Unlock className="h-4 w-4" /> Abrir caja
              </h2>
              <Field label="Fondo inicial (S/)" htmlFor="opening">
                <input id="opening" className="input" type="number" min={0} step="0.10" value={amount} onChange={(e) => setAmount(e.target.value)} />
              </Field>
              {error && <Alert>{error}</Alert>}
              <button className="btn-primary w-full" onClick={openCash} disabled={busy}>
                {busy && <Spinner className="h-4 w-4" />} Abrir turno
              </button>
            </section>
          ) : (
            <>
              <section className="card space-y-3 p-6 lg:col-span-2">
                <div className="flex items-center justify-between">
                  <h2 className="font-semibold">Turno actual #{session.id}</h2>
                  <Badge tone="green">Abierta</Badge>
                </div>
                <p className="text-sm text-slate-500">Abierta el {formatDateTime(session.openedAt)}</p>
                <dl className="grid grid-cols-2 gap-4 pt-2 sm:grid-cols-4">
                  <div>
                    <dt className="text-xs text-slate-500">Fondo inicial</dt>
                    <dd className="font-semibold tabular-nums">{formatMoney(session.openingAmount)}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-slate-500">Ventas ({session.salesCount})</dt>
                    <dd className="font-semibold tabular-nums">{formatMoney(session.salesTotal)}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-slate-500">Anuladas</dt>
                    <dd className="font-semibold">{session.voidedCount}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-slate-500">Efectivo esperado</dt>
                    <dd className="font-semibold tabular-nums text-brand-700 dark:text-brand-100">{formatMoney(session.expectedCash)}</dd>
                  </div>
                </dl>
                <div className="grid grid-cols-2 gap-2 pt-2 sm:grid-cols-4">
                  {session.paymentsByMethod.map((p) => (
                    <div key={p.method} className="rounded-lg bg-slate-50 p-3 dark:bg-slate-800/60">
                      <p className="text-xs text-slate-500">{paymentLabels[p.method]}</p>
                      <p className="font-medium tabular-nums">{formatMoney(p.amount)}</p>
                    </div>
                  ))}
                </div>
              </section>
              <section className="card space-y-4 p-6">
                <h2 className="flex items-center gap-2 font-semibold">
                  <Lock className="h-4 w-4" /> Cerrar caja (arqueo)
                </h2>
                <Field label="Efectivo contado (S/)" htmlFor="counted">
                  <input id="counted" className="input" type="number" min={0} step="0.10" value={counted} onChange={(e) => setCounted(e.target.value)} />
                </Field>
                {counted !== '' && !Number.isNaN(Number(counted)) && (
                  <p className="text-sm">
                    Diferencia: <DifferenceBadge value={Math.round((Number(counted) - session.expectedCash) * 100) / 100} />
                  </p>
                )}
                <Field label="Observaciones" htmlFor="notes">
                  <textarea id="notes" className="input" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} />
                </Field>
                {error && <Alert>{error}</Alert>}
                <button className="btn-primary w-full" onClick={closeCash} disabled={busy}>
                  {busy && <Spinner className="h-4 w-4" />} Cerrar turno
                </button>
              </section>
            </>
          )}
        </div>
      )}

      <section className="card mt-6">
        <h2 className="border-b border-slate-200 px-4 py-3 font-semibold dark:border-slate-800">
          Historial de turnos {isAdmin ? '(todos los cajeros)' : ''}
        </h2>
        {history.loading && !history.data && <LoadingState />}
        {history.error && <ErrorState message={history.error} onRetry={history.reload} />}
        {history.data && history.data.items.length === 0 && <EmptyState title="Aún no hay turnos registrados" />}
        {history.data && history.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Cajero</th>
                    <th>Apertura</th>
                    <th className="hidden md:table-cell">Cierre</th>
                    <th className="text-right">Esperado</th>
                    <th className="text-right">Contado</th>
                    <th>Arqueo</th>
                  </tr>
                </thead>
                <tbody>
                  {history.data.items.map((s) => (
                    <tr key={s.id}>
                      <td>{s.id}</td>
                      <td>{s.cashierName}</td>
                      <td className="whitespace-nowrap">{formatDateTime(s.openedAt)}</td>
                      <td className="hidden whitespace-nowrap md:table-cell">{s.closedAt ? formatDateTime(s.closedAt) : <Badge tone="green">Abierta</Badge>}</td>
                      <td className="text-right tabular-nums">{s.expectedCash !== null ? formatMoney(s.expectedCash) : '—'}</td>
                      <td className="text-right tabular-nums">{s.countedCash !== null ? formatMoney(s.countedCash) : '—'}</td>
                      <td>
                        <DifferenceBadge value={s.difference} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} totalPages={history.data.totalPages} totalItems={history.data.totalItems} onChange={setPage} />
          </>
        )}
      </section>

      <Modal open={!!closed} title="Arqueo de caja" onClose={() => setClosed(null)}>
        {closed && (
          <dl className="space-y-2 text-sm tabular-nums">
            <div className="flex justify-between"><dt>Fondo inicial</dt><dd>{formatMoney(closed.openingAmount)}</dd></div>
            {closed.paymentsByMethod.map((p) => (
              <div key={p.method} className="flex justify-between text-slate-500"><dt>{paymentLabels[p.method]}</dt><dd>{formatMoney(p.amount)}</dd></div>
            ))}
            <div className="flex justify-between font-medium"><dt>Efectivo esperado</dt><dd>{formatMoney(closed.expectedCash)}</dd></div>
            <div className="flex justify-between font-medium"><dt>Efectivo contado</dt><dd>{formatMoney(closed.countedCash)}</dd></div>
            <div className="flex justify-between border-t border-slate-200 pt-2 dark:border-slate-700"><dt>Resultado</dt><dd><DifferenceBadge value={closed.difference} /></dd></div>
          </dl>
        )}
      </Modal>
    </>
  );
}
