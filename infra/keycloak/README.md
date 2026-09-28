# Keycloak lokal — dummy SSO Kemenkeu

**DUMMY** pengganti SSO Kemenkeu, sekaligus **spesifikasi hidup** dari yang kami minta ke BaTII di
`docs/Kebutuhan-Teknis-BaTII.pdf` bagian E. Tercatat di [DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md)
bagian 1.6 (asumsi di bagian 5).

> **Status:** terverifikasi pada Keycloak yang berjalan, 18 Sep 2026 — 18/18 pemeriksaan
> spesifikasi dan 13/13 uji ujung ke ujung dengan `libs/iam-dummy`. Lihat bagian "Verifikasi".

## Menjalankan

```bash
node infra/keycloak/buat-env.mjs
```

```bash
docker compose --profile sso up -d keycloak
```

- `buat-env.mjs` mengisi `.env` (tidak masuk git) dengan nilai **acak lokal**. Nilai tidak pernah
  dicetak ke layar dan tidak menimpa yang sudah ada.
- Konsol admin: `http://localhost:8081` (kredensial di `.env`).
- Issuer: `http://localhost:8081/realms/kemenkeu`.
- Realm hanya diimpor saat container **baru dibuat**. Setelah mengubah
  `import/kemenkeu-realm.json`: `docker compose --profile sso up -d --force-recreate keycloak`.

## Pemetaan ke Kebutuhan Teknis bagian E

| Kebutuhan Teknis | Diminta ke BaTII | Di dummy ini |
| --- | --- | --- |
| URL issuer | `https://sso-dev.kemenkeu.go.id/realms/kemenkeu` | `http://localhost:8081/realms/kemenkeu` — nama realm sama |
| URL discovery | issuer + `/.well-known/openid-configuration` | sama |
| Client id remote module | `sigap-web-dev` | `sigap-web-dev` — **public**, authorization code + **PKCE S256**, tanpa password grant, tanpa implicit |
| Client id microservice | `sigap-api-dev` | `sigap-api-dev` — **confidential**, secret dari `.env` (di platform: vault), hanya service account |
| Redirect URI | `https://dev.satu.kemenkeu.go.id/sigap/callback` | `http://localhost:4200/*` (shell dummy), `http://localhost:4299/*` (remote mandiri) |
| Audience token | `sigap-api` | `sigap-api` di klaim `aud` access token (audience mapper) |
| Scope | `openid profile email groups satker` | `openid profile email` bawaan; klaim `groups`/`satker` **selalu** dibawa lewat mapper di client — lihat asumsi 81 |
| Klaim nomor pegawai | `nip` atau `preferred_username` | **keduanya**, bernilai sama: username akun = NIP |
| Klaim satuan kerja | `kode_satker` | `kode_satker` (atribut pengguna) |
| Klaim unit Eselon I | `kode_eselon1` | `kode_eselon1` (atribut pengguna) |
| Klaim grup/peran | `groups` atau `realm_access.roles` | `groups`, **tanpa** awalan path (`sigap-pegawai`, bukan `/sigap-pegawai`) |
| Masa berlaku access token | 15 menit | `accessTokenLifespan: 900` |
| Masa berlaku refresh token | 8 jam | `ssoSessionIdleTimeout` & `ssoSessionMaxLifespan: 28800` |
| Sepuluh akun uji | Lampiran 1 | sepuluh akun di bawah |

Klaim yang benar-benar dibaca `libs/iam-dummy` saat ini: `nip` (cadangan `preferred_username`),
`groups`, dan `aud`. `kode_satker`/`kode_eselon1` sudah dibawa token tetapi belum dipakai; unit
pengguna masih dicari lewat NIP di tabel `"User"` (API_CONTRACT 1.2).

## Sepuluh akun uji

Username = NIP. **NIP sengaja palsu**: 18 digit berawalan `9000…` (tahun lahir 9000 mustahil),
jadi tidak mungkin sama dengan NIP pegawai sungguhan. Email berdomain `.invalid` (dicadangkan RFC
2606, tidak pernah bisa menerima surat). Satu kata sandi untuk semua akun: `SIGAP_UJI_PASSWORD` di
`.env`.

