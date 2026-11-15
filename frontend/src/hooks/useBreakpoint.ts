import { useEffect, useState } from 'react';

/** Khớp breakpoint Tailwind: sm 640 · md 768 · lg 1024 · xl 1280 */
const queries = {
  sm: '(min-width: 640px)',
  md: '(min-width: 768px)',
  lg: '(min-width: 1024px)',
  xl: '(min-width: 1280px)',
} as const;

type Breakpoints = Record<keyof typeof queries, boolean>;

function read(): Breakpoints {
  if (typeof window === 'undefined') return { sm: true, md: true, lg: true, xl: true };
  return Object.fromEntries(
    Object.entries(queries).map(([key, q]) => [key, window.matchMedia(q).matches]),
  ) as Breakpoints;
}

export function useBreakpoint(): Breakpoints & { isMobile: boolean } {
  const [state, setState] = useState<Breakpoints>(read);
  useEffect(() => {
    const lists = Object.values(queries).map((q) => window.matchMedia(q));
    const onChange = () => setState(read());
    lists.forEach((l) => l.addEventListener('change', onChange));
    return () => lists.forEach((l) => l.removeEventListener('change', onChange));
  }, []);
  return { ...state, isMobile: !state.sm };
}
