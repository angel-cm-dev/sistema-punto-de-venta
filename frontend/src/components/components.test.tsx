import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { Pagination } from './ui';
import SaleReceipt from './SaleReceipt';
import LoginPage from '../pages/LoginPage';
import { AuthProvider } from '../context/AuthContext';
import type { Sale } from '../lib/types';

describe('Pagination', () => {
  it('deshabilita "anterior" en la primera página y avanza', async () => {
    const onChange = vi.fn();
    render(<Pagination page={1} totalPages={3} totalItems={25} onChange={onChange} />);
    expect(screen.getByLabelText('Página anterior')).toBeDisabled();
    expect(screen.getByText('25 registros')).toBeInTheDocument();
    await userEvent.click(screen.getByLabelText('Página siguiente'));
    expect(onChange).toHaveBeenCalledWith(2);
  });
});

describe('SaleReceipt', () => {
  const sale: Sale = {
    id: 1, documentType: 'Factura', documentNumber: 'F001-00000012', status: 'Anulada', createdAt: '2026-11-01T15:00:00Z',
    customerId: 1, customerName: 'Empresa Demo', customerDocNumber: '20601234565', cashierId: 2, cashierName: 'Cajera',
    cashSessionId: 1, grossAmount: 118, discount: 0, subtotal: 100, igv: 18, total: 118, amountPaid: 120, change: 2,
    items: [{ productId: 1, productName: 'Arroz', quantity: 2, unitPrice: 59, lineTotal: 118 }],
    payments: [{ method: 'Efectivo', amount: 120, reference: null }],
    voidedAt: '2026-11-01T16:00:00Z', voidReason: 'Error de digitación',
  };

  it('muestra serie-correlativo, IGV, vuelto y motivo de anulación', () => {
    render(<SaleReceipt sale={sale} />);
    expect(screen.getByText('F001-00000012')).toBeInTheDocument();
    expect(screen.getByText('Factura electrónica')).toBeInTheDocument();
    expect(screen.getByText('IGV (18%)')).toBeInTheDocument();
    expect(screen.getByText('Vuelto')).toBeInTheDocument();
    expect(screen.getByText(/Error de digitación/)).toBeInTheDocument();
  });
});

describe('LoginPage', () => {
  it('valida campos antes de llamar a la API', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    render(
      <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <AuthProvider>
          <LoginPage />
        </AuthProvider>
      </MemoryRouter>,
    );
    await userEvent.click(await screen.findByRole('button', { name: /ingresar/i }));
    expect(screen.getByText('Ingrese un correo válido.')).toBeInTheDocument();
    expect(screen.getByText('Ingrese su contraseña.')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it('muestra el error del servidor con credenciales inválidas', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ title: 'Credenciales inválidas', detail: 'Correo o contraseña incorrectos.' }), { status: 401 }),
      ),
    );
    render(
      <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <AuthProvider>
          <LoginPage />
        </AuthProvider>
      </MemoryRouter>,
    );
    await userEvent.type(screen.getByLabelText('Correo'), 'admin@demo.pe');
    await userEvent.type(screen.getByLabelText('Contraseña'), 'mala');
    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Correo o contraseña incorrectos.');
    vi.unstubAllGlobals();
  });
});
