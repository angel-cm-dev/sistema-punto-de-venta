import { useCallback, useState } from 'react';

const KEY = 'pos.theme';

/** Alterna la clase "dark" en <html> y recuerda la preferencia del usuario. */
export function useTheme() {
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'));

  const toggle = useCallback(() => {
    setDark((prev) => {
      const next = !prev;
      document.documentElement.classList.toggle('dark', next);
      try {
        localStorage.setItem(KEY, next ? 'dark' : 'light');
      } catch {
        /* sin almacenamiento: el tema dura la sesión */
      }
      return next;
    });
  }, []);

  return { dark, toggle };
}
