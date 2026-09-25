import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const APP_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

const IDENTITY_KEYS = [
  'remoteName',
  'elementName',
  'defineFunction',
  'selector',
  'routePath',
  'displayName',
  'port',
];

export function readRemoteIdentity() {
  const raw = JSON.parse(readFileSync(join(APP_ROOT, 'remote-identity.json'), 'utf8'));
  return Object.fromEntries(IDENTITY_KEYS.map((key) => [key, raw[key]]));
}

const GENERATED_DIR = join(APP_ROOT, 'src', 'federation', 'generated');
const HEADER = [
  '// HASIL GENERATE dari remote-identity.json oleh scripts/remote-identity.mjs.',
  '// Jangan diedit dan jangan di-commit — ubah remote-identity.json.',
];

// Dua hal tidak bisa dibaca dari JSON saat runtime:
// - nama fungsi define harus named export statis (itulah yang dipanggil shell);
// - selector di @Component harus bisa dievaluasi statis oleh compiler AOT.
// Karena itu keduanya di-generate sebagai TypeScript dari remote-identity.json.
export function generateRemoteSources() {
  const identity = readRemoteIdentity();
  if (!/^[A-Za-z_$][\w$]*$/.test(identity.defineFunction)) {
    throw new Error(
      `defineFunction di remote-identity.json bukan identifier yang valid: "${identity.defineFunction}"`,
    );
  }
  mkdirSync(GENERATED_DIR, { recursive: true });
  writeFileSync(
    join(GENERATED_DIR, 'remote-identity.ts'),
    [
      ...HEADER,
      `export const REMOTE_IDENTITY = ${JSON.stringify(identity, null, 2)} as const;`,
      '',
    ].join('\n'),
  );
  writeFileSync(
    join(GENERATED_DIR, 'remote-entry.ts'),
    [
      ...HEADER,
      `export { defineRemoteElement as ${identity.defineFunction} } from '../define-remote-element';`,
      '',
    ].join('\n'),
  );
}
