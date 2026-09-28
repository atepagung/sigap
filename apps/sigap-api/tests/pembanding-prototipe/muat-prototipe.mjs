// Kait resolver Node untuk memuat src/logic/*.ts prototipe apa adanya, tanpa build dan tanpa
// node_modules prototipe. Node 24 menghapus anotasi tipe sendiri; yang perlu dibantu hanya
// tiga hal yang biasanya diurus bundler Next.js:
//   1. alias `@/` → `<prototipe>/src/`
//   2. impor tanpa ekstensi → `.ts`
//   3. `@/lib/prisma` diganti tiruan. Tiruan itu meneruskan ke `globalThis.__prismaPembanding`
//      bila skenario memasangnya (mis. baris "KantorBmn" untuk gedungTerdampak), dan melempar
//      galat bila tidak. Fungsi yang diam-diam menyentuh database di luar skenario membuat
//      pembuatan fikstur gagal keras, alih-alih merekam perilaku yang tidak sama.
//
// Dipasang lewat `module.register` oleh buat-fikstur.mjs. Jangan dipakai di luar itu.

import { existsSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import path from 'node:path';

const PRISMA_TIRUAN =
  'data:text/javascript,' +
  encodeURIComponent(
    'export const prisma = new Proxy({}, { get(_, k) {' +
      ' const p = globalThis.__prismaPembanding;' +
      " if (!p || !(k in p)) throw new Error('Fungsi yang dibandingkan menyentuh prisma.' + String(k) + ' di luar skenario.');" +
      ' return p[k]; } });'
  );

let akarSrc = '';

export async function initialize(data) {
  akarSrc = data.akarSrc;
}

function berkasTs(dasar) {
  for (const calon of [`${dasar}.ts`, path.join(dasar, 'index.ts')]) {
    if (existsSync(calon)) return pathToFileURL(calon).href;
  }
  return null;
}

export async function resolve(specifier, context, nextResolve) {
  if (specifier === '@/lib/prisma') {
    return { url: PRISMA_TIRUAN, shortCircuit: true };
  }
  if (specifier.startsWith('@/')) {
    const url = berkasTs(path.join(akarSrc, specifier.slice(2)));
    if (url) return { url, shortCircuit: true };
  }
  if ((specifier.startsWith('./') || specifier.startsWith('../')) && context.parentURL?.startsWith('file:')) {
    const induk = path.dirname(new URL(context.parentURL).pathname.replace(/^\/([A-Za-z]:)/, '$1'));
    const url = berkasTs(path.resolve(induk, specifier));
    if (url) return { url, shortCircuit: true };
  }
  return nextResolve(specifier, context);
}
