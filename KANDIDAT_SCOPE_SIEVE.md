# KANDIDAT_SCOPE_SIEVE — Scope dan Sieve dilihat dari 33 tabel

Disusun 21 September 2026 (P4.2) dari struktur tabel yang sesungguhnya — `infra/skema/`, yang
kini terpasang di PostgreSQL dev dan dijaga tes `SkemaTests` — lalu dibandingkan dengan
[PERMISSION_MAP.md](PERMISSION_MAP.md) bagian 2 (Scope) dan 6 (Sieve) serta
[iam-policy.sigap.json](apps/sigap-api/iam-policy.sigap.json).

**Dokumen ini berisi kandidat, bukan keputusan.** Keputusan tetap tinggal di PERMISSION_MAP dan
berkas kebijakan. Butir bertanda ⚖ perlu diputuskan pemilik proyek atau pemilik proses bisnis
sebelum endpoint yang bersangkutan dibangun.

Istilah: **Scope** = baris yang boleh dibaca, disaring di klausa `WHERE`. **Sieve** = kolom yang
di-`null`-kan untuk sebagian peran. **Tidak diproyeksikan** = kolom yang tidak pernah boleh keluar
di respons mana pun, untuk siapa pun — lebih keras daripada Sieve.

---

## Ringkasan

**Scope.** Kolom-kolom di PERMISSION_MAP bagian 2.2 semuanya ada persis di skema, dengan nama dan
tipe yang benar. Tidak ada yang keliru; yang ada adalah celah di tabel yang tidak tercakup dan
satu aturan pemasangan yang tidak tertulis di mana pun (S5).

**Sieve.** Kelima kunci di PERMISSION_MAP bagian 6 tepat sasaran. Kandidat baru terpenting:

1. **NIP pegawai** (V1) mengalir ke peran pemantau dan ke sesama pegawai lewat `RingkasPengguna`,
   padahal NIP 18 digit menyandikan tanggal lahir dan jenis kelamin. Tidak ada Sieve untuknya.
2. **Keberadaan pegawai** (V2): kolom `"kehadiran"` masih ada di tabel walau tidak lagi dipakai
   sejak koreksi 2. Kontrak tidak mengeluarkannya — dan itu harus dijaga tetap begitu.

---

## 1. Kandidat Scope

### 1.1 Kolom penyaring per tabel

`{unit}` dan `{pemilik}` mengikuti istilah PERMISSION_MAP bagian 2.2. Kolom **ditebalkan** bila
belum tercantum di sana.

| Tabel | `{unit}` | `{pemilik}` | Di PERMISSION_MAP | Catatan |
|---|---|---|---|---|
| `"SafetyCheckResponse"` | `"unitId"` | `"userId"` | ✓ | lihat S3 |
| `"DisasterAlert"` | `"unitId"` | `"pelaporId"` | ✓ | |
| `"DamageAssessment"` | `"unitId"` | — | ✓ | lihat S5 |
| `"ChecklistKondisiLapangan"` | `"unitId"` | — | ✓ | lihat S5 |
| `"DisasterDeclaration"` | `"unitId"` | — | ✓ | |
| `"LayananKritis"` | `"unitId"` | — | ✓ | |
| `"GangguanLayanan"` | lewat `"LayananKritis"."unitId"` | — | ✓ | join `"layananId"` |
| `"BroadcastSasaranUnit"` | `"unitId"` | — | ✓ | tabel ke-33 |
| `"ActiveBroadcast"` | lewat `"BroadcastSasaranUnit"` | `"dikirimOlehId"` | ✓ | lihat S4 |
| `"Attachment"` | `IKUT_INDUK`: `"disasterAlertId"` / `"damageAssessmentId"` / `"checklistId"` | — | ✓ | |
| `"User"` | `"unitId"` | `"id"` | ✓ | |
| `"Unit"` | `"id"` | — | ✓ | `"provinsi"`, `"eselonIKey"` = dasar WILAYAH/ESELON_I |
| `"LanggananPush"` | — | **`"userId"`** | ✗ | lihat S2 |
| `"KantorBmn"` | **`"unitId"` (kosong semua)** | — | ✗ | lihat S1 |
| `"KejadianManual"` | — | — | ✗ | lihat S8 |
| `"JejakPerubahan"` | lewat induk `("entitas", "entitasId")` | `"olehId"` | ✗ | lihat S7 |
| `"KirimanPush"` | — | — | — | internal, tanpa endpoint |
| `"PemulihanLogEntry"` | `"unitId"` | `"petugasId"` | — | tanpa endpoint Fase 1 |
| `"BroadcastRequest"` | `"requestedByUnitId"` | `"requestedById"` | — | tidak dipakai (API_CONTRACT bagian 8) |
| `"UserRole"` | lewat `"User"` | `"userId"` | — | peran dibaca dari token, bukan tabel ini |
| 13 tabel Fase 2 | lihat S9 | | — | di luar cakupan |

