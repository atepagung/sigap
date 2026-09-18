# iam-dummy-web

**DUMMY** pengganti directive `*hasPermission` platform ICS (Angular). Tercatat di
[DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md) bagian 4.

```html
<button *hasPermission="'sigap:laporan:verify'">Verifikasi</button>
```

```ts
import { HasPermissionDirective } from '@danarakca/iam';   // alias di tsconfig.base.json
```

Tes berjalan bersama tes sigap-web: `npm run test:web` dari root.

## Batas yang wajib dipahami

- **Ini hanya menyembunyikan tampilan, bukan keamanan.** Tombol yang disembunyikan tetap ditolak
  API kalau dipanggil langsung — keamanan sesungguhnya di `[KemenkeuAuthorize]` + Scope + Sieve.
- **Sumber daftar permission belum diketahui** (Lampiran E #12; PERMISSION_MAP bagian 8 no. 5).
  Sementara ini dipasang lewat satu baris di `app.config.ts`:
  ```ts
  provideIamPermissions(() => /* ambil daftar permission, mis. GET /me/konteks */)
  ```
  Baris itu yang diganti saat mekanisme platform diketahui.
- **Fail-closed:** sebelum daftar tiba, atau bila gagal dimuat, semua elemen ber-`*hasPermission`
  tersembunyi. Bootstrap aplikasi tidak ditahan.
- Kode aplikasi cukup memakai directive-nya. Hindari memanggil `IamPermissions` langsung dari
  kode fitur — itu bukan kontrak platform.

## Penukaran

Hapus entri `@danarakca/iam` di `tsconfig.base.json`, pasang paket asli, ganti baris
`provideIamPermissions` di `app.config.ts`, hapus folder ini.
