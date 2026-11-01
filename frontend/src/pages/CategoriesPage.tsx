import { useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { api, ApiError } from '../lib/api';
import type { Category } from '../lib/types';
import { useAsync } from '../hooks/useAsync';
import { Alert, EmptyState, ErrorState, Field, LoadingState, Modal, PageHeader, Spinner } from '../components/ui';

export default function CategoriesPage() {
  const categories = useAsync(() => api.get<Category[]>('/api/categories'), []);
  const [editing, setEditing] = useState<Category | 'new' | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const openEditor = (c: Category | 'new') => {
    setEditing(c);
    setName(c === 'new' ? '' : c.name);
    setDescription(c === 'new' ? '' : (c.description ?? ''));
    setError(null);
  };

  const save = async () => {
    if (!name.trim()) return setError('El nombre es obligatorio.');
    setSaving(true);
    try {
      const body = { name: name.trim(), description: description.trim() || null };
      if (editing === 'new') await api.post('/api/categories', body);
      else if (editing) await api.put(`/api/categories/${editing.id}`, body);
      setEditing(null);
      categories.reload();
    } catch (e) {
      setError(e instanceof ApiError ? e.userMessage : 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (c: Category) => {
    if (!window.confirm(`¿Eliminar la categoría "${c.name}"?`)) return;
    setListError(null);
    try {
      await api.delete(`/api/categories/${c.id}`);
      categories.reload();
    } catch (e) {
      setListError(e instanceof ApiError ? e.userMessage : 'No se pudo eliminar.');
    }
  };

  return (
    <>
      <PageHeader
        title="Categorías"
        actions={
          <button className="btn-primary" onClick={() => openEditor('new')}>
            <Plus className="h-4 w-4" /> Nueva categoría
          </button>
        }
      />
      {listError && <div className="mb-4"><Alert>{listError}</Alert></div>}
      <div className="card">
        {categories.loading && !categories.data && <LoadingState />}
        {categories.error && <ErrorState message={categories.error} onRetry={categories.reload} />}
        {categories.data?.length === 0 && <EmptyState title="Sin categorías" />}
        {categories.data && categories.data.length > 0 && (
          <div className="overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th className="hidden sm:table-cell">Descripción</th>
                  <th className="text-right">Productos</th>
                  <th className="text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {categories.data.map((c) => (
                  <tr key={c.id}>
                    <td className="font-medium">{c.name}</td>
                    <td className="hidden text-slate-500 sm:table-cell">{c.description ?? '—'}</td>
                    <td className="text-right tabular-nums">{c.productCount}</td>
                    <td className="whitespace-nowrap text-right">
                      <button className="btn-ghost" onClick={() => openEditor(c)} aria-label={`Editar ${c.name}`}>
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button className="btn-ghost" onClick={() => remove(c)} aria-label={`Eliminar ${c.name}`}>
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <Modal open={editing !== null} title={editing === 'new' ? 'Nueva categoría' : 'Editar categoría'} onClose={() => setEditing(null)}>
        <div className="space-y-4">
          <Field label="Nombre" htmlFor="cname">
            <input id="cname" className="input" value={name} onChange={(e) => setName(e.target.value)} maxLength={80} />
          </Field>
          <Field label="Descripción" htmlFor="cdesc">
            <input id="cdesc" className="input" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={250} />
          </Field>
          {error && <Alert>{error}</Alert>}
          <div className="flex justify-end gap-2">
            <button className="btn-secondary" onClick={() => setEditing(null)}>
              Cancelar
            </button>
            <button className="btn-primary" onClick={save} disabled={saving}>
              {saving && <Spinner className="h-4 w-4" />} Guardar
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}
