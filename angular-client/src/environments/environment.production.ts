export const environment = {
  production: true,
  apiBaseUrl: window.__TRADING_SCANNER_CONFIG__?.apiBaseUrl ?? 'http://localhost:5152/api',
};
