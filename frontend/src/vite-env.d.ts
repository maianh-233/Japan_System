/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "api" (mặc định) · "mock" — xem src/lib/api/config.ts */
  readonly VITE_API_MODE?: 'api' | 'mock';
  readonly VITE_API_BASE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
