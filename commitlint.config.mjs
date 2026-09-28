// Conventional Commits, ditegakkan oleh hook commit-msg (.husky/commit-msg). Panduan lengkap di
// CONTRIBUTING.md bagian "Format commit".
//
// Bahasa subjek Indonesia, jadi aturan huruf besar/kecil bawaan (yang dirancang untuk Inggris)
// dilonggarkan. Yang dijaga: tipe yang sah, subjek tidak kosong, dan panjang header.

/** @type {import('@commitlint/types').UserConfig} */
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'docs',
        'style',
        'refactor',
        'perf',
        'test',
        'build',
        'ci',
        'chore',
        'revert',
      ],
    ],
    // Scope opsional; bila ada, salah satu area di bawah. Menjaga riwayat tetap bisa disaring.
    'scope-enum': [
      2,
      'always',
      ['web', 'api', 'shell', 'libs', 'infra', 'skema', 'docs', 'ci', 'deps', 'repo'],
    ],
    'subject-case': [0],
    'subject-full-stop': [2, 'never', '.'],
    'header-max-length': [2, 'always', 100],
    // Isi commit menjelaskan alasan dan sering memuat nama berkas serta URL panjang.
    'body-max-line-length': [0],
    'footer-max-line-length': [0],
  },
};
