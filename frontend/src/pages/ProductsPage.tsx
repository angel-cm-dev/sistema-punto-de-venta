import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PackagePlus, Pencil, Search, SlidersHorizontal } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { Category, Paged, Product } from '../lib/types';
import { formatMoney } from '../lib/format';
import { hasErrors, validateProduct, type Errors, type ProductForm } from '../lib/validators';
import { useAsync, useDebounced } from '../hooks/useAsync';
import { useAuth } from '../context/AuthContext';
import { Alert, Badge, EmptyState, ErrorState, Field, LoadingState, Modal, PageHeader, Pagination, Spinner } from '../components/ui';
import SortHeader, { type SortState } from '../components/SortHeader';

const emptyForm: ProductForm = { sku: '', name: '', categoryId: '', price: '', stock: '0', minStock: '5' };

export default function ProductsPage() {
  const { isAdmin } = useAuth();
  const [params] = useSearchParams();
  const [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [lowStock, setLowStock] = useState(params.get('lowStock') === 'true');
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<SortState>({ sortBy: 'name', sortDir: 'asc' });
  const debounced = useDebounced(search);

  const categories = useAsync(() => api.get<Category[]>('/api/categories'), []);
  const products = useAsync(
    () =>
      api.get<Paged<Product>>('/api/products', {
        search: debounced, categoryId, lowStock: lowStock || undefined, includeInactive: isAdmin || undefined,
        page, pageSize: 12, ...sort,
      }),
    [debounced, categoryId, lowStock, page, sort, isAdmin],
  );

  const [editing, setEditing] = useState<Product | 'new' | null>(null);
  const [adjusting, setAdjusting] = useState<Product | null>(null);

  return (
    <>
      <PageHeader
        title="Productos"
        subtitle="Catálogo, precios (incluyen IGV) y stock"
        actions={
          isAdmin && (
            <button className="btn-primary" onClick={() => setEditing('new')}>
              <PackagePlus className="h-4 w-4" /> Nuevo producto
            </button>
          )
        }
      />
      <div className="card">
        <div className="grid gap-3 border-b border-slate-200 p-4 dark:border-slate-800 sm:grid-cols-3">
          <div className="relative">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Nombre o SKU"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              aria-label="Buscar productos"
            />
          </div>
          <select
            className="input"
            value={categoryId}
            onChange={(e) => {
              setCategoryId(e.target.value);
              setPage(1);
            }}
            aria-label="Categoría"
          >
            <option value="">Todas las categorías</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="h-4 w-4 accent-brand-600"
              checked={lowStock}
              onChange={(e) => {
                setLowStock(e.target.checked);
                setPage(1);
              }}
            />
            Solo stock bajo
          </label>
        </div>

        {products.loading && !products.data && <LoadingState />}
        {products.error && <ErrorState message={products.error} onRetry={products.reload} />}
        {products.data && products.data.items.length === 0 && <EmptyState title="No se encontraron productos" />}
        {products.data && products.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <SortHeader label="SKU" field="sku" sort={sort} onSort={setSort} />
                    <SortHeader label="Producto" field="name" sort={sort} onSort={setSort} />
                    <th className="hidden md:table-cell">Categoría</th>
                    <SortHeader label="Precio" field="price" sort={sort} onSort={setSort} className="text-right" />
                    <SortHeader label="Stock" field="stock" sort={sort} onSort={setSort} className="text-right" />
                    {isAdmin && <th className="text-right">Acciones</th>}
                  </tr>
                </thead>
                <tbody>
                  {products.data.items.map((p) => (
                    <tr key={p.id} className={p.isActive ? '' : 'opacity-50'}>
                      <td className="font-mono text-xs">{p.sku}</td>
                      <td>
                        {p.name} {!p.isActive && <Badge>Inactivo</Badge>}
                      </td>
                      <td className="hidden md:table-cell">{p.categoryName}</td>
                      <td className="text-right tabular-nums">{formatMoney(p.price)}</td>
                      <td className="text-right">
                        <Badge tone={p.stock === 0 ? 'red' : p.isLowStock ? 'amber' : 'green'}>{p.stock}</Badge>
                      </td>
                      {isAdmin && (
                        <td className="whitespace-nowrap text-right">
                          <button className="btn-ghost" onClick={() => setAdjusting(p)} aria-label={`Ajustar stock de ${p.name}`}>
                            <SlidersHorizontal className="h-4 w-4" />
                          </button>
                          <button className="btn-ghost" onClick={() => setEditing(p)} aria-label={`Editar ${p.name}`}>
                            <Pencil className="h-4 w-4" />
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} totalPages={products.data.totalPages} totalItems={products.data.totalItems} onChange={setPage} />
          </>
        )}
      </div>

      {editing && (
        <ProductModal
          product={editing === 'new' ? null : editing}
          categories={categories.data ?? []}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            products.reload();
          }}
        />
      )}
      {adjusting && (
        <AdjustStockModal
          product={adjusting}
          onClose={() => setAdjusting(null)}
          onSaved={() => {
            setAdjusting(null);
            products.reload();
          }}
        />
      )}
    </>
  );
}

function ProductModal({
  product,
  categories,
  onClose,
  onSaved,
}: {
  product: Product | null;
  categories: Category[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = product === null;
  const [form, setForm] = useState<ProductForm>(
    product
      ? { sku: product.sku, name: product.name, categoryId: String(product.categoryId), price: String(product.price), stock: String(product.stock), minStock: String(product.minStock) }
      : emptyForm,
  );
  const [active, setActive] = useState(product?.isActive ?? true);
  const [errors, setErrors] = useState<Errors<ProductForm>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: keyof ProductForm) => (e: { target: { value: string } }) => setForm({ ...form, [k]: e.target.value });

  const save = async () => {
    const v = validateProduct(form, isNew);
    setErrors(v);
    if (hasErrors(v)) return;
    setSaving(true);
    setServerError(null);
    try {
      const body = { name: form.name.trim(), categoryId: Number(form.categoryId), price: Number(form.price), minStock: Number(form.minStock) };
      if (isNew) await api.post('/api/products', { ...body, sku: form.sku.trim(), stock: Number(form.stock) });
      else await api.put(`/api/products/${product.id}`, { ...body, isActive: active });
      onSaved();
    } catch (e) {
      if (e instanceof ApiError) {
        // Los errores por campo del backend se muestran junto a cada input.
        setErrors(Object.fromEntries(Object.entries(e.fieldErrors).map(([k, v]) => [k, v[0]])) as Errors<ProductForm>);
        setServerError(e.userMessage);
      } else setServerError('No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title={isNew ? 'Nuevo producto' : `Editar ${product.name}`} onClose={onClose}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="SKU" htmlFor="sku" error={errors.sku}>
          <input id="sku" className={`input ${errors.sku ? 'input-error' : ''}`} value={form.sku} onChange={set('sku')} disabled={!isNew} />
        </Field>
        <Field label="Categoría" htmlFor="cat" error={errors.categoryId}>
          <select id="cat" className={`input ${errors.categoryId ? 'input-error' : ''}`} value={form.categoryId} onChange={set('categoryId')}>
            <option value="">Seleccione…</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </Field>
        <div className="sm:col-span-2">
          <Field label="Nombre" htmlFor="name" error={errors.name}>
            <input id="name" className={`input ${errors.name ? 'input-error' : ''}`} value={form.name} onChange={set('name')} />
          </Field>
        </div>
        <Field label="Precio con IGV (S/)" htmlFor="price" error={errors.price}>
          <input id="price" className={`input ${errors.price ? 'input-error' : ''}`} inputMode="decimal" value={form.price} onChange={set('price')} />
        </Field>
        <Field label="Stock mínimo" htmlFor="min" error={errors.minStock}>
          <input id="min" className={`input ${errors.minStock ? 'input-error' : ''}`} inputMode="numeric" value={form.minStock} onChange={set('minStock')} />
        </Field>
        {isNew ? (
          <Field label="Stock inicial" htmlFor="stock" error={errors.stock}>
            <input id="stock" className={`input ${errors.stock ? 'input-error' : ''}`} inputMode="numeric" value={form.stock} onChange={set('stock')} />
          </Field>
        ) : (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="h-4 w-4 accent-brand-600" checked={active} onChange={(e) => setActive(e.target.checked)} /> Activo
            (visible en ventas)
          </label>
        )}
      </div>
      {serverError && <div className="mt-4"><Alert>{serverError}</Alert></div>}
      <div className="mt-6 flex justify-end gap-2">
        <button className="btn-secondary" onClick={onClose}>
          Cancelar
        </button>
        <button className="btn-primary" onClick={save} disabled={saving}>
          {saving && <Spinner className="h-4 w-4" />} Guardar
        </button>
      </div>
    </Modal>
  );
}

function AdjustStockModal({ product, onClose, onSaved }: { product: Product; onClose: () => void; onSaved: () => void }) {
  const [delta, setDelta] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    const d = Number(delta);
    if (!Number.isInteger(d) || d === 0) return setError('Ingrese un entero distinto de 0 (negativo para mermas).');
    if (product.stock + d < 0) return setError('El ajuste dejaría el stock en negativo.');
    if (!reason.trim()) return setError('Indique el motivo del ajuste.');
    setSaving(true);
    try {
      await api.post(`/api/products/${product.id}/stock-adjustments`, { delta: d, reason: reason.trim() });
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.userMessage : 'No se pudo ajustar.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title={`Ajustar stock · ${product.name}`} onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-slate-500">
          Stock actual: <strong>{product.stock}</strong>
          {delta && !Number.isNaN(Number(delta)) && <> → nuevo: <strong>{product.stock + Number(delta)}</strong></>}
        </p>
        <Field label="Cantidad (+ ingreso / − merma)" htmlFor="delta">
          <input id="delta" className="input" type="number" value={delta} onChange={(e) => setDelta(e.target.value)} />
        </Field>
        <Field label="Motivo" htmlFor="reason">
          <input id="reason" className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ej. conteo físico, merma, compra" />
        </Field>
        {error && <Alert>{error}</Alert>}
        <div className="flex justify-end gap-2">
          <button className="btn-secondary" onClick={onClose}>
            Cancelar
          </button>
          <button className="btn-primary" onClick={save} disabled={saving}>
            {saving && <Spinner className="h-4 w-4" />} Aplicar ajuste
          </button>
        </div>
      </div>
    </Modal>
  );
}
