# CONTRIBUTING — SIGAP

Konvensi tim untuk repo ini. Aturan teknis rinci (routing, styling, keamanan, lintas platform)
ada di [AGENTS.md](AGENTS.md) dan [apps/sigap-web/AGENTS.md](apps/sigap-web/AGENTS.md) — berlaku
untuk manusia **dan** AI coding tool. Dokumen ini tentang *cara bekerja*.

## 1. Menyiapkan lingkungan

| Perlu | Versi | Catatan |
| --- | --- | --- |
| Node.js | 24 | npm 11 (`packageManager` di `package.json`) |
| .NET SDK | 10 | |
| Docker | terbaru | PostgreSQL, Keycloak, MinIO lokal |

```bash
npm install                                        # juga memasang hook Git (lihat di bawah)
docker compose up -d postgres
node infra/skema/terapkan.mjs --yes-development    # 33 tabel ke database dev
```

- Laptop dengan PostgreSQL native Windows: database docker dipetakan ke **port 5433**, bukan 5432.
- Keycloak lokal (SSO dummy): `node infra/keycloak/buat-env.mjs` lalu
  `docker compose --profile sso up -d`. Berkas `.env` **tidak** masuk git.
- **Hook Git.** `npm install` menjalankan `husky` yang mengatur `core.hooksPath` ke `.husky/`.
  Ini mengubah konfigurasi git lokal Anda — disengaja, sekali per clone.
  - `pre-commit`: ESLint + Prettier + Stylelint atas berkas yang di-stage, plus pemeriksa lintas
    platform. Cepat, hanya berkas yang berubah.
  - `commit-msg`: memeriksa format commit (bagian 3).

## 2. Cabang dan alur kerja

- `master` selalu hijau. Jangan push langsung; lewat pull request.
- Nama cabang baru: `tipe/deskripsi-singkat`, mis. `feat/asesmen-aspek-sdm`,
  `fix/scope-rekap-safety-check`. Tipe sama dengan tipe commit.
- Pull request **kecil dan satu tujuan**. Pekerjaan per domain (P4.4 dst.) dipecah per domain,
  bukan satu PR raksasa.
- Satu langkah PLAYBOOK = satu atau beberapa commit yang bisa dibaca sendiri-sendiri.
- Sebelum meminta review: tes hijau dan pemeriksaan lokal bersih (bagian 5).

## 3. Format commit — Conventional Commits

```
<tipe>(<scope>): <subjek>

<isi: mengapa, bukan apa>

<footer>
```

Ditegakkan `commitlint` di hook `commit-msg` dan di CI untuk setiap pull request (hook bisa
dilewati dengan `--no-verify`; CI tidak).

**Tipe**

| Tipe | Untuk |
| --- | --- |
| `feat` | fitur atau perilaku baru |
| `fix` | perbaikan bug |
| `docs` | dokumen saja |
| `refactor` | ubah struktur tanpa ubah perilaku |
| `test` | tes saja |
| `perf` | perbaikan kinerja |
| `build` | sistem build, dependensi, csproj/package.json |
| `ci` | workflow CI, hook Git |
| `chore` | pekerjaan rutin lain |
| `style` | format/spasi tanpa ubah makna |
| `revert` | membatalkan commit sebelumnya |

**Scope** (opsional, bila ada harus salah satu): `web`, `api`, `shell`, `libs`, `infra`, `skema`,
`docs`, `ci`, `deps`, `repo`.

**Aturan**

- Subjek: bahasa **Indonesia**, kalimat perintah/deskriptif singkat, **tanpa titik**, maksimal 100
  karakter untuk seluruh baris pertama. Contoh: `feat(api): tambah endpoint verifikasi laporan`.
- Isi menjelaskan **mengapa** dan keputusan yang non-obvious — bukan mengulang diff. Sebutkan
  langkah PLAYBOOK bila relevan (`P4.2`).
- Perubahan yang **merusak kompatibilitas**: tambahkan `!` setelah tipe/scope (`feat(api)!: ...`)
  dan footer `BREAKING CHANGE: <apa yang berubah dan cara migrasinya>`.
- Satu commit = satu perubahan logis. Jangan mencampur perubahan format dengan perubahan perilaku.
- Perubahan struktur tabel database **wajib** disebut di isi commit beserta persetujuannya.

