# shell-dummy

**DUMMY** pengganti sementara shell ICS Keuangan (`apbn.web` / "Lobi"). Tidak boleh naik ke
production. Tercatat di [DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md).

```bash
npm run start:shell   # dari root repo, port 4200
```

## Aturan emas (dari slide arsitektur ICS)

Shell **sangat ringan** dan **hanya** mengurus tiga hal. **Dilarang ada logika bisnis** di sini —
tidak ada nama fitur SIGAP, tidak ada role, tidak ada aturan MKB.

| Urusan | Di mana |
| --- | --- |
| Sidebar | `src/app/app.ts` — disusun otomatis dari registry, satu entri per remote |
| Otentikasi | `src/auth/shell-auth.ts` — **baru titik sambungnya**. Login OIDC ke Keycloak lokal dikerjakan di P3.4 |
| Routing | `src/app/app.routes.ts` — satu rute per remote, mencocokkan route path beserta seluruh sub-path-nya; sisanya urusan router remote |

## Cara remote dimuat

1. `src/registry/remote-registry.ts` membaca `apps/sigap-web/remote-identity.json` — satu-satunya
   sumber identitas remote — dan menyusun manifest Native Federation.
2. Saat rute remote dibuka (lazy), `src/app/remote-host.ts` memanggil `loadRemoteModule`, lalu
   fungsi define remote, lalu memasang custom element-nya.
3. Kalau remote gagal dimuat, hanya area konten yang menampilkan pesan; shell tetap hidup
   ("gangguan terlokalisasi").

## Yang sengaja TIDAK ditiru

- **Registry sisi server & kill-switch.** Di platform asli, daftar remote diatur lewat konfigurasi
  server dan modul bisa dimatikan tanpa deploy ulang. Di sini registry disusun saat build.
- **Penyerahan token ke remote.** Belum diketahui caranya (Lampiran E #12). Shell dummy tidak
  menyerahkan apa pun, dan remote tidak boleh mengarang cara mengambilnya.
- **Design system milik shell.** Shell dummy memuat katalog `libs/keu-ui-dummy` secara global —
  asumsi bahwa shell asli juga menyediakan katalog untuk semua remote.
