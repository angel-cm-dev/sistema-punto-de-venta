import { useState } from 'react';
import { Pencil, Search, UserPlus } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { Customer, Paged } from '../lib/types';
import { formatDateTime } from '../lib/format';
import { hasErrors, validateCustomer, type CustomerForm, type Errors } from '../lib/validators';
import { useAsync, useDebounced } from '../hooks/useAsync';
import { Alert, Badge, EmptyState, ErrorState, Field, LoadingState, Modal, PageHeader, Pagination, Spinner } from '../components/ui';
import SortHeader, { type SortState } from '../components/SortHeader';

const empty: CustomerForm = { docType: 'Dni', docNumber: '', name: '', email: '', phone: '', address: '' };

export default function CustomersPage() {
  const [search, setSearch] = useState('');
  const [docType, setDocType] = useState('');
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<SortState>({ sortBy: 'name', sortDir: 'asc' });
  const debounced = useDebounced(search);
  const customers = useAsync(
    () => api.get<Paged<Customer>>('/api/customers', { search: debounced, docType, page, pageSize: 12, ...sort }),
    [debounced, docType, page, sort],
  );
  const [editing, setEditing] = useState<Customer | 'new' | null>(null);

  return (
    <>
      <PageHeader
        title="Clientes"
        subtitle="Personas (DNI) y empresas (RUC)"
        actions={
          <button className="btn-primary" onClick={() => setEditing('new')}>
            <UserPlus className="h-4 w-4" /> Nuevo cliente
          </button>
        }
      />
      <div className="card">
        <div className="grid gap-3 border-b border-slate-200 p-4 dark:border-slate-800 sm:grid-cols-3">
          <div className="relative sm:col-span-2">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Nombre o número de documento"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              aria-label="Buscar clientes"
            />
          </div>
          <select
            className="input"
            value={docType}
            onChange={(e) => {
              setDocType(e.target.value);
              setPage(1);
            }}
            aria-label="Tipo de documento"
          >
            <option value="">DNI y RUC</option>
            <option value="Dni">Solo DNI</option>
            <option value="Ruc">Solo RUC</option>
          </select>
        </div>
        {customers.loading && !customers.data && <LoadingState />}
        {customers.error && <ErrorState message={customers.error} onRetry={customers.reload} />}
        {customers.data && customers.data.items.length === 0 && <EmptyState title="No se encontraron clientes" />}
        {customers.data && customers.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <SortHeader label="Documento" field="docNumber" sort={sort} onSort={setSort} />
                    <SortHeader label="Nombre / razón social" field="name" sort={sort} onSort={setSort} />
                    <th className="hidden md:table-cell">Contacto</th>
                    <SortHeader label="Registro" field="createdAt" sort={sort} onSort={setSort} className="hidden lg:table-cell" />
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {customers.data.items.map((c) => (
                    <tr key={c.id}>
                      <td className="whitespace-nowrap">
                        <Badge tone={c.docType === 'Ruc' ? 'sky' : 'slate'}>{c.docType.toUpperCase()}</Badge>{' '}
                        <span className="tabular-nums">{c.docNumber}</span>
                      </td>
                      <td className="font-medium">{c.name}</td>
                      <td className="hidden text-slate-500 md:table-cell">{[c.email, c.phone].filter(Boolean).join(' · ') || '—'}</td>
                      <td className="hidden text-slate-500 lg:table-cell">{formatDateTime(c.createdAt)}</td>
                      <td className="text-right">
                        <button className="btn-ghost" onClick={() => setEditing(c)} aria-label={`Editar ${c.name}`}>
                          <Pencil className="h-4 w-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} totalPages={customers.data.totalPages} totalItems={customers.data.totalItems} onChange={setPage} />
          </>
        )}
      </div>
      {editing && (
        <CustomerModal
          customer={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            customers.reload();
          }}
        />
      )}
    </>
  );
}

function CustomerModal({ customer, onClose, onSaved }: { customer: Customer | null; onClose: () => void; onSaved: () => void }) {
  const isNew = customer === null;
  const [form, setForm] = useState<CustomerForm>(
    customer
      ? { docType: customer.docType, docNumber: customer.docNumber, name: customer.name, email: customer.email ?? '', phone: customer.phone ?? '', address: customer.address ?? '' }
      : empty,
  );
  const [errors, setErrors] = useState<Errors<CustomerForm>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: keyof CustomerForm) => (e: { target: { value: string } }) => setForm({ ...form, [k]: e.target.value });

  const save = async () => {
    const v = validateCustomer(form, isNew);
    setErrors(v);
    if (hasErrors(v)) return;
    setSaving(true);
    setServerError(null);
    const body = { name: form.name.trim(), email: form.email || null, phone: form.phone || null, address: form.address || null };
    try {
      if (isNew) await api.post('/api/customers', { ...body, docType: form.docType, docNumber: form.docNumber });
      else await api.put(`/api/customers/${customer.id}`, body);
      onSaved();
    } catch (e) {
      setServerError(e instanceof ApiError ? e.userMessage : 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title={isNew ? 'Nuevo cliente' : 'Editar cliente'} onClose={onClose}>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Tipo" htmlFor="dtype">
          <select id="dtype" className="input" value={form.docType} onChange={set('docType')} disabled={!isNew}>
            <option value="Dni">DNI</option>
            <option value="Ruc">RUC</option>
          </select>
        </Field>
        <div className="sm:col-span-2">
          <Field label="Número" htmlFor="dnum" error={errors.docNumber}>
            <input
              id="dnum"
              className={`input ${errors.docNumber ? 'input-error' : ''}`}
              inputMode="numeric"
              maxLength={form.docType === 'Ruc' ? 11 : 8}
              value={form.docNumber}
              onChange={(e) => setForm({ ...form, docNumber: e.target.value.replace(/\D/g, '') })}
              disabled={!isNew}
            />
          </Field>
        </div>
        <div className="sm:col-span-3">
          <Field label={form.docType === 'Ruc' ? 'Razón social' : 'Nombres y apellidos'} htmlFor="cname" error={errors.name}>
            <input id="cname" className={`input ${errors.name ? 'input-error' : ''}`} value={form.name} onChange={set('name')} />
          </Field>
        </div>
        <Field label="Correo" htmlFor="cemail" error={errors.email}>
          <input id="cemail" className={`input ${errors.email ? 'input-error' : ''}`} value={form.email} onChange={set('email')} />
        </Field>
        <Field label="Teléfono" htmlFor="cphone">
          <input id="cphone" className="input" value={form.phone} onChange={set('phone')} maxLength={20} />
        </Field>
        <Field label="Dirección" htmlFor="caddr">
          <input id="caddr" className="input" value={form.address} onChange={set('address')} maxLength={250} />
        </Field>
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
