# sigap-web

Remote MFE SIGAP Bencana di platform ICS Keuangan. Di-scaffold sendiri dengan Angular CLI karena
`starter.mfe` belum bisa diakses (PLAYBOOK Lampiran E #1); direkonsiliasi dengan `starter.mfe` resmi
di P7.2.

## Menjalankan

Dari root repo:

```bash
npm install
npm run start:web      # remote mandiri, port dari remote-identity.json
npm run start:shell    # shell dummy di port 4200, memuat remote ini
```

**Jangan jalankan `ng serve` / `ng build` langsung** — pakai skrip npm. Port dan berkas entri
federation diturunkan dari `remote-identity.json` oleh skrip sebelum Angular CLI dipanggil.

**Jangan menjalankan `npm run build` saat dev server app yang sama masih hidup.** Keduanya berbagi
cache artefak Native Federation; dev server lalu menyajikan campuran chunk production dan
development (gejalanya: halaman kosong, `ngDevMode is not defined`, 404 pada `_angular_*.js`).
Kalau terjadi, cukup restart dev server-nya.

## Identitas remote — satu berkas

Seluruh identitas (remoteName, element, function, selector, route path, display name, port) ada di
[`remote-identity.json`](remote-identity.json) dan **hanya di sana**. Nilainya PROVISIONAL sampai
BaTII menetapkan yang resmi. Untuk menukar: ubah berkas itu, lalu `npm test` — tes Golden Rule akan
gagal kalau turunannya tidak konsisten satu sama lain.

Yang membaca berkas itu:

| Pembaca | Nilai yang dipakai |
| --- | --- |
| `federation.config.mjs` | `remoteName` |
| `scripts/serve.mjs` | `port` |
| `scripts/remote-identity.mjs` | men-generate `src/federation/generated/` (named export `defineFunction`, konstanta untuk `selector`) |
| `src/federation/*` | `elementName`, `selector`, `routePath` |
| `apps/shell-dummy/src/registry/remote-registry.ts` | semuanya (hanya selama shell masih dummy) |

## Struktur

```
src/
  federation/   plumbing federation — tanggung jawab starter.mfe, JANGAN taruh fitur di sini
  app/          kode aplikasi: rute, halaman, fitur
```

`src/federation/` dikarantina karena seluruh isinya akan diganti/dibandingkan dengan `starter.mfe`
di P7.2. Kode fitur tidak boleh bergantung pada detail di dalamnya selain
`generated/remote-identity`.

## Aturan yang wajib dipatuhi kode aplikasi

- **Routing flat, path relatif, dilarang nested** — tidak ada `children` di `app.routes.ts`.
  Rute ditulis tanpa awalan route path remote: tulis `verifikasi`, bukan `/sigap-bencana/verifikasi`.
  Remote tidak tahu (dan tidak boleh tahu) di mana shell memasangnya; `APP_BASE_HREF` yang
  menangani awalannya. Tautan ke akar remote memakai `routerLink="/"` — itu akar *router remote*,
  tidak pernah keluar ke wilayah shell.
- **Jangan menaruh style global** di aplikasi ini. Saat dimuat di dalam shell, hanya JavaScript
  remote yang dimuat — `styles` global di `angular.json` **tidak ikut**. Katalog design system
  dimuat oleh shell. Style komponen (terkapsulasi) tetap ikut karena menempel di JavaScript.
- **Jangan hardcode warna, jangan bikin komponen visual sendiri** — pakai katalog
  `@danarakca/keu-ui` (lihat `libs/keu-ui-dummy/README.md`).
- **Jangan set `title` di rute.** Judul dokumen milik shell; kalau remote juga mengubahnya, kedua
  aplikasi saling menimpa.

## Cara kerja sinkronisasi URL (ringkas)

Shell dan remote masing-masing punya router. Keduanya sinkron dua arah dengan URL browser:

1. Remote bernavigasi → `pushState` → remote mengirim `popstate` → router shell menyelaraskan diri.
2. Shell bernavigasi → `pushState` → shell mengirim `popstate` → router remote menyelaraskan diri.
3. Navigasi yang dipicu `popstate` tidak dikabarkan ulang, jadi tidak ada pantulan.
4. `SilentLocationStrategy` membuat router remote mengabaikan URL di luar wilayahnya (mis. saat
   shell menampilkan modul lain).

Seluruh mekanisme ini **tebakan kita** atas "Silent Location Strategy" di slide — lihat
`DUMMY_REGISTRY.md`.
