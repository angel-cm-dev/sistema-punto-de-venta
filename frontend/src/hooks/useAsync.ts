import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../lib/api';

interface AsyncState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  reload: () => void;
}

/**
 * Ejecuta una carga asíncrona cuando cambian sus dependencias y expone
 * los estados de carga / error / datos. Descarta respuestas obsoletas
 * (si el usuario cambia de filtro rápido, solo cuenta la última petición).
 */
export function useAsync<T>(loader: () => Promise<T>, deps: unknown[]): AsyncState<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const requestId = useRef(0);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(loader, deps);

  useEffect(() => {
    const id = ++requestId.current;
    setLoading(true);
    setError(null);
    run()
      .then((result) => {
        if (id === requestId.current) setData(result);
      })
      .catch((e: unknown) => {
        if (id === requestId.current) setError(e instanceof ApiError ? e.userMessage : 'Error inesperado');
      })
      .finally(() => {
        if (id === requestId.current) setLoading(false);
      });
  }, [run, tick]);

  const reload = useCallback(() => setTick((t) => t + 1), []);
  return { data, loading, error, reload };
}

/** Retrasa un valor (p. ej. el texto de búsqueda) para no disparar una petición por tecla. */
export function useDebounced<T>(value: T, delay = 350): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}
