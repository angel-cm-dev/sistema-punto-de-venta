import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ShoppingCart } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { ApiError } from '../lib/api';
import { Alert, Field, Spinner } from '../components/ui';

export default function LoginPage() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (user) return <Navigate to="/" replace />;

  const validate = () => {
    const next: typeof errors = {};
    if (!/^\S+@\S+\.\S+$/.test(email)) next.email = 'Ingrese un correo válido.';
    if (!password) next.password = 'Ingrese su contraseña.';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    if (!validate()) return;
    setSubmitting(true);
    try {
      await login(email.trim(), password);
      const from = (location.state as { from?: string } | null)?.from ?? '/';
      navigate(from, { replace: true });
    } catch (err) {
      setServerError(err instanceof ApiError ? err.userMessage : 'No se pudo iniciar sesión.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-brand-50 to-slate-100 px-4 dark:from-slate-950 dark:to-slate-900">
      <div className="card w-full max-w-sm p-8">
        <div className="mb-6 flex flex-col items-center text-center">
          <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-brand-600 text-white">
            <ShoppingCart className="h-6 w-6" />
          </div>
          <h1 className="text-xl font-semibold">Punto de Venta</h1>
          <p className="text-sm text-slate-500">Inicie sesión para continuar</p>
        </div>

        <form onSubmit={onSubmit} noValidate className="space-y-4">
          {serverError && <Alert>{serverError}</Alert>}
          <Field label="Correo" htmlFor="email" error={errors.email}>
            <input
              id="email"
              type="email"
              autoComplete="username"
              className={`input ${errors.email ? 'input-error' : ''}`}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </Field>
          <Field label="Contraseña" htmlFor="password" error={errors.password}>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              className={`input ${errors.password ? 'input-error' : ''}`}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </Field>
          <button type="submit" className="btn-primary w-full" disabled={submitting}>
            {submitting && <Spinner className="h-4 w-4" />} Ingresar
          </button>
        </form>

        <div className="mt-6 rounded-lg bg-slate-50 p-3 text-xs text-slate-500 dark:bg-slate-800/60">
          <p className="font-medium text-slate-600 dark:text-slate-300">Cuentas de demostración</p>
          <p>admin@demo.pe · cajero@demo.pe — contraseña Demo123!</p>
        </div>
      </div>
    </div>
  );
}
