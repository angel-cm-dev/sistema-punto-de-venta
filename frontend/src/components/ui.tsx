import { useEffect, type ReactNode } from 'react';
import { AlertTriangle, ChevronLeft, ChevronRight, Inbox, Loader2, X } from 'lucide-react';

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{subtitle}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

export function Spinner({ className = 'h-5 w-5' }: { className?: string }) {
  return <Loader2 className={`animate-spin ${className}`} aria-hidden />;
}

export function LoadingState({ label = 'Cargando…' }: { label?: string }) {
  return (
    <div role="status" className="flex items-center justify-center gap-3 py-16 text-slate-500">
      <Spinner /> <span>{label}</span>
    </div>
  );
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 py-16 text-center">
      <Inbox className="h-10 w-10 text-slate-300 dark:text-slate-600" aria-hidden />
      <p className="font-medium">{title}</p>
      {description && <p className="max-w-sm text-sm text-slate-500">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center justify-center gap-3 py-12 text-center">
      <AlertTriangle className="h-10 w-10 text-amber-500" aria-hidden />
      <p className="max-w-md text-sm text-slate-600 dark:text-slate-300">{message}</p>
      {onRetry && (
        <button className="btn-secondary" onClick={onRetry}>
          Reintentar
        </button>
      )}
    </div>
  );
}

export function Alert({ children, tone = 'error' }: { children: ReactNode; tone?: 'error' | 'success' | 'info' }) {
  const styles = {
    error: 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/50 dark:text-red-300',
    success: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-300',
    info: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950/50 dark:text-sky-300',
  }[tone];
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`rounded-lg border px-3 py-2 text-sm ${styles}`}>
      {children}
    </div>
  );
}

const badgeTones = {
  green: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-300',
  red: 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300',
  amber: 'bg-amber-100 text-amber-800 dark:bg-amber-900/50 dark:text-amber-300',
  slate: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
  sky: 'bg-sky-100 text-sky-700 dark:bg-sky-900/50 dark:text-sky-300',
};

export function Badge({ children, tone = 'slate' }: { children: ReactNode; tone?: keyof typeof badgeTones }) {
  return <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${badgeTones[tone]}`}>{children}</span>;
}

export function Field({
  label,
  error,
  children,
  htmlFor,
}: {
  label: string;
  error?: string;
  children: ReactNode;
  htmlFor?: string;
}) {
  return (
    <div>
      <label className="label" htmlFor={htmlFor}>
        {label}
      </label>
      {children}
      {error && <p className="mt-1 text-xs text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}

export function Modal({
  open,
  title,
  onClose,
  children,
  wide = false,
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  wide?: boolean;
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/50 p-4 sm:items-center">
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`card w-full ${wide ? 'max-w-3xl' : 'max-w-lg'} my-8 p-6`}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button className="btn-ghost" onClick={onClose} aria-label="Cerrar">
            <X className="h-5 w-5" />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function Pagination({
  page,
  totalPages,
  totalItems,
  onChange,
}: {
  page: number;
  totalPages: number;
  totalItems: number;
  onChange: (page: number) => void;
}) {
  return (
    <div className="flex items-center justify-between gap-2 px-4 py-3 text-sm text-slate-500">
      <span>
        {totalItems} registro{totalItems === 1 ? '' : 's'}
      </span>
      <div className="flex items-center gap-2">
        <button
          className="btn-ghost"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
          aria-label="Página anterior"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span>
          Página {Math.min(page, Math.max(totalPages, 1))} de {Math.max(totalPages, 1)}
        </span>
        <button
          className="btn-ghost"
          disabled={page >= totalPages}
          onClick={() => onChange(page + 1)}
          aria-label="Página siguiente"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}

export function StatCard({
  label,
  value,
  hint,
  icon,
  tone = 'brand',
}: {
  label: string;
  value: string;
  hint?: string;
  icon: ReactNode;
  tone?: 'brand' | 'amber' | 'sky' | 'violet';
}) {
  const tones = {
    brand: 'bg-brand-50 text-brand-700 dark:bg-brand-900/40 dark:text-brand-100',
    amber: 'bg-amber-50 text-amber-700 dark:bg-amber-900/40 dark:text-amber-200',
    sky: 'bg-sky-50 text-sky-700 dark:bg-sky-900/40 dark:text-sky-200',
    violet: 'bg-violet-50 text-violet-700 dark:bg-violet-900/40 dark:text-violet-200',
  }[tone];
  return (
    <div className="card flex items-start gap-4 p-5">
      <div className={`rounded-lg p-2.5 ${tones}`}>{icon}</div>
      <div className="min-w-0">
        <p className="text-sm text-slate-500 dark:text-slate-400">{label}</p>
        <p className="mt-1 truncate text-2xl font-semibold tabular-nums">{value}</p>
        {hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
      </div>
    </div>
  );
}

/** Gráfico de barras simple en HTML/CSS (sin dependencias de charting). */
export function BarChart({ data }: { data: { label: string; value: number; display: string }[] }) {
  const max = Math.max(...data.map((d) => d.value), 1);
  return (
    <div className="flex h-56 items-end gap-2 sm:gap-4" role="img" aria-label="Ventas por día">
      {data.map((d) => (
        <div key={d.label} className="flex flex-1 flex-col items-center gap-2">
          <span className="text-[10px] font-medium tabular-nums text-slate-500 sm:text-xs">{d.display}</span>
          <div className="flex w-full flex-1 items-end">
            <div
              className="w-full rounded-t-md bg-brand-500/80 transition-all hover:bg-brand-600"
              style={{ height: `${Math.max((d.value / max) * 100, d.value > 0 ? 3 : 0)}%` }}
              title={`${d.label}: ${d.display}`}
            />
          </div>
          <span className="text-xs text-slate-500">{d.label}</span>
        </div>
      ))}
    </div>
  );
}
