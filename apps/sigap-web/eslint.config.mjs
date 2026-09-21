// ESLint (flat config) untuk sigap-web. Selain aturan Angular/TypeScript baku, berkas ini
// menegakkan aturan platform ICS dan aturan proyek yang bisa diperiksa mesin. Yang tidak bisa
// (mis. "jangan taruh logika bisnis di shell") ada di AGENTS.md.
//
// Setiap aturan khusus di bawah diberi alasan. Jangan dimatikan dengan komentar
// eslint-disable tanpa alasan tertulis di tempatnya.

import eslint from '@eslint/js';
import angular from 'angular-eslint';
import prettier from 'eslint-config-prettier';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/**
 * Literal warna dalam string TypeScript, mis. gaya inline atau `styles: [...]` di @Component:
 * hex, atau fungsi warna (rgb/hsl/...). Stylelint menjaga berkas .scss dan .html; ini menjaga
 * TypeScript, yang tidak dijangkau Stylelint.
 *
 * Keterbatasan yang disadari: string yang secara sintaks adalah hex warna sah ikut ditandai,
 * termasuk `#123` (= #112233). Rujukan tiket ditulis "issue 123", bukan "#123".
 */
const HEX_DALAM_STRING =
  '(?:#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})(?![0-9a-zA-Z_-])|(?:^|[^A-Za-z-])(?:rgba?|hsla?|hwb|oklch|oklab|lab|lch) *[(])';

export default tseslint.config(
  {
    // Keluaran build dan berkas yang di-generate dari remote-identity.json. Yang di-generate
    // tidak disunting tangan, jadi tidak ikut diperiksa gayanya.
    ignores: ['dist/**', '.angular/**', 'coverage/**', 'src/federation/generated/**'],
  },

  // ── TypeScript ─────────────────────────────────────────────────────────────
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      // Prefiks selector `app` dan gaya kebab-case/camelCase. Selector modul federation
      // (`app-remote-sigap-bencana-entry`) diturunkan dari remote-identity.json, bukan ditulis
      // tangan, dan memenuhi aturan yang sama.
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'app', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'app', style: 'kebab-case' },
      ],

      // Aturan platform: DILARANG nested routing (flat routes saja). Router remote sinkron dua
      // arah dengan URL browser lewat Silent Location Strategy; rute bersarang merusaknya.
      'no-restricted-syntax': [
        'error',
        {
          selector: "Property[key.name='children']",
          message:
            'Dilarang nested routing (Standar ICS): pakai flat routes tanpa `children`. Lihat apps/sigap-web/AGENTS.md.',
        },
        {
          selector: "Property[key.name='loadChildren']",
          message:
            'Dilarang nested routing (Standar ICS): pakai flat routes, gunakan `loadComponent`. Lihat apps/sigap-web/AGENTS.md.',
        },
        // Dilarang hardcode warna. Warna hanya dari token design system (`@use 'index' as *`).
        {
          selector: `Literal[value=/${HEX_DALAM_STRING}/]`,
          message:
            "Dilarang hardcode warna hex. Pakai token design system lewat SCSS (@use 'index' as *). Lihat AGENTS.md bagian Styling.",
        },
        {
          selector: `TemplateElement[value.raw=/${HEX_DALAM_STRING}/]`,
          message:
            "Dilarang hardcode warna hex. Pakai token design system lewat SCSS (@use 'index' as *). Lihat AGENTS.md bagian Styling.",
        },
      ],

      // Kode aplikasi mengimpor lewat nama paket ASLI (`@danarakca/keu-ui`, `@danarakca/iam`),
      // bukan jalur ke library dummy. Itulah yang membuat penukaran dummy cukup satu baris di
      // tsconfig.base.json tanpa mengubah satu pun import (DUMMY_REGISTRY.md).
      // Pengecualian: src/federation/** — plumbing yang memang dikarantina (lihat override di bawah).
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: [
                '**/libs/*-dummy*',
                '**/libs/*-dummy*/**',
                '**/keu-ui-dummy*',
                '**/iam-dummy*',
              ],
              message:
                'Impor lewat nama paket asli (@danarakca/keu-ui, @danarakca/iam), bukan jalur ke library dummy.',
            },
          ],
        },
      ],

      '@typescript-eslint/no-explicit-any': 'error',
      // Halaman modul ini tidak boleh mengubah judul tab: itu milik shell (DUMMY_REGISTRY butir 49).
      'no-restricted-properties': [
        'error',
        {
          object: 'document',
          property: 'title',
          message: 'Judul tab milik shell, bukan remote (DUMMY_REGISTRY butir 49).',
        },
      ],
    },
  },

  // Berkas tes: mock dan akses internal wajar di sini.
  {
    files: ['**/*.spec.ts'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      'no-restricted-syntax': 'off',
    },
  },

  // Satu-satunya tempat yang boleh menyetel judul tab: mode mandiri, ketika remote dibuka
  // langsung di port-nya tanpa shell. Di dalam shell, judul milik shell.
  {
    files: ['src/federation/standalone.ts'],
    rules: { 'no-restricted-properties': 'off' },
  },

  // Plumbing federation dikarantina dari kode fitur (DUMMY_REGISTRY 1.3): boleh menyentuh
  // API router/history tingkat rendah, dan diganti utuh saat starter.mfe resmi tersedia (P7.2).
  {
    files: ['src/federation/**/*.ts'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },

  // ── Template HTML (juga template inline) ───────────────────────────────────
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      '@angular-eslint/template/prefer-control-flow': 'error',
    },
  },

  // ── Skrip Node (.mjs) ──────────────────────────────────────────────────────
  {
    files: ['**/*.mjs', '**/*.js'],
    extends: [eslint.configs.recommended],
    languageOptions: { globals: { ...globals.node } },
  },

  // Terakhir: matikan aturan gaya yang bentrok dengan Prettier. Formatting urusan Prettier.
  prettier,
);
