import { useState } from 'react';
import { Pencil, Search, UserPlus } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { Paged, User } from '../lib/types';
import { formatDateTime } from '../lib/format';
import { hasErrors, validateUser, type Errors, type UserForm } from '../lib/validators';
import { useAsync, useDebounced } from '../hooks/useAsync';
import { Alert, Badge, EmptyState, ErrorState, Field, LoadingState, Modal, PageHeader, Pagination, Spinner } from '../components/ui';

export default function UsersPage() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const debounced = useDebounced(search);
  const users = useAsync(() => api.get<Paged<User>>('/api/users', { search: debounced, page, pageSize: 10 }), [debounced, page]);
  const [editing, setEditing] = useState<User | 'new' | null>(null);

  return (
    <>
      <PageHeader
        title="Usuarios"
        subtitle="Administradores y cajeros"
        actions={
          <button className="btn-primary" onClick={() => setEditing('new')}>
            <UserPlus className="h-4 w-4" /> Nuevo usuario
          </button>
        }
      />
      <div className="card">
        <div className="border-b border-slate-200 p-4 dark:border-slate-800">
          <div className="relative max-w-sm">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Nombre o correo"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              aria-label="Buscar usuarios"
            />
          </div>
        </div>
        {users.loading && !users.data && <LoadingState />}
        {users.error && <ErrorState message={users.error} onRetry={users.reload} />}
        {users.data && users.data.items.length === 0 && <EmptyState title="Sin usuarios" />}
        {users.data && users.data.items.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr><th>Nombre</th><th>Correo</th><th>Rol</th><th>Estado</th><th className="hidden md:table-cell">Alta</th><th /></tr>
                </thead>
                <tbody>
                  {users.data.items.map((u) => (
                    <tr key={u.id}>
                      <td className="font-medium">{u.fullName}</td>
                      <td>{u.email}</td>
                      <td><Badge tone={u.role === 'Admin' ? 'sky' : 'slate'}>{u.role}</Badge></td>
                      <td><Badge tone={u.isActive ? 'green' : 'red'}>{u.isActive ? 'Activo' : 'Inactivo'}</Badge></td>
                      <td className="hidden text-slate-500 md:table-cell">{formatDateTime(u.createdAt)}</td>
                      <td className="text-right">
                        <button className="btn-ghost" onClick={() => setEditing(u)} aria-label={`Editar ${u.fullName}`}>
                          <Pencil className="h-4 w-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination page={page} totalPages={users.data.totalPages} totalItems={users.data.totalItems} onChange={setPage} />
          </>
        )}
      </div>
      {editing && (
        <UserModal
          user={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            users.reload();
          }}
        />
      )}
    </>
  );
}

function UserModal({ user, onClose, onSaved }: { user: User | null; onClose: () => void; onSaved: () => void }) {
  const isNew = user === null;
  const [form, setForm] = useState<UserForm>({ email: user?.email ?? '', fullName: user?.fullName ?? '', password: '', role: user?.role ?? 'Cajero' });
  const [active, setActive] = useState(user?.isActive ?? true);
  const [errors, setErrors] = useState<Errors<UserForm>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: keyof UserForm) => (e: { target: { value: string } }) => setForm({ ...form, [k]: e.target.value });

  const save = async () => {
    const v = validateUser(form, isNew);
    setErrors(v);
    if (hasErrors(v)) return;
    setSaving(true);
    setServerError(null);
    try {
      if (isNew) await api.post('/api/users', form);
      else {
        await api.put(`/api/users/${user.id}`, { fullName: form.fullName, role: form.role, isActive: active });
        if (form.password) await api.put(`/api/users/${user.id}/password`, { newPassword: form.password });
      }
      onSaved();
    } catch (e) {
      setServerError(e instanceof ApiError ? e.userMessage : 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open title={isNew ? 'Nuevo usuario' : 'Editar usuario'} onClose={onClose}>
      <div className="space-y-4">
        <Field label="Correo" htmlFor="uemail" error={errors.email}>
          <input id="uemail" className={`input ${errors.email ? 'input-error' : ''}`} value={form.email} onChange={set('email')} disabled={!isNew} />
        </Field>
        <Field label="Nombre completo" htmlFor="uname" error={errors.fullName}>
          <input id="uname" className={`input ${errors.fullName ? 'input-error' : ''}`} value={form.fullName} onChange={set('fullName')} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Rol" htmlFor="urole">
            <select id="urole" className="input" value={form.role} onChange={set('role')}>
              <option value="Cajero">Cajero</option>
              <option value="Admin">Admin</option>
            </select>
          </Field>
          <Field label={isNew ? 'Contraseña' : 'Nueva contraseña (opcional)'} htmlFor="upass" error={errors.password}>
            <input id="upass" type="password" autoComplete="new-password" className={`input ${errors.password ? 'input-error' : ''}`} value={form.password} onChange={set('password')} />
          </Field>
        </div>
        {!isNew && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="h-4 w-4 accent-brand-600" checked={active} onChange={(e) => setActive(e.target.checked)} /> Usuario activo
          </label>
        )}
        {serverError && <Alert>{serverError}</Alert>}
        <div className="flex justify-end gap-2">
          <button className="btn-secondary" onClick={onClose}>Cancelar</button>
          <button className="btn-primary" onClick={save} disabled={saving}>
            {saving && <Spinner className="h-4 w-4" />} Guardar
          </button>
        </div>
      </div>
    </Modal>
  );
}
