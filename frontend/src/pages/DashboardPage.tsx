import { Link } from 'react-router-dom';
import { AlertTriangle, CalendarRange, Receipt, TrendingUp, Wallet } from 'lucide-react';
import { api } from '../lib/api';
import type { Dashboard } from '../lib/types';
import { formatMoney, formatShortDate } from '../lib/format';
import { useAsync } from '../hooks/useAsync';
import { useAuth } from '../context/AuthContext';
import { BarChart, EmptyState, ErrorState, LoadingState, PageHeader, StatCard } from '../components/ui';

export default function DashboardPage() {
  const { user, isAdmin } = useAuth();
  const { data, loading, error, reload } = useAsync(() => api.get<Dashboard>('/api/reports/dashboard'), []);

  return (
    <>
      <PageHeader
        title={`Hola, ${user?.fullName.split(' ')[0] ?? ''}`}
        subtitle={isAdmin ? 'Resumen de toda la tienda' : 'Resumen de tus ventas'}
        actions={
          <Link to="/pos" className="btn-primary">
            Nueva venta
          </Link>
        }
      />
      {loading && !data && <LoadingState />}
      {error && <ErrorState message={error} onRetry={reload} />}
      {data && (
        <div className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatCard
              label="Ventas de hoy"
              value={formatMoney(data.salesToday)}
              hint={`${data.ticketsToday} comprobantes`}
              icon={<TrendingUp className="h-5 w-5" />}
            />
            <StatCard
              label="Ticket promedio"
              value={formatMoney(data.averageTicketToday)}
              icon={<Receipt className="h-5 w-5" />}
              tone="sky"
            />
            <StatCard
              label="Ventas del mes"
              value={formatMoney(data.salesMonth)}
              icon={<CalendarRange className="h-5 w-5" />}
              tone="violet"
            />
            <StatCard
              label="Stock bajo"
              value={String(data.lowStockProducts)}
              hint={`${data.openCashSessions} caja(s) abierta(s)`}
              icon={<AlertTriangle className="h-5 w-5" />}
              tone="amber"
            />
          </div>

          <div className="grid gap-6 lg:grid-cols-3">
            <section className="card p-5 lg:col-span-2">
              <h2 className="mb-4 font-semibold">Ventas de los últimos 7 días</h2>
              <BarChart
                data={data.last7Days.map((d) => ({
                  label: formatShortDate(d.date),
                  value: d.total,
                  display: d.total > 0 ? formatMoney(d.total).replace('S/ ', '') : '0',
                }))}
              />
            </section>

            <section className="card p-5">
              <h2 className="mb-4 font-semibold">Más vendidos (30 días)</h2>
              {data.topProducts.length === 0 ? (
                <EmptyState title="Sin ventas aún" />
              ) : (
                <ol className="space-y-3">
                  {data.topProducts.map((p, i) => (
                    <li key={p.productId} className="flex items-center gap-3">
                      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100 text-xs font-semibold dark:bg-slate-800">
                        {i + 1}
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">{p.productName}</p>
                        <p className="text-xs text-slate-500">{p.quantity} unidades</p>
                      </div>
                      <span className="text-sm font-semibold tabular-nums">{formatMoney(p.amount)}</span>
                    </li>
                  ))}
                </ol>
              )}
            </section>
          </div>

          {data.lowStockProducts > 0 && (
            <Link
              to="/productos?lowStock=true"
              className="card flex items-center gap-3 border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200"
            >
              <Wallet className="h-4 w-4" /> Hay {data.lowStockProducts} producto(s) en o bajo el stock mínimo. Ver
              detalle →
            </Link>
          )}
        </div>
      )}
    </>
  );
}
