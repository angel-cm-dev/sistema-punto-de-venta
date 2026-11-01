const currency = new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN', minimumFractionDigits: 2 });

/** Formatea soles: 1234.5 → "S/ 1,234.50". */
export const formatMoney = (value: number | null | undefined): string =>
  // Intl usa un espacio duro (U+00A0) tras "S/"; se normaliza a espacio simple.
  currency.format(value ?? 0).replace(/\s/g, ' ');

const dateTime = new Intl.DateTimeFormat('es-PE', {
  timeZone: 'America/Lima',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/** Fecha y hora en hora de Lima (el backend guarda UTC). */
export const formatDateTime = (iso: string | null | undefined): string => (iso ? dateTime.format(new Date(iso)) : '—');

/** "2026-11-01" → "01/11" (para ejes de gráficos). */
export const formatShortDate = (isoDate: string): string => {
  const [, m, d] = isoDate.split('-');
  return `${d}/${m}`;
};

/** Fecha de hoy en Lima en formato yyyy-MM-dd (para filtros). */
export const todayLima = (now = new Date()): string =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Lima' }).format(now);

export const addDays = (isoDate: string, days: number): string => {
  const d = new Date(`${isoDate}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

export const paymentLabels: Record<string, string> = {
  Efectivo: 'Efectivo',
  Tarjeta: 'Tarjeta',
  Yape: 'Yape',
  Plin: 'Plin',
};