| NIP (username) | Grup | Peran | Lingkup (Lampiran B) | `kode_satker` | `kode_eselon1` |
| --- | --- | --- | --- | --- | --- |
| `900000000000000001` | `sigap-pegawai` | PEGAWAI | dirinya & unitnya | `DEMO-KPP-MADYA-PKU` | `djp` |
| `900000000000000002` | `sigap-satgas` | SATGAS | unit | `DEMO-KPP-MADYA-PKU` | `djp` |
| `900000000000000003` | `sigap-pimpinan` | PIMPINAN | unit | `DEMO-KPP-MADYA-PKU` | `djp` |
| `900000000000000004` | `sigap-perwakilan` | PERWAKILAN | provinsi (Riau), lintas Eselon I | `DEMO-KANWIL-RIAU` | `setjen` |
| `900000000000000005` | `sigap-subkoordinator` | SUBKOORDINATOR | **Eselon I** (DJP) | `djp` | `djp` |
| `900000000000000006` | `sigap-koordinator` | KOORDINATOR | **nasional** | `setjen.biro-organisasi-dan-ketatalaksanaan` | `setjen` |
| `900000000000000007` | `sigap-sekjen` | SEKJEN | nasional | `setjen` | `setjen` |
| `900000000000000008` | `sigap-admin` | ADMIN | nasional, data sistem — **tanpa permission bisnis** | `DEMO-KANWIL-RIAU` | `setjen` |
| `900000000000000009` | `sigap-pengembang` | PENGEMBANG | **Fase 2** — didaftarkan saja | `DEMO-KPP-MADYA-PKU` | `djp` |
| `900000000000000010` | `sigap-impl-rkb` | IMPL_RKB | **Fase 2** — didaftarkan saja | `DEMO-KPP-MADYA-PKU` | `djp` |

- **Lingkup Subkoordinator = Eselon I dan Koordinator MKB = nasional**, mengikuti Dokumen UR dan
  PLAYBOOK Lampiran B. Lampiran 1 Kebutuhan Teknis menulis sebaliknya dan itu **keliru** — koreksi
  ini perlu disampaikan saat pendaftaran grup.
- Dua akun Fase 2 hanya punya grup. Mereka tidak mendapat permission apa pun karena
  `apps/sigap-api/iam-policy.sigap.json` tidak memberi grup itu permission — bukan karena Keycloak.
  Keycloak memang tidak tahu permission; ia hanya membawa grup.
- Penempatan unit mengikuti seed prototipe (`prisma/seed.ts`). Seeder sigap-api (P3.5/P4) wajib
  membuat baris `"User"` untuk kesepuluh NIP ini, kalau tidak lingkup datanya kosong (fail-closed).

## Client ketiga: `sigap-uji-lokal` — BUKAN bagian permintaan ke BaTII

Public client yang mengizinkan **password grant**, hanya supaya skrip dan tes bisa mengambil token
akun uji tanpa browser. Klaimnya sama persis dengan `sigap-web-dev`. Password grant sengaja
**tidak** diaktifkan di `sigap-web-dev`, karena client remote module di platform asli tidak akan
mengizinkannya. Jangan pernah meminta client seperti ini ke BaTII.

## Verifikasi

Dengan Keycloak berjalan. Kedua skrip membaca kredensial dari `.env` dan tidak pernah mencetak
kredensial maupun token.

```bash
node infra/keycloak/verifikasi.mjs
```

```bash
dotnet run infra/keycloak/uji-ujung-ke-ujung.cs
```

**`verifikasi.mjs` — spesifikasi (18 pemeriksaan).** Untuk kesepuluh akun: `nip` =
`preferred_username` = NIP, `kode_satker`, `kode_eselon1`, `groups` berisi tepat satu grup tanpa
`/`, `aud` = `["sigap-api"]`, access token 900 detik, refresh token 28800 detik, issuer benar. Juga
membuktikan penolakan yang disyaratkan: kata sandi salah, password grant di `sigap-web-dev`,
permintaan otorisasi tanpa PKCE, redirect ke situs lain, dan secret `sigap-api-dev` yang salah.

**`uji-ujung-ke-ujung.cs` — token asli ke `libs/iam-dummy` (13 pemeriksaan).** Token divalidasi
lewat JWKS Keycloak, bukan kunci uji. Hasil per peran (18 Sep 2026):

| Akun | Permission | Verifikasi laporan | Monitor |
| --- | --- | --- | --- |
| Pegawai | 10 | 403 | 403 |
| Satgas | 17 | 200 | 403 |
| Pimpinan | 9 | 403 | 403 |
| Perwakilan, Subkoordinator, Koordinator | 9 | 403 | 200 |
| Sekjen | 6 (semuanya baca) | 403 | 200 |
| Admin, Pengembang, Impl. RKB | 0 | 403 | 403 |

Jumlah permission cocok dengan matriks PERMISSION_MAP bagian 4. Tanpa token, token client lain,
dan token yang tanda tangannya dirusak semuanya ditolak 401.

Catatan temuan: token service account `sigap-api-dev` ditolak sigap-api (401), karena `aud`-nya
bukan `sigap-api`. Benar untuk sekarang, tetapi perlu diputuskan kalau kelak microservice memanggil
dirinya atau layanan lain dengan token itu — lihat asumsi 86.