```text
✔ feat(api): tambah pemetaan EF Core schema-first untuk 33 tabel
✔ fix(web): tolak nested routing di app.routes.ts
✔ ci: jalankan sigap-api hanya bila path terkait berubah
✔ docs(skema): jelaskan mengapa timestamptz dikembalikan ke TIMESTAMP(3)

✘ update                          (tanpa tipe)
✘ Feat: Tambah endpoint           (tipe harus huruf kecil)
✘ feat: tambah endpoint.          (titik di akhir)
✘ wip: setengah jadi              (tipe tidak dikenal)
```

## 4. Yang butuh persetujuan pemilik proyek sebelum dikerjakan

Laporkan dulu, jangan langsung dikerjakan:

- **Perubahan struktur tabel** (kolom, tabel, tipe, indeks) — selain dua yang sudah disetujui di
  `infra/skema/1x-*.sql`.
- **Permission baru atau perubahan Scope/Sieve.** Ubah bersama: `iam-policy.sigap.json`,
  `Izin.cs`, `PERMISSION_MAP.md`; tes `IzinTests` menjaganya.
- **Perubahan kontrak** `API_CONTRACT.md` / `PERMISSION_MAP.md`.
- **Dependensi baru** yang menduplikasi platform (UI, auth, mediator).
- **Dummy baru** atau asumsi baru tentang platform → catat di `DUMMY_REGISTRY.md`.
- **Fitur Fase 2.**

## 5. Sebelum membuka pull request

```bash
npm run check:web                                    # bila menyentuh sigap-web
dotnet format apps/sigap-api/sigap-api.slnx --verify-no-changes
dotnet test apps/sigap-api/sigap-api.slnx            # bila menyentuh sigap-api
npm run periksa:repo                                 # aturan lintas platform
```

Yang dianggap **selesai**: tes hijau, pemeriksaan di atas bersih, dan perilaku baru dijalankan
sungguhan (bukan hanya dikompilasi). Bug diperbaiki dengan tes yang gagal lebih dulu. Tes tidak
boleh bergantung pada data yang hanya ada di laptop Anda (mis. hasil seeder) — CI memakai database
kosong.

### Reproduksi CI di Linux

Development di Windows, production di Linux. Kapitalisasi nama berkas, akhir baris, dan pemisah path
adalah tiga kegagalan khas yang hanya muncul di Linux. Jalankan pipeline yang sama dengan CI di
container, atas snapshot persis dari apa yang akan di-commit:

```bash
npm run verifikasi:linux -- web       # lint, tes, build sigap-web
npm run verifikasi:linux -- api       # format, build, tes (Postgres sementara), publish-guard
npm run verifikasi:linux -- semua
```

## 6. Pipeline CI

| Workflow | Jalan bila berubah | Isi |
| --- | --- | --- |
| `sigap-web` | `apps/sigap-web/**`, `libs/keu-ui-dummy/**`, `libs/iam-dummy-web/**`, `tsconfig.base.json`, lockfile | ESLint, Stylelint, Prettier, tes, build produksi |
| `sigap-api` | `apps/sigap-api/**`, `libs/iam-dummy/**`, `libs/notifikasi*/**`, `infra/skema/**`, `API_CONTRACT.md` | `dotnet format`, build, tes + PostgreSQL, tes library, build Release, penjaga dummy |
| `repo` | **selalu** | pemeriksa lintas platform; format commit (pull request) |

Filter path sengaja mencakup **dependensi** tiap aplikasi, bukan hanya foldernya sendiri: ubahan di
`libs/keu-ui-dummy` yang merusak build sigap-web harus ketahuan di pull request-nya, bukan setelah
merge. Semua berjalan di **Linux**.

## 7. Dokumentasi yang ikut diperbarui

- Selesai satu langkah PLAYBOOK → catat di `MIGRATION_NOTES.md` bagian 5.2 (apa, di mana, bukti).
- Menambah atau mengubah dummy → `DUMMY_REGISTRY.md`.
- Keputusan yang mengikat sistem baru → `MIGRATION_NOTES.md` bagian 5.4.
- Perubahan konvensi kode → `AGENTS.md`, dan aturan lint yang sesuai bila bisa ditegakkan mesin.

## 8. Menggunakan AI coding tool

Semua tool membaca [AGENTS.md](AGENTS.md) lebih dulu. Tanggung jawab atas kode tetap di penulis
commit: baca diff-nya, jalankan tesnya. Tool tidak boleh push, menambah remote, atau mengubah
konfigurasi git. Jangan menempel secret ke prompt.

## 9. Keamanan

Jangan commit secret (kunci VAPID, kredensial, token, `.env`). Temuan kerentanan dilaporkan
langsung ke pemilik proyek, bukan lewat issue publik.
