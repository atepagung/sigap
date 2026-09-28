# keu-ui-dummy

Dummy pengganti **`@danarakca/keu-ui`** (design system platform ICS Keuangan), dipakai selama
paket resmi belum bisa diakses.

> **Jangan menambah fitur di sini.** Dummy hanya perlu berjalan, tidak perlu benar (aturan dummy
> #3, `docs/PLAYBOOK.md` Fase 3). Kalau butuh sesuatu yang belum ada di sini, yang benar adalah
> menanyakannya ke BaTII dan mencatatnya di [DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md) —
> bukan mengarang komponen baru.

## Cara kode aplikasi memakainya

Kode aplikasi **tidak pernah menyebut kata "dummy"**. Impor memakai nama paket asli:

```ts
import { KeuModalComponent } from '@danarakca/keu-ui';
```

```scss
@use 'index' as *;

.ringkasan-unit {
  color: $color-navy; // token, bukan hardcode #003d7a
}
```

Untuk tampilan, pakai class katalog — **dilarang** membuat komponen visual sendiri yang
menduplikasi katalog, dan **dilarang** hardcode warna:

```html
<div class="table-card">
  <div class="table-card__header">
    <h2 class="table-card__title">Rekap Safety Check</h2>
  </div>
  ...
</div>
```

## Mekanisme penukaran — ada DUA sisi

Ini bagian terpenting. Alias TypeScript saja **tidak cukup**, karena SCSS tidak lewat TypeScript.

### Sisi 1 — TypeScript (`tsconfig.base.json`)

```json
"paths": { "@danarakca/keu-ui": ["libs/keu-ui-dummy/index.ts"] }
```

### Sisi 2 — SCSS (`angular.json`, dibuat saat P3.2/P4)

Dua entri berbeda, jangan tertukar:

```json
"styles": ["libs/keu-ui-dummy/styles/catalog.scss"],
"stylePreprocessorOptions": {
  "includePaths": ["libs/keu-ui-dummy/styles"]
}
```

- `includePaths` yang membuat `@use 'index' as *` bisa menemukan `_index.scss`.
  Isinya **hanya token** — tidak menghasilkan CSS, jadi aman dipanggil dari komponen mana pun.
- `styles` memuat **katalog komponen** (`.page-header`, `.table-card`, dst) satu kali secara
  global. Katalog sengaja **tidak** ikut di `_index.scss`: kalau ikut, CSS-nya akan terduplikasi
  di setiap komponen yang menulis `@use 'index' as *`.

> **Terbukti di P3.2:** saat remote dimuat di dalam shell lewat Native Federation, `styles` global
> milik remote **tidak ikut termuat** — hanya JavaScript-nya. Karena itu `apps/shell-dummy` juga
> memuat `catalog.scss` secara global; `styles` di angular.json remote hanya berlaku saat remote
> dijalankan mandiri. Konsekuensinya bagi kode aplikasi: jangan pernah menaruh style di
> stylesheet global remote.

> **[ASUMSI]** Pemisahan token vs katalog ini tebakan kita. Slide arsitektur hanya menyebut
> `@use 'index' as *` dan nama class, tanpa menjelaskan bagaimana CSS katalog dimuat. Kalau
> paket resmi ternyata menggabung keduanya, yang berubah hanya dua baris angular.json di atas.

### Langkah penukaran

1. Hapus entri `@danarakca/keu-ui` dari `compilerOptions.paths` di `tsconfig.base.json`.
2. `npm install @danarakca/keu-ui`.
3. Arahkan `styles` dan `includePaths` di `angular.json` ke berkas milik paket resmi.
4. Sesuaikan nama yang ternyata berbeda (lihat kolom asumsi di
   [DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md)).
5. Hapus folder `libs/keu-ui-dummy/`.

Langkah 1–3 tidak menyentuh satu pun berkas kode aplikasi.

## Isi

| Berkas | Isi | Status nama |
| --- | --- | --- |
| `styles/_index.scss` | Entry untuk `@use 'index' as *`, mem-forward token | `index` [RESMI] |
| `styles/_tokens.scss` | Token warna, jarak, radius, tipografi | Navy/Blue/Gold [RESMI], sisanya [ASUMSI] |
| `styles/catalog.scss` | Entry katalog komponen (dimuat global) | [ASUMSI] |
| `styles/catalog/_page-header.scss` | `.page-header` | class induk [RESMI], anak [ASUMSI] |
| `styles/catalog/_stats-row.scss` | `.stats-row`, `.stat-card` | `.stats-row` [RESMI], sisanya [ASUMSI] |
| `styles/catalog/_table-card.scss` | `.table-card` | class induk [RESMI], anak [ASUMSI] |
| `styles/catalog/_form-field.scss` | `.form-field` | [ASUMSI] |
| `styles/catalog/_button.scss` | `.button` | [ASUMSI] |
| `styles/catalog/_status-badge.scss` | `.status-badge` | [ASUMSI] |
| `styles/catalog/_tabs.scss` | `.tabs` | [ASUMSI] |
| `styles/catalog/_modal.scss` | `.modal`, `.modal-backdrop` | [ASUMSI] |
| `index.ts` | Public API TypeScript | [ASUMSI] |
| `src/keu-modal.component.ts` | `<keu-modal>` | [ASUMSI] |
| `src/keu-tabs.component.ts` | `<keu-tabs>`, `<keu-tab>` | [ASUMSI] |

## Kenapa komponen Angular-nya cuma dua

Setiap nama Angular yang kita tebak (`<keu-modal>`) akan tersebar ke template aplikasi dan harus
dicari-ganti saat paket resmi datang. Nama class CSS `.page-header`, `.stats-row`, `.table-card`
**tidak** punya risiko itu karena namanya berasal dari slide resmi.

Karena itu komponen Angular dibatasi ke dua kasus yang benar-benar butuh perilaku dan tidak bisa
diselesaikan dengan CSS saja:

- **`<keu-modal>`** — popup broadcast safety check (wajib dijawab, `dismissible=false`) dan
  dialog konfirmasi.
- **`<keu-tabs>` / `<keu-tab>`** — rekap safety check per kondisi (Aman / Butuh Bantuan / Belum
  Merespons), koreksi stakeholder #11.

Selebihnya (kartu statistik, header halaman, tabel, badge, form field, tombol) cukup class CSS.

Keduanya sengaja **tidak punya style sendiri** — tampilannya menumpang katalog global, supaya
saat katalog resmi masuk, tampilan ikut berubah tanpa menyentuh komponen.

Kompatibilitas: komponen memakai `@Input()`/`@Output()` klasik (bukan signal `input()`/`output()`
yang menuntut Angular ≥17.1) dan tag penutup eksplisit `<ng-content></ng-content>` (bukan
self-closing yang menuntut Angular ≥16). Batas bawahnya tinggal `standalone: true`, yaitu Angular
≥14 — jadi versi apa pun yang nanti dibawa `starter.mfe` hampir pasti jalan tanpa perubahan.
