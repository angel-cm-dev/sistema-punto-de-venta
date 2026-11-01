import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Minus, Plus, Search, Trash2, UserPlus } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { CashSession, Customer, DocumentType, Paged, PaymentMethod, Product, Sale } from '../lib/types';
import { formatMoney } from '../lib/format';
import { computeChange, computeTotals, isValidDni, isValidRuc, validateSale, type CartLine, type CartPayment } from '../lib/sales';
import { useAsync, useDebounced } from '../hooks/useAsync';
import { useAuth } from '../context/AuthContext';
import { Alert, EmptyState, ErrorState, LoadingState, Modal, PageHeader, Spinner } from '../components/ui';
import SaleReceipt from '../components/SaleReceipt';

const METHODS: PaymentMethod[] = ['Efectivo', 'Tarjeta', 'Yape', 'Plin'];

export default function PosPage() {
  const { isAdmin } = useAuth();
  const session = useAsync(() => api.get<CashSession | undefined>('/api/cash-sessions/current'), []);

  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const products = useAsync(
    () => api.get<Paged<Product>>('/api/products', { search: debounced, pageSize: 24, sortBy: 'name' }),
    [debounced],
  );

  const [lines, setLines] = useState<CartLine[]>([]);
  const [documentType, setDocumentType] = useState<DocumentType>('Boleta');
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [docQuery, setDocQuery] = useState('');
  const [customerMsg, setCustomerMsg] = useState<string | null>(null);
  const [showNewCustomer, setShowNewCustomer] = useState(false);
  const [discount, setDiscount] = useState(0);
  const [payments, setPayments] = useState<CartPayment[]>([{ method: 'Efectivo', amount: 0 }]);
  const [submitting, setSubmitting] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const [receipt, setReceipt] = useState<Sale | null>(null);

  const totals = useMemo(() => computeTotals(lines, discount), [lines, discount]);
  const change = computeChange(totals.total, payments);
  const problems = validateSale({ documentType, customerDocType: customer?.docType, lines, payments, discount, isAdmin });

  const addProduct = (p: Product) => {
    setLines((prev) => {
      const existing = prev.find((l) => l.productId === p.id);
      if (existing) return prev.map((l) => (l.productId === p.id ? { ...l, quantity: l.quantity + 1 } : l));
      return [...prev, { productId: p.id, name: p.name, unitPrice: p.price, quantity: 1, stock: p.stock }];
    });
  };

  const setQuantity = (productId: number, quantity: number) =>
    setLines((prev) =>
      quantity <= 0 ? prev.filter((l) => l.productId !== productId) : prev.map((l) => (l.productId === productId ? { ...l, quantity } : l)),
    );

  const lookupCustomer = async () => {
    setCustomerMsg(null);
    const doc = docQuery.trim();
    if (!isValidDni(doc) && !isValidRuc(doc)) {
      setCustomerMsg('Ingrese un DNI (8 dígitos) o RUC (11 dígitos) válido.');
      return;
    }
    try {
      setCustomer(await api.get<Customer>(`/api/customers/by-document/${doc}`));
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) {
        setCustomerMsg('Cliente no registrado.');
        setShowNewCustomer(true);
      } else setCustomerMsg(e instanceof ApiError ? e.userMessage : 'Error al buscar cliente.');
    }
  };

  const reset = () => {
    setLines([]);
    setCustomer(null);
    setDocQuery('');
    setDiscount(0);
    setPayments([{ method: 'Efectivo', amount: 0 }]);
    setDocumentType('Boleta');
  };

  const checkout = async () => {
    setServerError(null);
    setSubmitting(true);
    try {
      const sale = await api.post<Sale>('/api/sales', {
        documentType,
        customerId: customer?.id ?? null,
        discount,
        items: lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
        payments: payments.filter((p) => p.amount > 0),
      });
      setReceipt(sale);
      reset();
      products.reload();
    } catch (e) {
      setServerError(e instanceof ApiError ? e.userMessage : 'No se pudo registrar la venta.');
    } finally {
      setSubmitting(false);
    }
  };

  if (session.loading) return <LoadingState />;
  if (session.error) return <ErrorState message={session.error} onRetry={session.reload} />;
  if (!session.data)
    return (
      <>
        <PageHeader title="Nueva venta" />
        <div className="card">
          <EmptyState
            title="No tiene una caja abierta"
            description="Toda venta se registra en un turno de caja para poder hacer el arqueo al cierre."
            action={
              <Link to="/caja" className="btn-primary">
                Abrir caja
              </Link>
            }
          />
        </div>
      </>
    );

  return (
    <>
      <PageHeader title="Nueva venta" subtitle={`Caja #${session.data.id} abierta`} />
      <div className="grid gap-6 xl:grid-cols-5">
        {/* ---------- Catálogo ---------- */}
        <section className="card p-4 xl:col-span-3">
          <div className="relative mb-4">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Buscar por nombre o SKU…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Buscar productos"
            />
          </div>
          {products.loading && !products.data && <LoadingState />}
          {products.error && <ErrorState message={products.error} onRetry={products.reload} />}
          {products.data && products.data.items.length === 0 && <EmptyState title="Sin resultados" />}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {products.data?.items.map((p) => (
              <button
                key={p.id}
                onClick={() => addProduct(p)}
                disabled={p.stock === 0}
                className="rounded-lg border border-slate-200 p-3 text-left transition hover:border-brand-500 hover:shadow disabled:opacity-40 dark:border-slate-700"
              >
                <p className="line-clamp-2 text-sm font-medium">{p.name}</p>
                <p className="mt-1 text-xs text-slate-500">{p.sku}</p>
                <div className="mt-2 flex items-center justify-between">
                  <span className="font-semibold tabular-nums">{formatMoney(p.price)}</span>
                  <span className={`text-xs ${p.isLowStock ? 'text-amber-600' : 'text-slate-500'}`}>Stock {p.stock}</span>
                </div>
              </button>
            ))}
          </div>
        </section>

        {/* ---------- Carrito y cobro ---------- */}
        <section className="card space-y-4 p-4 xl:col-span-2">
          <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-label="Tipo de comprobante">
            {(['Boleta', 'Factura'] as DocumentType[]).map((t) => (
              <button
                key={t}
                role="radio"
                aria-checked={documentType === t}
                onClick={() => setDocumentType(t)}
                className={documentType === t ? 'btn-primary' : 'btn-secondary'}
              >
                {t}
              </button>
            ))}
          </div>

          <div>
            <label className="label" htmlFor="doc">
              Cliente (DNI / RUC)
            </label>
            {customer ? (
              <div className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm dark:bg-slate-800">
                <span>
                  {customer.name} <span className="text-slate-500">· {customer.docNumber}</span>
                </span>
                <button className="btn-ghost" onClick={() => setCustomer(null)} aria-label="Quitar cliente">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ) : (
              <div className="flex gap-2">
                <input
                  id="doc"
                  className="input"
                  inputMode="numeric"
                  maxLength={11}
                  value={docQuery}
                  onChange={(e) => setDocQuery(e.target.value.replace(/\D/g, ''))}
                  onKeyDown={(e) => e.key === 'Enter' && lookupCustomer()}
                  placeholder="Opcional en boletas"
                />
                <button className="btn-secondary" onClick={lookupCustomer}>
                  Buscar
                </button>
              </div>
            )}
            {customerMsg && <p className="mt-1 text-xs text-amber-600">{customerMsg}</p>}
          </div>

          <div className="max-h-72 divide-y divide-slate-100 overflow-y-auto dark:divide-slate-800">
            {lines.length === 0 && <p className="py-6 text-center text-sm text-slate-500">Agregue productos del catálogo.</p>}
            {lines.map((l) => (
              <div key={l.productId} className="flex items-center gap-2 py-2">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{l.name}</p>
                  <p className="text-xs text-slate-500">{formatMoney(l.unitPrice)} c/u</p>
                </div>
                <button className="btn-ghost" onClick={() => setQuantity(l.productId, l.quantity - 1)} aria-label="Restar">
                  <Minus className="h-3 w-3" />
                </button>
                <input
                  className="input w-14 px-1 text-center"
                  type="number"
                  min={1}
                  value={l.quantity}
                  onChange={(e) => setQuantity(l.productId, Number(e.target.value))}
                  aria-label={`Cantidad de ${l.name}`}
                />
                <button className="btn-ghost" onClick={() => setQuantity(l.productId, l.quantity + 1)} aria-label="Sumar">
                  <Plus className="h-3 w-3" />
                </button>
                <span className="w-20 text-right text-sm font-medium tabular-nums">{formatMoney(l.unitPrice * l.quantity)}</span>
              </div>
            ))}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label" htmlFor="discount">
                Descuento (S/)
              </label>
              <input
                id="discount"
                className="input"
                type="number"
                min={0}
                step="0.10"
                value={discount || ''}
                onChange={(e) => setDiscount(Number(e.target.value))}
              />
            </div>
            <dl className="space-y-0.5 text-right text-sm tabular-nums">
              <div className="flex justify-between">
                <dt className="text-slate-500">Op. gravada</dt>
                <dd>{formatMoney(totals.subtotal)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-slate-500">IGV 18%</dt>
                <dd>{formatMoney(totals.igv)}</dd>
              </div>
              <div className="flex justify-between text-lg font-semibold">
                <dt>Total</dt>
                <dd>{formatMoney(totals.total)}</dd>
              </div>
            </dl>
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="label mb-0">Pagos</span>
              <button
                className="btn-ghost text-xs"
                onClick={() => setPayments((p) => [...p, { method: 'Yape', amount: change.missing }])}
              >
                <Plus className="h-3 w-3" /> Agregar medio
              </button>
            </div>
            {payments.map((p, idx) => (
              <div key={idx} className="flex gap-2">
                <select
                  className="input w-32"
                  value={p.method}
                  onChange={(e) =>
                    setPayments((all) => all.map((x, i) => (i === idx ? { ...x, method: e.target.value as PaymentMethod } : x)))
                  }
                  aria-label="Medio de pago"
                >
                  {METHODS.map((m) => (
                    <option key={m}>{m}</option>
                  ))}
                </select>
                <input
                  className="input"
                  type="number"
                  min={0}
                  step="0.10"
                  placeholder="Monto"
                  value={p.amount || ''}
                  onChange={(e) =>
                    setPayments((all) => all.map((x, i) => (i === idx ? { ...x, amount: Number(e.target.value) } : x)))
                  }
                  aria-label="Monto"
                />
                {p.method !== 'Efectivo' && (
                  <input
                    className="input"
                    placeholder="Nº operación"
                    value={p.reference ?? ''}
                    onChange={(e) =>
                      setPayments((all) => all.map((x, i) => (i === idx ? { ...x, reference: e.target.value } : x)))
                    }
                    aria-label="Referencia"
                  />
                )}
                {payments.length > 1 && (
                  <button className="btn-ghost" onClick={() => setPayments((all) => all.filter((_, i) => i !== idx))} aria-label="Quitar pago">
                    <Trash2 className="h-4 w-4" />
                  </button>
                )}
              </div>
            ))}
            <div className="flex justify-between text-sm">
              <button
                className="text-xs text-brand-700 underline dark:text-brand-100"
                onClick={() => setPayments([{ method: 'Efectivo', amount: totals.total }])}
              >
                Pago exacto
              </button>
              <span className="tabular-nums">
                {change.missing > 0 ? (
                  <span className="text-amber-600">Falta {formatMoney(change.missing)}</span>
                ) : (
                  <span>Vuelto {formatMoney(change.change)}</span>
                )}
              </span>
            </div>
          </div>

          {serverError && <Alert>{serverError}</Alert>}
          {lines.length > 0 && problems.length > 0 && (
            <ul className="list-inside list-disc text-xs text-amber-700 dark:text-amber-300">
              {problems.map((p) => (
                <li key={p}>{p}</li>
              ))}
            </ul>
          )}
          <button className="btn-primary w-full py-3 text-base" disabled={problems.length > 0 || submitting} onClick={checkout}>
            {submitting && <Spinner className="h-4 w-4" />} Cobrar {formatMoney(totals.total)}
          </button>
        </section>
      </div>

      <Modal open={!!receipt} title="Venta registrada" onClose={() => setReceipt(null)} wide>
        {receipt && <SaleReceipt sale={receipt} />}
        <div className="mt-6 flex justify-end gap-2">
          <button className="btn-secondary" onClick={() => window.print()}>
            Imprimir
          </button>
          <button className="btn-primary" onClick={() => setReceipt(null)}>
            Nueva venta
          </button>
        </div>
      </Modal>

      <NewCustomerModal
        open={showNewCustomer}
        docNumber={docQuery}
        onClose={() => setShowNewCustomer(false)}
        onCreated={(c) => {
          setCustomer(c);
          setCustomerMsg(null);
          setShowNewCustomer(false);
        }}
      />
    </>
  );
}

