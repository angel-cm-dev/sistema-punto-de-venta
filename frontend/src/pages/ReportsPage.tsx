import { useState } from 'react';
import { api } from '../lib/api';
import type { CashierSales, DailySales, ProductSales } from '../lib/types';
import { addDays, formatMoney, formatShortDate, todayLima } from '../lib/format';
import { useAsync } from '../hooks/useAsync';
import { BarChart, EmptyState, ErrorState, LoadingState, PageHeader } from '../components/ui';

type Tab = 'dia' | 'producto' | 'cajero';

export default function ReportsPage() {
  const [from, setFrom] = useState(addDays(todayLima(), -13));
  const [to, setTo] = useState(todayLima());
  const [tab, setTab] = useState<Tab>('dia');

  const report = useAsync(async () => {
    const range = { from, to };
    if (tab === 'dia') return { tab, rows: await api.get<DailySales[]>('/api/reports/sales-by-day', range) } as const;
    if (tab === 'producto')
      return { tab, rows: await api.get<ProductSales[]>('/api/reports/sales-by-product', { ...range, top: 20 }) } as const;
    return { tab, rows: await api.get<CashierSales[]>('/api/reports/sales-by-cashier', range) } as const;
  }, [from, to, tab]);

  const tabs: { id: Tab; label: string }[] = [
    { id: 'dia', label: 'Por día' },
    { id: 'producto', label: 'Por producto' },
    { id: 'cajero', label: 'Por cajero' },
  ];

  return (
    <>
      <PageHeader title="Reportes de ventas" subtitle="Solo comprobantes emitidos (los anulados no suman)" />
      <div className="card mb-6 flex flex-col gap-3 p-4 sm:flex-row sm:items-end">
        <div>
          <label className="label" htmlFor="from">Desde</label>
          <input id="from" className="input" type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="to">Hasta</label>
          <input id="to" className="input" type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
        </div>
        <div className="flex gap-1 rounded-lg bg-slate-100 p-1 dark:bg-slate-800 sm:ml-auto" role="tablist">
          {tabs.map((t) => (
            <button
              key={t.id}
              role="tab"
              aria-selected={tab === t.id}
              onClick={() => setTab(t.id)}
              className={`rounded-md px-3 py-1.5 text-sm font-medium ${tab === t.id ? 'bg-white shadow dark:bg-slate-700' : 'text-slate-500'}`}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      <div className="card p-4">
        {report.loading && <LoadingState />}
        {report.error && <ErrorState message={report.error} onRetry={report.reload} />}
        {!report.loading && report.data && report.data.rows.length === 0 && <EmptyState title="Sin ventas en el rango elegido" />}

        {!report.loading && report.data?.tab === 'dia' && report.data.rows.length > 0 && (
          <>
            <BarChart data={report.data.rows.map((d) => ({ label: formatShortDate(d.date), value: d.total, display: String(Math.round(d.total)) }))} />
            <div className="mt-6 overflow-x-auto">
              <table className="table">
                <thead>
                  <tr><th>Fecha</th><th className="text-right">Tickets</th><th className="text-right">Op. gravada</th><th className="text-right">IGV</th><th className="text-right">Total</th></tr>
                </thead>
                <tbody>
                  {report.data.rows.map((d) => (
                    <tr key={d.date}>
                      <td>{d.date}</td>
                      <td className="text-right tabular-nums">{d.tickets}</td>
                      <td className="text-right tabular-nums">{formatMoney(d.subtotal)}</td>
                      <td className="text-right tabular-nums">{formatMoney(d.igv)}</td>
                      <td className="text-right font-medium tabular-nums">{formatMoney(d.total)}</td>
                    </tr>
                  ))}
                  <tr className="font-semibold">
                    <td>Total</td>
                    <td className="text-right tabular-nums">{report.data.rows.reduce((a, d) => a + d.tickets, 0)}</td>
                    <td className="text-right tabular-nums">{formatMoney(report.data.rows.reduce((a, d) => a + d.subtotal, 0))}</td>
                    <td className="text-right tabular-nums">{formatMoney(report.data.rows.reduce((a, d) => a + d.igv, 0))}</td>
                    <td className="text-right tabular-nums">{formatMoney(report.data.rows.reduce((a, d) => a + d.total, 0))}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </>
        )}

        {!report.loading && report.data?.tab === 'producto' && report.data.rows.length > 0 && (
          <div className="overflow-x-auto">
            <table className="table">
              <thead><tr><th>#</th><th>Producto</th><th className="text-right">Unidades</th><th className="text-right">Importe</th></tr></thead>
              <tbody>
                {report.data.rows.map((p, i) => (
                  <tr key={p.productId}>
                    <td>{i + 1}</td>
                    <td>{p.productName}</td>
                    <td className="text-right tabular-nums">{p.quantity}</td>
                    <td className="text-right font-medium tabular-nums">{formatMoney(p.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!report.loading && report.data?.tab === 'cajero' && report.data.rows.length > 0 && (
          <div className="overflow-x-auto">
            <table className="table">
              <thead><tr><th>Cajero</th><th className="text-right">Tickets</th><th className="text-right">Ticket promedio</th><th className="text-right">Total</th></tr></thead>
              <tbody>
                {report.data.rows.map((c) => (
                  <tr key={c.cashierId}>
                    <td>{c.cashierName}</td>
                    <td className="text-right tabular-nums">{c.tickets}</td>
                    <td className="text-right tabular-nums">{formatMoney(c.averageTicket)}</td>
                    <td className="text-right font-medium tabular-nums">{formatMoney(c.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
