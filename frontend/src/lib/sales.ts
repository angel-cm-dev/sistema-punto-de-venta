// Lógica de negocio del lado cliente para la pantalla de venta. Replica las reglas del
// backend SOLO para dar feedback inmediato: la validación autoritativa está en la API.

import type { DocumentType, PaymentMethod } from './types';

export const IGV_RATE = 0.18;
export const BOLETA_ID_THRESHOLD = 700;
export const CASHIER_MAX_DISCOUNT = 0.1;

export const round2 = (n: number): number => Math.round((n + Number.EPSILON) * 100) / 100;

export interface CartLine {
  productId: number;
  name: string;
  unitPrice: number;
  quantity: number;
  stock: number;
}

export interface CartPayment {
  method: PaymentMethod;
  amount: number;
  reference?: string;
}

export interface CartTotals {
  gross: number;
  discount: number;
  total: number;
  subtotal: number;
  igv: number;
}

/** Precios con IGV incluido: el impuesto se extrae del total (base = total / 1.18). */
export function computeTotals(lines: CartLine[], discount = 0): CartTotals {
  const gross = round2(lines.reduce((acc, l) => acc + round2(l.unitPrice * l.quantity), 0));
  const safeDiscount = Math.min(Math.max(discount, 0), gross);
  const total = round2(gross - safeDiscount);
  const subtotal = round2(total / (1 + IGV_RATE));
  return { gross, discount: round2(safeDiscount), total, subtotal, igv: round2(total - subtotal) };
}

export function computeChange(total: number, payments: CartPayment[]): { paid: number; change: number; missing: number } {
  const paid = round2(payments.reduce((acc, p) => acc + (p.amount || 0), 0));
  return { paid, change: round2(Math.max(paid - total, 0)), missing: round2(Math.max(total - paid, 0)) };
}

const RUC_WEIGHTS = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

/** Módulo 11 de SUNAT (mismo algoritmo que el backend). */
export function isValidRuc(value: string): boolean {
  if (!/^\d{11}$/.test(value)) return false;
  if (!['10', '15', '17', '20'].includes(value.slice(0, 2))) return false;
  const sum = RUC_WEIGHTS.reduce((acc, w, i) => acc + w * Number(value[i]), 0);
  let check = 11 - (sum % 11);
  if (check === 10) check = 0;
  else if (check === 11) check = 1;
  return check === Number(value[10]);
}

export const isValidDni = (value: string): boolean => /^\d{8}$/.test(value);

export interface SaleValidationInput {
  documentType: DocumentType;
  customerDocType?: 'Dni' | 'Ruc' | null;
  lines: CartLine[];
  payments: CartPayment[];
  discount: number;
  isAdmin: boolean;
}

/** Devuelve la lista de problemas que impiden cobrar (vacía = se puede cobrar). */
export function validateSale(input: SaleValidationInput): string[] {
  const errors: string[] = [];
  const totals = computeTotals(input.lines, input.discount);

  if (input.lines.length === 0) errors.push('Agregue al menos un producto.');
  input.lines
    .filter((l) => l.quantity > l.stock)
    .forEach((l) => errors.push(`Stock insuficiente de ${l.name} (disponible ${l.stock}).`));

  if (!input.isAdmin && input.discount > round2(totals.gross * CASHIER_MAX_DISCOUNT))
    errors.push('El descuento del cajero no puede superar el 10%.');

  if (input.documentType === 'Factura' && input.customerDocType !== 'Ruc')
    errors.push('La factura requiere un cliente con RUC.');
  if (input.documentType === 'Boleta' && totals.total > BOLETA_ID_THRESHOLD && !input.customerDocType)
    errors.push('Boletas mayores a S/ 700 requieren identificar al cliente.');

  const nonCash = round2(input.payments.filter((p) => p.method !== 'Efectivo').reduce((a, p) => a + (p.amount || 0), 0));
  if (nonCash > totals.total) errors.push('Tarjeta/Yape/Plin no pueden superar el total.');
  if (input.lines.length > 0 && computeChange(totals.total, input.payments).missing > 0)
    errors.push('El pago no cubre el total.');

  return errors;
}