function NewCustomerModal({
  open,
  docNumber,
  onClose,
  onCreated,
}: {
  open: boolean;
  docNumber: string;
  onClose: () => void;
  onCreated: (c: Customer) => void;
}) {
  const [name, setName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const docType = docNumber.length === 11 ? 'Ruc' : 'Dni';

  const save = async () => {
    if (!name.trim()) {
      setError('Ingrese el nombre o razón social.');
      return;
    }
    setSaving(true);
    try {
      onCreated(await api.post<Customer>('/api/customers', { docType, docNumber, name: name.trim() }));
      setName('');
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.userMessage : 'No se pudo registrar.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title="Registrar cliente" onClose={onClose}>
      <div className="space-y-3">
        <p className="text-sm text-slate-500">
          {docType}: <span className="font-medium text-slate-700 dark:text-slate-200">{docNumber}</span>
        </p>
        <input
          className="input"
          placeholder={docType === 'Ruc' ? 'Razón social' : 'Nombres y apellidos'}
          value={name}
          onChange={(e) => setName(e.target.value)}
          autoFocus
        />
        {error && <Alert>{error}</Alert>}
        <div className="flex justify-end gap-2">
          <button className="btn-secondary" onClick={onClose}>
            Cancelar
          </button>
          <button className="btn-primary" onClick={save} disabled={saving}>
            <UserPlus className="h-4 w-4" /> Registrar
          </button>
        </div>
      </div>
    </Modal>
  );
}
