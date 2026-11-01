import { useState } from 'react';
import { Search } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { Paged, Sale, SaleListItem } from '../lib/types';
import { addDays, formatDateTime, formatMoney, todayLima } from '../lib/format';
import { useAsync, useDebounced } from '../hooks/useAsync';
import { useAuth } from '../context/AuthContext';
import { Alert, Badge, EmptyState, ErrorState, LoadingState, Modal, PageHeader, Pagination, Spinner } from '../components/ui';
import SaleReceipt from '../components/SaleReceipt';

export default function SalesPage() {
  const { isAdmin } = useAuth();
  const [from, setFrom] = useState(addDays(todayLima(), -7));
  const [to, setTo] = useState(todayLima());
  const [status, setStatus] = useState('');
  const [documentType, setDocumentType] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const debounced = useDebounced(search);

  const sales = useAsync(
    () =>
      api.get<Paged<SaleListItem>>('/api/sales', {
        from, to, status, documentType, search: debounced, page, pageSize: 15, sortBy: 'createdAt', sortDir: 'desc',
      }),
    [from, to, status, documentType, debounced, page],
  );

  const [selected, setSelected] = useState<Sale | null>(null);
  const [voidReason, setVoidReason] = useState('');
  const [voidError, setVoidError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const open = async (id: number) => {
    setVoidReason('');
    setVoidError(null);
    setSelected(await api.get<Sale>(`/api/sales/${id}`));
  };

  const voidSale = async () => {
    if (!selected) return;
    if (voidReason.trim().length < 5) {
      setVoidError('Indique un motivo de al menos 5 caracteres.');
      return;
    }
    setBusy(true);
    try {
      setSelected(await api.post<Sale>(`/api/sales/${selected.id}/void`, { reason: voidReason.trim() }));
      sales.reload();
    } catch (e) {
      setVoidError(e instanceof ApiError ? e.userMessage : 'No se pudo anular.');
    } finally {
      setBusy(false);
    }
  };

  const onFilter = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setPage(1);
  };

  return (
    <>
      <PageHeader title="Ventas" subtitle={isAdmin ? 'Comprobantes de todos los cajeros' : 'Tus comprobantes emitidos'} />
      <div className="card">
        <div className="grid gap-3 border-b border-slate-200 p-4 dark:border-slate-800 sm:grid-cols-2 lg:grid-cols-5">
          <div className="relative lg:col-span-1">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Serie, cliente o documento"
              value={search}
              onChange={(e) => onFilter(setSearch)(e.target.value)}
              aria-label="Buscar ventas"
            />
          </div>
          <input className="input" type="date" value={from} onChange={(e) => onFilter(setFrom)(e.target.value)} aria-label="Desde" />
          <input className="input" type="date" value={to} onChange={(e) => onFilter(setTo)(e.target.value)} aria-label="Hasta" />
          <select className="input" value={documentType} onChange={(e) => onFilter(setDocumentType)(e.target.value)} aria-label="Tipo">
            <option value="">Boletas y facturas</option>
            <option value="Boleta">Boletas</option>
            <option value="Factura">Facturas</option>
          </select>
          <select className="input" value={status} onChange={(e) => onFilter(setStatus)(e.target.value)} aria-label="Estado">
            <option value="">Todos los estados</option>
            <option value="Emitida">Emitidas</option>
            <option value="Anulada">Anuladas</option>
          </select>
        </div>

        {sales.loading && !sales.data && <LoadingState />}
        {sales.error && <ErrorState message={sales.error} onRetry={sales.reload} />}
        {sales.data && sales.data.items.length === 0 && (
          <EmptyState title="No hay ventas en este rango" description="Pruebe ampliando las fechas o quitando filtros." />
        )}
        {sales.data && sales.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Comprobante</th>
                    <th>Fecha</th>
                    <th className="hidden md:table-cell">Cliente</th>
                    {isAdmin && <th className="hidden lg:table-cell">Cajero</th>}
                    <th>Estado</th>
                    <th className="text-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {sales.data.items.map((s) => (
                    <tr key={s.id} className="cursor-pointer" onClick={() => open(s.id)}>
                      <td className="font-medium tabular-nums">{s.documentNumber}</td>
                      <td className="whitespace-nowrap text-slate-500">{formatDateTime(s.createdAt)}</td>
                      <td className="hidden md:table-cell">{s.customerName ?? 'Cliente varios'}</td>
                      {isAdmin && <td className="hidden lg:table-cell">{s.cashierName}</td>}
                      <td>
                        <Badge tone={s.status === 'Emitida' ? 'green' : 'red'}>{s.status}</Badge>
                      </td>
                      <td className="text-right font-medium tabular-nums">{formatMoney(s.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} totalPages={sales.data.totalPages} totalItems={sales.data.totalItems} onChange={setPage} />
          </>
        )}
      </div>

      <Modal open={!!selected} title="Detalle del comprobante" onClose={() => setSelected(null)} wide>
        {selected && (
          <>
            <SaleReceipt sale={selected} />
            {isAdmin && selected.status === 'Emitida' && (
              <div className="mt-6 space-y-2 border-t border-slate-200 pt-4 dark:border-slate-800">
                <label className="label" htmlFor="reason">
                  Anular comprobante (repone el stock)
                </label>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <input
                    id="reason"
                    className="input"
                    placeholder="Motivo de la anulación"
                    value={voidReason}
                    onChange={(e) => setVoidReason(e.target.value)}
                  />
                  <button className="btn-danger" onClick={voidSale} disabled={busy}>
                    {busy && <Spinner className="h-4 w-4" />} Anular
                  </button>
                </div>
                {voidError && <Alert>{voidError}</Alert>}
              </div>
            )}
          </>
        )}
      </Modal>
    </>
  );
}
