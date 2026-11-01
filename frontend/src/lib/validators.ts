import { isValidDni, isValidRuc } from './sales';

export type Errors<T> = Partial<Record<keyof T, string>>;

export interface ProductForm {
  sku: string;
  name: string;
  categoryId: string;
  price: string;
  stock: string;
  minStock: string;
}

/** Validación de formulario de producto (mismas reglas que el validador FluentValidation del backend). */
export function validateProduct(form: ProductForm, isNew: boolean): Errors<ProductForm> {
  const e: Errors<ProductForm> = {};
  if (isNew && !/^[A-Za-z0-9-]{1,30}$/.test(form.sku.trim())) e.sku = 'Solo letras, números y guiones (máx. 30).';
  if (!form.name.trim()) e.name = 'El nombre es obligatorio.';
  else if (form.name.trim().length > 150) e.name = 'Máximo 150 caracteres.';
  if (!form.categoryId) e.categoryId = 'Seleccione una categoría.';
  const price = Number(form.price);
  if (form.price === '' || Number.isNaN(price) || price <= 0) e.price = 'El precio debe ser mayor a 0.';
  else if (!/^\d+(\.\d{1,2})?$/.test(form.price)) e.price = 'Máximo 2 decimales.';
  if (isNew && (!/^\d+$/.test(form.stock) || Number(form.stock) < 0)) e.stock = 'Stock entero ≥ 0.';
  if (!/^\d+$/.test(form.minStock)) e.minStock = 'Stock mínimo entero ≥ 0.';
  return e;
}

export interface CustomerForm {
  docType: 'Dni' | 'Ruc';
  docNumber: string;
  name: string;
  email: string;
  phone: string;
  address: string;
}

export function validateCustomer(form: CustomerForm, isNew: boolean): Errors<CustomerForm> {
  const e: Errors<CustomerForm> = {};
  if (isNew) {
    if (form.docType === 'Dni' && !isValidDni(form.docNumber)) e.docNumber = 'DNI inválido (8 dígitos).';
    if (form.docType === 'Ruc' && !isValidRuc(form.docNumber)) e.docNumber = 'RUC inválido (dígito verificador).';
  }
  if (!form.name.trim()) e.name = 'El nombre o razón social es obligatorio.';
  if (form.email && !/^\S+@\S+\.\S+$/.test(form.email)) e.email = 'Correo inválido.';
  return e;
}

export interface UserForm {
  email: string;
  fullName: string;
  password: string;
  role: 'Admin' | 'Cajero';
}

export function validateUser(form: UserForm, isNew: boolean): Errors<UserForm> {
  const e: Errors<UserForm> = {};
  if (isNew && !/^\S+@\S+\.\S+$/.test(form.email)) e.email = 'Correo inválido.';
  if (!form.fullName.trim()) e.fullName = 'El nombre es obligatorio.';
  if (isNew || form.password) {
    if (form.password.length < 8 || !/[A-Z]/.test(form.password) || !/[a-z]/.test(form.password) || !/\d/.test(form.password))
      e.password = 'Mínimo 8 caracteres con mayúscula, minúscula y dígito.';
  }
  return e;
}

export const hasErrors = (e: object): boolean => Object.keys(e).length > 0;
