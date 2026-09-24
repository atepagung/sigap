# skenario-seed

Skenario UAT untuk PostgreSQL **development** supaya dashboard dan alur Pimpinan punya isi: gempa
bumi M 6.4 Kab. Pasaman, KPP Madya Pekanbaru sebagai unit terdampak (mengikuti prototipe yang sudah
disetujui). Semua baris `isDemo = true`, id `skenario-*`, tanpa perubahan skema.

```bash
node infra/organisasi-seed/seed.mjs --yes-development   # prasyarat: unit, akun uji, pegawai
node infra/skenario-seed/seed.mjs --yes-development
```

## Isinya

| Tabel | Baris | Catatan |
| --- | --- | --- |
| `"ActiveBroadcast"` + `"BroadcastSasaranUnit"` | 1 + 1 | Gempa Bumi, dipicu Satgas 3 jam lalu, sasaran `UNIT` = KPP Madya (`DISASAR`) |
| `"SafetyCheckResponse"` | 10 | 8 Aman, 2 Butuh Bantuan (dicatatkan Satgas, berketerangan). **3 dari 13 pegawai sengaja belum menjawab**, termasuk akun uji Pegawai Umum `900000000000000001`, supaya "Safety Check Saya" bisa dicoba |
| `"DamageAssessment"` + `"ChecklistKondisiLapangan"` | 2 + 2 | Dua versi dari Satgas dalam satu seri (v1 Minor, v2 Berat). Tiap versi = sepasang baris dengan `"createdAt"` **sama persis** (aturan pasangan S5) |
| `"JejakPerubahan"` | 1 | Baris audit `DIPICU` (`SATGAS|UNIT|<unit>`) yang dibaca API untuk peran dan lingkup pemicu; tanpanya API menampilkan `"?"` |
| `"LayananKritis"` | 3 | RTO 8, 4, dan 24 jam |
| `"GangguanLayanan"` | 1 | Sistem Informasi Perpajakan Kantor `TERGANGGU` sejak 90 menit lalu (RTO 4 jam, sisa ±2,5 jam) |

**Tanpa deklarasi tanggap darurat**, jadi seri berstatus `MENUNGGU_PIMPINAN`. Itu disengaja: login
sebagai Pimpinan (`900000000000000003`) lalu setujui asesmen untuk mencoba alurnya.

## Menjalankan ulang

Skenario dibuang lalu dibuat ulang dengan waktu segar relatif terhadap sekarang (RTO dan "3 jam lalu"
ikut bergeser). Yang ikut dibuang: **seluruh** `"DamageAssessment"`, `"ChecklistKondisiLapangan"`, dan
`"DisasterDeclaration"` milik KPP Madya, termasuk yang dibuat lewat API. Bila deklarasi lama dibiarkan,
persetujuannya akan menempel pada seri asesmen yang baru.

## Nilai berskala

Kolom `"ChecklistKondisiLapangan"` memakai **label** tersimpan (`Rusak Ringan`), bukan kode API, persis
seperti yang ditulis `AsesmenStore`. `bangun.spec.mjs` mencocokkan setiap label dengan
`Sigap.Domain/Asesmen/OpsiAsesmen.cs` sehingga seeder tidak bisa menulis nilai yang tak dikenal aplikasi.

## Penjaga "tidak pernah di production"

`NODE_ENV` bukan `production`, host `DATABASE_URL` host development yang dikenal, `--yes-development` wajib.

## Tes

```bash
node --test infra/skenario-seed/bangun.spec.mjs
```
