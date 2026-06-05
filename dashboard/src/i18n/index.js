import { resources } from './resources';

const defaultLocale = 'en';

export function t(key, values = {}) {
  const parts = key.split('.');
  let current = resources[defaultLocale];

  for (const part of parts) {
    current = current?.[part];
  }

  let text = typeof current === 'string' ? current : key;
  for (const [name, value] of Object.entries(values)) {
    text = text.replace(new RegExp(`{${name}}`, 'g'), String(value));
  }

  return text;
}
