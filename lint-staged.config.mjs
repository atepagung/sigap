// Dijalankan hook pre-commit (.husky/pre-commit) hanya atas berkas yang di-stage, supaya commit
// tetap cepat. CI menjalankan pemeriksaan penuh — hook ini lapisan pertama, bukan satu-satunya.
//
// Kompilasi .NET sengaja TIDAK di sini: terlalu lambat untuk hook, dan build sudah menegakkan
// analyzer + gaya kode (EnforceCodeStyleInBuild). Itu urusan CI.
//
// Path dikutip dengan JSON.stringify supaya berkas yang namanya berspasi tetap aman di Windows
// (PowerShell/cmd) maupun bash.

const q = (files) => files.map((f) => JSON.stringify(f)).join(' ');

export default {
  // Kode dan template sigap-web: perbaiki otomatis yang aman, lalu format.
  'apps/sigap-web/**/*.{ts,html}': (files) => [
    `eslint --fix --no-warn-ignored ${q(files)}`,
    `prettier --write --ignore-path apps/sigap-web/.prettierignore ${q(files)}`,
  ],

  // Gaya SCSS + aturan warna: sigap-web dan katalog design system dummy.
  '{apps/sigap-web,libs/keu-ui-dummy}/**/*.{scss,css}': (files) =>
    `stylelint --config apps/sigap-web/stylelint.config.mjs ${q(files)}`,

  'apps/sigap-web/**/*.{json,mjs}': (files) =>
    `prettier --write --ignore-path apps/sigap-web/.prettierignore ${q(files)}`,

  // Skrip dan konfigurasi di root repo.
  '{scripts/*.mjs,*.mjs}': (files) => `prettier --write ${q(files)}`,

  // Aturan lintas platform (akhir baris, kapitalisasi, skrip npm, path C#) atas seluruh repo.
  // Fungsi yang mengembalikan string tanpa argumen berkas = dijalankan sekali.
  '*': () => 'node scripts/periksa-repo.mjs',
};
