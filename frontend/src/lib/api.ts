// Cliente HTTP mínimo sobre fetch: agrega el JWT, normaliza errores ProblemDetails
// y avisa a la app cuando la sesión expira (401) para redirigir al login.

const TOKEN_KEY = 'pos.token';
export const UNAUTHORIZED_EVENT = 'pos:unauthorized';

const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

export const tokenStorage = {
  get: (): string | null => {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  },
  set: (token: string) => {
    try {
      localStorage.setItem(TOKEN_KEY, token);
    } catch {
      /* almacenamiento no disponible (modo privado): la sesión dura lo que la pestaña */
    }
  },
  clear: () => {
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch {
      /* ignorado */
    }
  },
};

/** Error de API con la información de un ProblemDetails (RFC 9457). */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly title: string,
    public readonly detail?: string,
    public readonly fieldErrors: Record<string, string[]> = {},
    public readonly code?: string,
  ) {
    super(detail || title);
    this.name = 'ApiError';
  }

  /** Mensaje listo para mostrar al usuario. */
  get userMessage(): string {
    const firstField = Object.values(this.fieldErrors)[0]?.[0];
    return this.detail || firstField || this.title;
  }
}

interface ProblemBody {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  code?: string;
}

export async function parseError(response: Response): Promise<ApiError> {
  let body: ProblemBody = {};
  try {
    body = (await response.json()) as ProblemBody;
  } catch {
    /* respuesta sin cuerpo JSON (p. ej. 403 vacío o error del proxy) */
  }
  const fallback: Record<number, string> = {
    401: 'Sesión expirada o no autenticada',
    403: 'No tiene permisos para esta acción',
    404: 'Recurso no encontrado',
    429: 'Demasiados intentos, espere un momento',
    502: 'El servidor no está disponible',
  };
  return new ApiError(
    response.status,
    body.title ?? fallback[response.status] ?? `Error ${response.status}`,
    body.detail,
    body.errors ?? {},
    body.code,
  );
}

type Query = Record<string, string | number | boolean | null | undefined>;

export function buildQuery(params?: Query): string {
  if (!params) return '';
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value));
  });
  const qs = search.toString();
  return qs ? `?${qs}` : '';
}

async function request<T>(method: string, path: string, body?: unknown, query?: Query): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  const token = tokenStorage.get();
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let response: Response;
  try {
    response = await fetch(`${BASE_URL}${path}${buildQuery(query)}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'No se pudo conectar con el servidor', 'Verifique su conexión o que la API esté en ejecución.');
  }

  if (response.status === 401 && token) {
    tokenStorage.clear();
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
  }
  if (!response.ok) throw await parseError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, query?: Query) => request<T>('GET', path, undefined, query),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  put: <T>(path: string, body: unknown) => request<T>('PUT', path, body),
  delete: <T = void>(path: string) => request<T>('DELETE', path),
};
