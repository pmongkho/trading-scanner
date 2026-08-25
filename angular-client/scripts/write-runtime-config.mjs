import { writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const configured = process.env.API_BASE_URL?.trim();
if (process.env.VERCEL && !configured) {
  throw new Error('API_BASE_URL must be set to the deployed ASP.NET server URL in Vercel.');
}

const parsed = new URL(configured || 'http://localhost:5152');
if (!['http:', 'https:'].includes(parsed.protocol)) {
  throw new Error('API_BASE_URL must use http or https.');
}

parsed.pathname = `${parsed.pathname.replace(/\/$/, '').replace(/\/api$/, '')}/api`;
parsed.search = '';
parsed.hash = '';

const destination = fileURLToPath(new URL('../public/runtime-config.js', import.meta.url));
const contents = `window.__TRADING_SCANNER_CONFIG__ = ${JSON.stringify({ apiBaseUrl: parsed.toString().replace(/\/$/, '') })};\n`;
await writeFile(destination, contents, 'utf8');
console.log(`Angular API target: ${parsed.origin}${parsed.pathname}`);
