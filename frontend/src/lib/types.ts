// Tipos que reflejan los DTOs del backend (PuntoVenta.Application).

export type Role = 'Admin' | 'Cajero';
export type DocumentType = 'Boleta' | 'Factura';
export type IdentityDocType = 'Dni' | 'Ruc';
export type PaymentMethod = 'Efectivo' | 'Tarjeta' | 'Yape' | 'Plin';
export type SaleStatus = 'Emitida' | 'Anulada';
export type CashSessionStatus = 'Abierta' | 'Cerrada';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface User {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Category {
  id: number;
  name: string;
  description: string | null;
  productCount: number;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  categoryId: number;
  categoryName: string;
  price: number;
  stock: number;
  minStock: number;
  isLowStock: boolean;
  isActive: boolean;
}

export interface Customer {
  id: number;
  docType: IdentityDocType;
  docNumber: string;
  name: string;
  email: string | null;
  phone: string | null;
  address: string | null;
  createdAt: string;
}

export interface PaymentMethodTotal {
  method: PaymentMethod;
  amount: number;
}

export interface CashSession {
  id: number;
  cashierId: number;
  cashierName: string;
  status: CashSessionStatus;
  openedAt: string;
  closedAt: string | null;
  openingAmount: number;
  salesCount: number;
  voidedCount: number;
  salesTotal: number;
  paymentsByMethod: PaymentMethodTotal[];
  expectedCash: number;
  countedCash: number | null;
  difference: number | null;
  notes: string | null;
}

export interface CashSessionListItem {
  id: number;
  cashierName: string;
  status: CashSessionStatus;
  openedAt: string;
  closedAt: string | null;
  openingAmount: number;
  expectedCash: number | null;
  countedCash: number | null;
  difference: number | null;
}

export interface SaleItem {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Payment {
  method: PaymentMethod;
  amount: number;
  reference: string | null;
}

export interface Sale {
  id: number;
  documentType: DocumentType;
  documentNumber: string;
  status: SaleStatus;
  createdAt: string;
  customerId: number | null;
  customerName: string | null;
  customerDocNumber: string | null;
  cashierId: number;
  cashierName: string;
  cashSessionId: number;
  grossAmount: number;
  discount: number;
  subtotal: number;
  igv: number;
  total: number;
  amountPaid: number;
  change: number;
  items: SaleItem[];
  payments: Payment[];
  voidedAt: string | null;
  voidReason: string | null;
}

export interface SaleListItem {
  id: number;
  documentType: DocumentType;
  documentNumber: string;
  status: SaleStatus;
  createdAt: string;
  customerName: string | null;
  cashierName: string;
  total: number;
}

export interface DailySales {
  date: string;
  tickets: number;
  subtotal: number;
  igv: number;
  total: number;
}

export interface ProductSales {
  productId: number;
  productName: string;
  quantity: number;
  amount: number;
}

export interface CashierSales {
  cashierId: number;
  cashierName: string;
  tickets: number;
  total: number;
  averageTicket: number;
}

export interface Dashboard {
  salesToday: number;
  ticketsToday: number;
  averageTicketToday: number;
  salesMonth: number;
  lowStockProducts: number;
  openCashSessions: number;
  last7Days: DailySales[];
  topProducts: ProductSales[];
}
