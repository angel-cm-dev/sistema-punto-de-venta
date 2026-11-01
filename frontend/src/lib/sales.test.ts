import { describe, expect, it } from 'vitest';
import { computeChange, computeTotals, isValidDni, isValidRuc, validateSale, type CartLine } from './sales';

const line = (unitPrice: number, quantity: number, stock = 100): CartLine => ({
  productId: unitPrice * 100 + quantity,
  name: `P${unitPrice}`,
  unitPrice,
  quantity,
  stock,
});

describe('computeTotals', () => {
  it('extrae el IGV de precios que ya lo incluyen', () => {
    expect(computeTotals([line(118, 1)])).toEqual({ gross: 118, discount: 0, total: 118, subtotal: 100, igv: 18 });
  });

  it('coincide con el backend: subtotal + IGV = total al céntimo', () => {
    const t = computeTotals([line(3.5, 2), line(24.9, 1)]);
    expect(t.total).toBe(31.9);
    expect(t.subtotal).toBe(27.03);
    expect(t.igv).toBe(4.87);
  });

  it('aplica el descuento antes de calcular el IGV y no permite descuentos mayores al bruto', () => {
    expect(computeTotals([line(118, 1)], 17.7).total).toBe(100.3);
    expect(computeTotals([line(10, 1)], 50).total).toBe(0);
  });
});

describe('computeChange', () => {
  it('calcula vuelto y faltante', () => {
    expect(computeChange(31.9, [{ method: 'Efectivo', amount: 50 }])).toEqual({ paid: 50, change: 18.1, missing: 0 });
    expect(computeChange(31.9, [{ method: 'Efectivo', amount: 30 }]).missing).toBe(1.9);
  });
});

describe('documentos de identidad', () => {
  it.each(['20601234565', '10456789124', '20487654320'])('RUC válido %s', (ruc) => expect(isValidRuc(ruc)).toBe(true));
  it.each(['20601234566', '30601234565', '123'])('RUC inválido %s', (ruc) => expect(isValidRuc(ruc)).toBe(false));
  it('DNI de 8 dígitos', () => {
    expect(isValidDni('45678912')).toBe(true);
    expect(isValidDni('4567891')).toBe(false);
  });
});

describe('validateSale', () => {
  const base = {
    documentType: 'Boleta' as const,
    customerDocType: null,
    lines: [line(10, 1)],
    payments: [{ method: 'Efectivo' as const, amount: 10 }],
    discount: 0,
    isAdmin: false,
  };

  it('acepta una venta correcta', () => expect(validateSale(base)).toEqual([]));

  it('exige RUC para factura', () =>
    expect(validateSale({ ...base, documentType: 'Factura' })).toContain('La factura requiere un cliente con RUC.'));

  it('limita el descuento del cajero al 10% pero no al admin', () => {
    expect(validateSale({ ...base, discount: 2 })).toContain('El descuento del cajero no puede superar el 10%.');
    expect(validateSale({ ...base, discount: 2, isAdmin: true })).toEqual([]);
  });

  it('detecta stock insuficiente y pagos electrónicos excedidos', () => {
    const errors = validateSale({ ...base, lines: [line(10, 5, 2)], payments: [{ method: 'Yape', amount: 60 }] });
    expect(errors.some((e) => e.startsWith('Stock insuficiente'))).toBe(true);
    expect(errors).toContain('Tarjeta/Yape/Plin no pueden superar el total.');
  });

  it('boletas sobre S/ 700 requieren cliente', () => {
    const big = { ...base, lines: [line(800, 1)], payments: [{ method: 'Tarjeta' as const, amount: 800 }] };
    expect(validateSale(big)).toContain('Boletas mayores a S/ 700 requieren identificar al cliente.');
    expect(validateSale({ ...big, customerDocType: 'Dni' })).toEqual([]);
  });
});
