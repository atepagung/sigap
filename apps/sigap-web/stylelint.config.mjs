// Stylelint untuk sigap-web. Inti berkas ini adalah aturan platform ICS:
//
//   Dilarang hardcode warna. Warna hanya berasal dari token design system, dipanggil dengan
//   `@use 'index' as *` (Standar Arsitektur ICS; Navy #003d7a, Blue #275EA8, Gold #FCB332).
//
// Satu-satunya tempat yang boleh memuat literal warna adalah berkas TOKEN. Di repo ini itu
// libs/keu-ui-dummy/styles/_tokens.scss (dummy). Saat paket asli @danarakca/keu-ui dipasang,
// token hidup di node_modules dan tidak pernah ikut diperiksa — pengecualian ini ikut hilang.

/** Fungsi warna yang menulis literal warna. Token yang dirujuk lewat variabel tetap boleh. */
const FUNGSI_WARNA = [
  'rgb',
  'rgba',
  'hsl',
  'hsla',
  'hwb',
  'lab',
  'lch',
  'oklab',
  'oklch',
  'color',
  'color-mix',
];

const PESAN =
  "Dilarang hardcode warna. Pakai token design system: @use 'index' as * lalu variabelnya. Lihat AGENTS.md bagian Styling.";

/** @type {import('stylelint').Config} */
export default {
  extends: ['stylelint-config-standard-scss'],

  // Berkas keluaran dan hasil generate.
  ignoreFiles: ['dist/**', '.angular/**', 'coverage/**', 'node_modules/**'],

  rules: {
    // ── Aturan warna ────────────────────────────────────────────────────────
    'color-no-hex': [true, { message: PESAN }],
    'color-named': ['never', { message: PESAN }],
    'function-disallowed-list': [FUNGSI_WARNA, { message: PESAN }],

    // ── Melonggarkan bawaan "standard" yang tidak relevan bagi kode ini ─────
    // Nama class katalog platform memakai BEM (.page-header__title); pola bawaan melarangnya.
    'selector-class-pattern': null,
    // Nama variabel/mixin mengikuti token platform, bukan pola kebab-case ketat kita.
    'scss/dollar-variable-pattern': null,
    'scss/at-mixin-pattern': null,
    'scss/at-function-pattern': null,
    // Komentar [ASUMSI] di berkas token ditulis rapat di samping nilainya.
    'scss/double-slash-comment-empty-line-before': null,
    'scss/comment-no-empty': null,
    'scss/dollar-variable-empty-line-before': null,
    // Notasi warna itu soal selera; yang dijaga di sini adalah SUMBER warna, bukan penulisannya.
    'color-hex-length': null,
    'color-function-notation': null,
    'color-function-alias-notation': null,
    'alpha-value-notation': null,
  },

  overrides: [
    // Template Angular: periksa <style> dan atribut style="" — tempat warna hardcode sering
    // menyelinap. Hanya bagian gaya yang diurai; sintaks template (@if, *hasPermission) diabaikan.
    {
      files: ['**/*.html'],
      customSyntax: 'postcss-html',
      rules: {
        // Atribut style="" berisi deklarasi tanpa selector; aturan bawaan tidak berlaku.
        'no-empty-source': null,
        'declaration-block-no-redundant-longhand-properties': null,
      },
    },

    // SATU-SATUNYA pengecualian: berkas token.
    {
      files: [
        '**/libs/keu-ui-dummy/styles/_tokens.scss',
        '../../libs/keu-ui-dummy/styles/_tokens.scss',
      ],
      rules: {
        'color-no-hex': null,
        'color-named': null,
        'function-disallowed-list': null,
      },
    },
  ],
};
