import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import Layout from './components/Layout';
import { LoadingState } from './components/ui';
import { useAuth } from './context/AuthContext';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import PosPage from './pages/PosPage';
import SalesPage from './pages/SalesPage';
import CashPage from './pages/CashPage';
import ProductsPage from './pages/ProductsPage';
import CategoriesPage from './pages/CategoriesPage';
import CustomersPage from './pages/CustomersPage';
import ReportsPage from './pages/ReportsPage';
import UsersPage from './pages/UsersPage';

function RequireAuth({ children, adminOnly = false }: { children: ReactNode; adminOnly?: boolean }) {
  const { user, loading, isAdmin } = useAuth();
  const location = useLocation();
  if (loading) return <LoadingState label="Verificando sesión…" />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  // La UI oculta lo que el rol no puede usar, pero la protección real está en la API.
  if (adminOnly && !isAdmin) return <Navigate to="/" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="pos" element={<PosPage />} />
        <Route path="ventas" element={<SalesPage />} />
        <Route path="caja" element={<CashPage />} />
        <Route path="productos" element={<ProductsPage />} />
        <Route path="clientes" element={<CustomersPage />} />
        <Route path="categorias" element={<RequireAuth adminOnly><CategoriesPage /></RequireAuth>} />
        <Route path="reportes" element={<RequireAuth adminOnly><ReportsPage /></RequireAuth>} />
        <Route path="usuarios" element={<RequireAuth adminOnly><UsersPage /></RequireAuth>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
