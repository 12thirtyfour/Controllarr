declare module '*.module.css';

interface ControllarrWindowConfig {
  apiKey: string;
  apiRoot: string;
  instanceName: string;
  theme: string;
  urlBase: string;
  version: string;
  isProduction: boolean;
  analytics?: boolean;
}

interface Window {
  Controllarr: ControllarrWindowConfig;
  Sonarr: ControllarrWindowConfig;
}

interface ImportMeta {
  glob: (pattern: string, options: { eager: true }) => unknown;
}
