import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError, buildQuery, tokenStorage, UNAUTHORIZED_EVENT } from './api';
import { formatMoney, addDays } from './format';
import { validateProduct } from './validators';

const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

afterEach(() => vi.unstubAllGlobals());

describe('cliente API', () => {
  it('envía el token Bearer guardado', async () => {
    tokenStorage.set('abc');
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, { ok: true }));
    vi.stubGlobal('fetch', fetchMock);

    await api.get('/api/products', { page: 2, search: '' });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/products?page=2');
    expect(init.headers.Authorization).toBe('Bearer abc');
  });

  it('convierte un ProblemDetails en ApiError con errores por campo', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        jsonResponse(400, { title: 'Datos de entrada inválidos', errors: { price: ['Debe ser mayor a 0'] } }),
      ),
    );
    const error = (await api.post('/api/products', {}).catch((e) => e)) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.fieldErrors.price).toEqual(['Debe ser mayor a 0']);
    expect(error.userMessage).toBe('Debe ser mayor a 0');
  });

  it('incluye el código de regla de negocio (422)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(422, { title: 'Regla', detail: 'Stock insuficiente', code: 'stock_insuficiente' })));
    const error = (await api.post('/api/sales', {}).catch((e) => e)) as ApiError;
    expect(error.code).toBe('stock_insuficiente');
    expect(error.userMessage).toBe('Stock insuficiente');
  });

  it('ante 401 borra el token y notifica a la app', async () => {
    tokenStorage.set('expirado');
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
    const listener = vi.fn();
    window.addEventListener(UNAUTHORIZED_EVENT, listener);

    await expect(api.get('/api/auth/me')).rejects.toBeInstanceOf(ApiError);
    expect(tokenStorage.get()).toBeNull();
    expect(listener).toHaveBeenCalledOnce();
    window.removeEventListener(UNAUTHORIZED_EVENT, listener);
  });

  it('204 devuelve undefined', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
    await expect(api.get('/api/cash-sessions/current')).resolves.toBeUndefined();
  });

  it('buildQuery omite vacíos', () => {
    expect(buildQuery({ a: 1, b: '', c: undefined, d: false })).toBe('?a=1&d=false');
  });
});

describe('utilidades', () => {
  it('formatea soles', () => expect(formatMoney(1234.5)).toMatch(/S\/\s?1,234\.50/));
  it('suma días a una fecha ISO', () => expect(addDays('2026-02-28', 1)).toBe('2026-03-01'));
  it('valida formulario de producto', () => {
    const errors = validateProduct({ sku: 'a b', name: '', categoryId: '', price: '1.234', stock: '-1', minStock: 'x' }, true);
    expect(Object.keys(errors).sort()).toEqual(['categoryId', 'minStock', 'name', 'price', 'sku', 'stock']);
  });
});