### 1.2 Temuan

**S1 ⚖ — `"KantorBmn"` belum dapat disaring per unit maupun per Eselon I.**
Seluruh 1.431 baris ber-`"unitId"` kosong (seeder P3.5 sengaja tidak mencocokkan gedung ke unit).
Kolom `"eselon1"` berisi **nama** ("Sekretariat Jenderal"), bukan kunci seperti
`"Unit"."eselonIKey"` ("setjen"), sehingga profil `ESELON_I` tidak dapat memakainya. Yang tersisa
hanya `"provinsi"` — dan kecocokan ejaannya dengan `"Unit"."provinsi"` belum dapat diperiksa
karena tabel `"Unit"` masih kosong. Belum ada endpoint Fase 1 yang membaca tabel ini; keputusan
ini harus diambil sebelum ada yang dibuat (mis. peta sebaran gedung terdampak).

**S2 — `"LanggananPush"."userId"` belum tercantum sebagai `{pemilik}`.**
PERMISSION_MAP bagian 3 butir 23 sudah menyatakan "hanya langganan milik sendiri", tetapi tabel
kolom di bagian 2.2 tidak memuat barisnya. Usul: tambahkan `"LanggananPush"` dengan
`{pemilik}` = `"userId"`, profil `SELF`. Hapus langganan (#45) juga wajib memakai predikat ini,
bukan hanya mencocokkan `"endpoint"`.

**S3 — Dua kolom unit untuk satu pegawai.**
`"SafetyCheckResponse"."unitId"` mencatat unit **saat menjawab**, sedangkan `"User"."unitId"`
adalah unit **sekarang**. Keduanya berbeda begitu pegawai dimutasi. Usul: baris jawaban disaring
dengan kolom milik tabel itu sendiri (sesuai PERMISSION_MAP), sedangkan penyebut rekap
("pegawai aktif berperan Pegawai Umum di unit itu", API_CONTRACT #4) memakai `"User"."unitId"`.
Aturan ini perlu ditulis eksplisit di use case P4.4 supaya tidak tertukar.

**S4 — Kolom sasaran di `"ActiveBroadcast"` bukan kolom Scope.**
`"targetUnitId"`, `"targetEselonIKey"`, `"targetKabkota"`, dan `"wilayah"` hanya menyimpan
**kriteria** saat dipicu. Menyaring dengannya menghasilkan jawaban yang bergeser setiap kali data
provinsi unit dilengkapi — alasan tabel ke-33 dibuat. PERMISSION_MAP sudah benar memakai
`"BroadcastSasaranUnit"`; catatan ini agar tidak ada yang tergoda memakai jalan pintas.

**S5 ⚖ — Dua separuh asesmen tidak punya kunci penghubung.**
Satu asesmen tersimpan di `"DamageAssessment"` dan `"ChecklistKondisiLapangan"` (API_CONTRACT
bagian 3.5.3), tetapi tidak ada foreign key maupun pengenal bersama di antara keduanya. Prototipe
memasangkannya dengan mengambil baris **terbaru** dari masing-masing tabel secara terpisah
(`deklarasi/page.tsx`). Itu rapuh begitu ada versi berurutan atau salah satu separuh dibatalkan.

Usul tanpa mengubah skema: tulis kedua baris dalam **satu transaksi** dan biarkan `"createdAt"`
diisi `DEFAULT CURRENT_TIMESTAMP` oleh database. PostgreSQL mengembalikan waktu **awal
transaksi** untuk `CURRENT_TIMESTAMP`, sehingga kedua baris dijamin ber-`"createdAt"` identik.
Pasangan lalu dicari dengan `("unitId", "submittedById", "createdAt")`. Konsekuensinya untuk
Scope: kedua separuh wajib disaring dengan permission dan predikat yang sama, dan pembatalan
harus menandai keduanya bersamaan.

**S6 ⚖ — `"isDemo"` dan `"dibatalkan"` bukan Scope, tetapi tetap penyaring baris.**
Keduanya ada di hampir semua tabel. `"dibatalkan"` punya arti bisnis yang dibaca kontrak
(mis. 409 `ASESMEN_DIBATALKAN`), jadi tidak boleh dijadikan filter global. `"isDemo"` menandai
data simulasi UAT prototipe; perlu diputuskan apakah baris itu boleh terbaca di lingkungan
produksi, atau cukup dipastikan tidak pernah ikut termigrasi.

**S7 — `"JejakPerubahan"` tidak boleh terbaca lintas lingkup.**
Belum ada endpoint yang membacanya. Bila kelak ada, lingkupnya harus `IKUT_INDUK` lewat
`("entitas", "entitasId")`, sebab `"ringkasan"` memuat nilai sebelum/sesudah — termasuk isi
kolom yang di-Sieve di tempat asalnya.

**S8 — `"KejadianManual"` adalah data rujukan nasional.**
Hanya punya `"provinsi"`, tanpa unit. Butir 1.1 bersifat baca-saja bagi seluruh peran, jadi tidak
perlu Scope baca. Siapa yang boleh menulis masih menunggu API_CONTRACT bagian 9 butir 10.

**S9 — Fase 2 sudah siap dari sisi skema.**
Semua tabel Fase 2 punya `"unitId"`, kecuali `"StandarPengendalian"` (`"eselonIKey"`, cocok
langsung dengan `ESELON_I`) serta `"AsetKritis"` dan `"TemplatePesanKunci"` (lewat
`"risikoId"` → `"RisikoBencana"."unitId"`). Tidak ada kebutuhan perubahan skema untuk Scope.

---

## 2. Kandidat Sieve

### 2.1 Yang sudah ada di PERMISSION_MAP, dicocokkan ke kolomnya

| Kunci Sieve | Kolom sumber | Terlihat untuk |
|---|---|---|
| `safety-check.rekap.lokasiTerakhir` | `"SafetyCheckResponse"."lat"`, `"lng"` | SATGAS |
| `safety-check.rekap.keterangan` | `"SafetyCheckResponse"."keterangan"` | SATGAS, PIMPINAN |
| `safety-check.rekap.dicatatOleh` | `"SafetyCheckResponse"."dicatatOlehId"` → `"User"` | SATGAS, PIMPINAN |
| `asesmen.sdm.catatanKondisiPegawai` | `"DamageAssessment"."catatanPegawai"` | SATGAS, PIMPINAN |
| `asesmen.sdm.catatanTambahan` | `"ChecklistKondisiLapangan"."sdmCatatan"` | SATGAS, PIMPINAN |

Kelimanya tepat, dan kolom sumbernya ada persis seperti yang diandaikan.

### 2.2 Kandidat baru

**V1 ⚖ — NIP (dan jabatan) di `RingkasPengguna`.**
`RingkasPengguna` (API_CONTRACT, potongan objek berulang) membawa `nip` dan `jabatan` dari
`"User"`. Objek itu muncul di:

| Endpoint | Siapa yang membaca | NIP siapa |
|---|---|---|
| #4 rekap safety check | PEGAWAI, PIMPINAN, SATGAS | seluruh rekan satu unit |
| #14, #15 broadcast (`pemicu.pengguna`) | SATGAS, PERWAKILAN, SUBKOORDINATOR, KOORDINATOR | pemicu dari unit lain |
| #24–#26 asesmen (`dikirimOleh`, `persetujuan.disetujuiOleh`) | termasuk PERWAKILAN, SUBKOORDINATOR, KOORDINATOR, SEKJEN | Satgas dan Pimpinan unit lain |

NIP 18 digit menyandikan tanggal lahir (8 digit pertama) dan jenis kelamin (digit ke-15). Usul:
kunci Sieve baru `pengguna.nip`, terlihat untuk SATGAS dan PIMPINAN, `null` untuk peran lain.
Nama tetap terlihat — nama diperlukan untuk menghubungi, NIP tidak. Bila NIP dipakai Angular
sebagai pengenal, pakai `id`.

**V2 — Keberadaan pegawai (`"SafetyCheckResponse"."kehadiran"`).**
WFO/WFH/CUTI/DINAS_LUAR menjelaskan di mana seseorang berada. Formulir berhenti mengisinya sejak
koreksi 2, dan **tidak ada satu pun respons kontrak yang memuatnya**. Aturan yang diusulkan:
*tidak diproyeksikan*. Bila kelak diperlukan, ia diperlakukan setingkat `keterangan`
(SATGAS, PIMPINAN), karena memberi tahu kapan rumah seseorang kosong.

Keberadaan dalam arti lokasi sudah tertangani: `lat`/`lng` hanya terlihat SATGAS lewat
`lokasiTerakhir`, dan endpoint pemantau (#30–#35) dirancang tanpa koordinat pegawai sama sekali.

**V3 — Koordinat kantor bukan data pribadi, tetapi yang dummy wajib diberi label.**
`"Unit"."lintang"`/`"bujur"` dan `"KantorBmn"."lintang"`/`"bujur"` adalah lokasi gedung negara,
bukan lokasi orang, jadi tidak perlu Sieve. Namun seluruh koordinat `"KantorBmn"` saat ini hasil
generate. Setiap respons yang memuatnya **wajib** menyertakan `isKoordinatDummy` — koordinat
palsu yang terbaca sebagai asli bisa mengirim tim ke lokasi yang salah (DUMMY_REGISTRY 1.7).

**V4 ⚖ — Uraian kondisi bencana dapat memuat nama.**
`"DamageAssessment"."deskripsi"` (`kondisiBencana.uraian`) adalah teks bebas yang terbaca oleh
pemantau. Contoh di kontrak aman ("Retak pada dinding lantai 2…"), tetapi tidak ada yang mencegah
Satgas menulis "Pak Budi terjepit". Pilihannya: Sieve seperti `catatanKondisiPegawai`, atau
petunjuk di formulir bahwa nama pegawai ditulis di kolom catatan SDM, bukan di uraian.

**V5 — Kategori SDM tingkat unit dapat mengenali orang di unit kecil.**
`"sdmKorban"`, `"sdmFisik"`, `"sdmPsikis"` ("Ada Luka Berat", "Trauma Berat") terbaca pemantau.
Di unit berpegawai lima orang, kategori itu nyaris menunjuk satu nama. Usul: **tetap terlihat**,
karena justru itu yang dibutuhkan pemantau untuk memutuskan bantuan; dicatat sebagai risiko yang
disadari.

**V6 ⚖ — Nilai fiskal.**
`"KantorBmn"."nilaiBuku"`, `"nilaiPerolehan"`, dan `"LayananKritis"."nilaiHarianRupiah"` tidak
muncul di respons Fase 1 mana pun (objek layanan #19 hanya memuat nama dan RTO). Aturan yang
diusulkan: tidak diproyeksikan di Fase 1; putuskan siapa yang boleh melihatnya sebelum fitur
paparan aset (Fase 2) dibangun.

**V7 — Tidak pernah diproyeksikan, untuk siapa pun.**

| Kolom | Alasan |
|---|---|
| `"User"."passwordHash"` | sisa login sementara prototipe; login sepenuhnya urusan SSO |
| `"LanggananPush"."p256dh"`, `"auth"`, `"endpoint"` | kunci perangkat; bocor berarti orang lain dapat mengirim push atas nama SIGAP |
| `"Attachment"."storageKey"`, `"url"` | kontrak mewajibkan `url` selalu path API ber-autentikasi (#11), tidak pernah alamat object storage |
| `"User"."email"` | tidak ada di `RingkasPengguna`, dan tidak diperlukan di mana pun |

Ini bukan Sieve per peran, melainkan aturan proyeksi DTO. Diperiksa saat audit P6.3.

**V8 — Status per rekan kerja terlihat oleh sesama pegawai.**
Lewat #4, PEGAWAI melihat siapa di unitnya yang `BUTUH_BANTUAN`. Itu sesuai matriks 2.1.3
("Read Only" bagi Pegawai) dan berguna untuk saling menolong; dicatat karena statusnya dekat
dengan informasi kesehatan. Tidak diusulkan perubahan.

**V9 — Fase 2: `"AnggotaCallTree"."kontak"`** adalah nomor telepon pribadi anggota tim.
Perlu aturan Sieve saat modul RPKK dibangun.

---

## 3. Selisih terhadap PERMISSION_MAP

| # | Bagian | Usul | Perlu keputusan |
|---|---|---|---|
| S2 | 2.2 | Tambah `"LanggananPush"`, `{pemilik}` = `"userId"` | — (melengkapi yang sudah tertulis di bagian 3) |
| S3 | 2.2 | Tulis aturan: baris jawaban pakai `"SafetyCheckResponse"."unitId"`, penyebut pakai `"User"."unitId"` | — |
| S5 | 2.2 / API_CONTRACT 3.5.3 | Aturan pasangan dua separuh asesmen `("unitId", "submittedById", "createdAt")` | ⚖ pemilik proyek |
| S1 | 2.2 | Cara menyaring `"KantorBmn"` sebelum ada endpoint yang membacanya | ⚖ pemilik proyek |
| S6 | — | Perlakuan `"isDemo"` di produksi | ⚖ pemilik proyek |
| V1 | 6 | Kunci Sieve `pengguna.nip`: SATGAS, PIMPINAN | ⚖ pemilik proses bisnis |
| V4 | 6 | Sieve `kondisiBencana.uraian`, atau petunjuk formulir | ⚖ pemilik proses bisnis |
| V2, V6, V7 | 6 | Daftar "tidak diproyeksikan" | — |

Tidak ada usul yang memerlukan perubahan struktur tabel.

---

## Catatan samping

Contoh `RingkasPengguna` di API_CONTRACT memakai nama dan NIP yang tampak seperti milik orang
sungguhan. Bila memang demikian, lebih aman diganti dengan nilai yang jelas palsu, seperti NIP uji
`9000…` di akun Keycloak dummy — dokumen kontrak akan beredar ke BaTII dan tim lain.
