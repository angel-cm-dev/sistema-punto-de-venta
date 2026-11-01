import type { Sale } from '../lib/types';
import { formatDateTime, formatMoney, paymentLabels } from '../lib/format';
import { Badge } from './ui';

/** Representación del comprobante (vista previa imprimible de la boleta/factura). */
export default function SaleReceipt({ sale }: { sale: Sale }) {
  return (
    <div className="space-y-4 text-sm">
      <div className="flex flex-wrap items-start justify-between gap-2 rounded-lg bg-slate-50 p-4 dark:bg-slate-800/60">
        <div>
          <p className="text-xs uppercase tracking-wide text-slate-500">
            {sale.documentType === 'Factura' ? 'Factura electrónica' : 'Boleta de venta electrónica'}
          </p>
          <p className="text-lg font-semibold tabular-nums">{sale.documentNumber}</p>
          <p className="text-xs text-slate-500">{formatDateTime(sale.createdAt)}</p>
        </div>
        <Badge tone={sale.status === 'Emitida' ? 'green' : 'red'}>{sale.status}</Badge>
      </div>

      <div className="grid gap-1 sm:grid-cols-2">
        <p>
          <span className="text-slate-500">Cliente: </span>
          {sale.customerName ? `${sale.customerName} (${sale.customerDocNumber})` : 'Cliente varios'}
        </p>
        <p>
          <span className="text-slate-500">Cajero: </span>
          {sale.cashierName}
        </p>
      </div>

      <div className="overflow-x-auto">
        <table className="table">
          <thead>
            <tr>
              <th>Producto</th>
              <th className="text-right">Cant.</th>
              <th className="text-right">P. unit.</th>
              <th className="text-right">Importe</th>
            </tr>
          </thead>
          <tbody>
            {sale.items.map((i) => (
              <tr key={i.productId}>
                <td>{i.productName}</td>
                <td className="text-right tabular-nums">{i.quantity}</td>
                <td className="text-right tabular-nums">{formatMoney(i.unitPrice)}</td>
                <td className="text-right tabular-nums">{formatMoney(i.lineTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <dl className="ml-auto max-w-xs space-y-1 tabular-nums">
        {sale.discount > 0 && (
          <div className="flex justify-between">
            <dt className="text-slate-500">Descuento</dt>
            <dd>−{formatMoney(sale.discount)}</dd>
          </div>
        )}
        <div className="flex justify-between">
          <dt className="text-slate-500">Op. gravada</dt>
          <dd>{formatMoney(sale.subtotal)}</dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-slate-500">IGV (18%)</dt>
          <dd>{formatMoney(sale.igv)}</dd>
        </div>
        <div className="flex justify-between border-t border-slate-200 pt-1 text-base font-semibold dark:border-slate-700">
          <dt>Total</dt>
          <dd>{formatMoney(sale.total)}</dd>
        </div>
        {sale.payments.map((p, idx) => (
          <div key={idx} className="flex justify-between text-xs text-slate-500">
            <dt>
              {paymentLabels[p.method]} {p.reference ? `· ${p.reference}` : ''}
            </dt>
            <dd>{formatMoney(p.amount)}</dd>
          </div>
        ))}
        {sale.change > 0 && (
          <div className="flex justify-between text-xs text-slate-500">
            <dt>Vuelto</dt>
            <dd>{formatMoney(sale.change)}</dd>
          </div>
        )}
      </dl>

      {sale.status === 'Anulada' && (
        <p className="rounded-lg bg-red-50 p-3 text-xs text-red-700 dark:bg-red-950/40 dark:text-red-300">
          Anulada el {formatDateTime(sale.voidedAt)}: {sale.voidReason}
        </p>
      )}
    </div>
  );
}
