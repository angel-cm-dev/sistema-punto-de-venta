import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import {
  BarChart3,
  Boxes,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  Receipt,
  ShoppingCart,
  Sun,
  Tags,
  UserCog,
  Users,
  Wallet,
  X,
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../hooks/useTheme';

const links = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, adminOnly: false },
  { to: '/pos', label: 'Nueva venta', icon: ShoppingCart, adminOnly: false },
  { to: '/ventas', label: 'Ventas', icon: Receipt, adminOnly: false },
  { to: '/caja', label: 'Caja', icon: Wallet, adminOnly: false },
  { to: '/productos', label: 'Productos', icon: Boxes, adminOnly: false },
  { to: '/categorias', label: 'Categorías', icon: Tags, adminOnly: true },
  { to: '/clientes', label: 'Clientes', icon: Users, adminOnly: false },
  { to: '/reportes', label: 'Reportes', icon: BarChart3, adminOnly: true },
  { to: '/usuarios', label: 'Usuarios', icon: UserCog, adminOnly: true },
];

export default function Layout() {
  const { user, isAdmin, logout } = useAuth();
  const { dark, toggle } = useTheme();
  const [open, setOpen] = useState(false);

  const sidebar = (
    <nav className="flex h-full flex-col">
      <div className="flex items-center gap-2 px-5 py-5">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-600 text-white">
          <ShoppingCart className="h-5 w-5" />
        </div>
        <div>
          <p className="font-semibold leading-tight">Punto de Venta</p>
          <p className="text-xs text-slate-500">Tienda Demo</p>
        </div>
      </div>
      <ul className="flex-1 space-y-1 px-3">
        {links
          .filter((l) => isAdmin || !l.adminOnly)
          .map(({ to, label, icon: Icon }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={to === '/'}
                onClick={() => setOpen(false)}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition ${
                    isActive
                      ? 'bg-brand-50 text-brand-700 dark:bg-brand-900/40 dark:text-brand-100'
                      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
                  }`
                }
              >
                <Icon className="h-4 w-4" /> {label}
              </NavLink>
            </li>
          ))}
      </ul>
      <div className="border-t border-slate-200 p-4 dark:border-slate-800">
        <p className="truncate text-sm font-medium">{user?.fullName}</p>
        <p className="truncate text-xs text-slate-500">
          {user?.email} · {user?.role}
        </p>
        <div className="mt-3 flex gap-2">
          <button className="btn-secondary flex-1 px-2" onClick={toggle} aria-label="Cambiar tema">
            {dark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
          </button>
          <button className="btn-secondary flex-1 px-2" onClick={logout} aria-label="Cerrar sesión">
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </div>
    </nav>
  );

  return (
    <div className="min-h-screen lg:pl-64">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 border-r border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900 lg:block">
        {sidebar}
      </aside>

      {open && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-slate-900/50" onClick={() => setOpen(false)} />
          <aside className="absolute inset-y-0 left-0 w-64 bg-white shadow-xl dark:bg-slate-900">
            <button className="btn-ghost absolute right-2 top-4" onClick={() => setOpen(false)} aria-label="Cerrar menú">
              <X className="h-5 w-5" />
            </button>
            {sidebar}
          </aside>
        </div>
      )}

      <header className="sticky top-0 z-20 flex items-center gap-3 border-b border-slate-200 bg-white/80 px-4 py-3 backdrop-blur dark:border-slate-800 dark:bg-slate-900/80 lg:hidden">
        <button className="btn-ghost" onClick={() => setOpen(true)} aria-label="Abrir menú">
          <Menu className="h-5 w-5" />
        </button>
        <span className="font-semibold">Punto de Venta</span>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <Outlet />
      </main>
    </div>
  );
}
