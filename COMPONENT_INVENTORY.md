# COMPONENT_INVENTORY

Inventarisasi komponen React (`.tsx`) di prototipe Next.js (`C:\dev\MKB APPS\App\src\components\`),
untuk menentukan apa yang perlu dibangun ulang di `apps/sigap-web` (Angular) dan apa yang tidak.
**Tidak ada kode Angular di dokumen ini** — ini murni analisis, ditulis untuk P2.2 di
`docs/PLAYBOOK.md`.

**Jumlah sebenarnya: 76 file**, bukan 69 seperti disebut di PLAYBOOK (71 di root `components/` +
5 di `components/visual/`) — sudah dicatat sebagai selisih di [MIGRATION_NOTES.md](MIGRATION_NOTES.md)
bagian 5.3. Semua 76 file dicek; tidak ada file `.tsx` lain di luar daftar ini.

**Cara membaca klasifikasi di bawah:** dua faktor dipakai bersamaan, bukan cuma "duplikatif":
1. **Sifat komponennya** — generik (styling/interaksi tanpa logika domain) vs spesifik SIGAP
   (logika/tampilan yang cuma masuk akal untuk MKB).
2. **Status scope Fase 1** (lihat [MIGRATION_NOTES.md](MIGRATION_NOTES.md) bagian 1.3) — kalau
   halaman pemakainya di luar scope Fase 1 (ARKB/ADB/SKB/RTDB/RKBU/RPKK, LPKB, eksekusi-RKB,
   kepatuhan, dokumen MKB, panduan, dll.), komponennya **tidak perlu dibangun sekarang** — ini beda
   dari "duplikatif", jadi ditandai terpisah di kelompok (c).

---

## 1. Tabel inventarisasi lengkap (76 komponen)

| Komponen | Fungsi | Halaman pemakai | Jumlah pemakaian | Catatan |
| --- | --- | --- | --- | --- |
| AdminPurge | Konfirmasi teks hapus semua data simulasi | admin | 1 | Utilitas demo/testing prototipe |
| AdminPurgeTotal | Konfirmasi teks hapus semua data operasional non-simulasi | admin | 1 | Utilitas demo/testing prototipe |
| AksesDitolak | Kartu "bukan kewenangan Anda" + daftar peran berhak | 30 halaman berproteksi peran | 30 | Dipakai hampir di semua halaman |
| AksiCatatan | Batal/pulihkan satu catatan (koreksi audit) | asesmen, deklarasi, log-pemulihan, status-layanan, lapor | 6 | +1 dipakai oleh RiwayatDeklarasi |
| AksiRilis | Setuju/kembalikan/hapus draf rilis komunikasi | rilis-komunikasi | 1 | Fitur komunikasi kebencanaan — out-of-scope |
| AksiSimulasi | Laporkan hasil/batalkan jadwal simulasi | simulasi | 1 | Jadwal simulasi — out-of-scope |
| AturPush | Aktif/nonaktifkan push notification perangkat | (dashboard) beranda | 1 | - |
| BadgeLingkup | Penanda cakupan data (nasional/eselon I/wilayah/unit) | 24 halaman | 24 | - |
| BroadcastPopup | Modal safety check saat broadcast bencana aktif | - | 1 | Hanya dipakai oleh Shell |
| CatatKeadaanPegawai | Satgas mencatat keadaan pegawai yang tak merespons sendiri | - | 1 | Hanya dipakai oleh RekapSafetyCheck |
| ChecklistEksekusi | Checklist eksekusi RKB per layanan terdampak | eksekusi-rkb | 1 | Eksekusi RKB — out-of-scope |
| DasborAspek | Dasbor agregat 5 aspek asesmen | monitor-sumberdaya | 1 | - |
| DeklarasiForm | Tombol deklarasi darurat baru + tombol nyatakan pulih | deklarasi | 2 | Export `DeclareButton` **tidak terpakai** (dead code); hanya `ResolveButton` yang jalan |
| DeklarasiWilayah | Kepala Perwakilan menyatakan darurat unit bawahan | monitor-sumberdaya | 1 | - |
| DocApprovalRow | Setujui/kembalikan status dokumen MKB | dokumen, dokumen/[kode] | 2 | Modul dokumen MKB — out-of-scope |
| FilterKatalog | Filter server-side katalog gedung kantor | katalog-kantor | 1 | Katalog kantor bukan bagian 2.1–2.6 — out-of-scope Fase 1 |
| FilterUnit | Filter struktur organisasi bertingkat | admin/unit | 1 | - |
| FiturDitutup | Kartu "fitur belum dibuka pada rilis ini" | ditutup | 1 | Infra feature-flag (P1.2 MODE_MATRIKS) |
| FormAdb | Kuesioner ADB dengan hitung RTO/MTPD | adb | 1 | ADB — out-of-scope |
| FormAjukanDokumen | Ajukan dokumen MKB ke Pimpinan Satker | dokumen | 1 | Modul dokumen MKB — out-of-scope |
| FormAksi | Pembungkus generik `<form>` + tombol submit + hasil | 22 halaman | 22 | +12 dipakai komponen lain |
| FormGangguan | Lapor gangguan layanan kritis | status-layanan | 1 | Kemungkinan duplikat Aspek Layanan Terdampak (koreksi #7) — perlu konfirmasi |
| FormLpkb | Susun LPKB (draf & ajukan) | lpkb | 1 | LPKB — out-of-scope |
| FormPengguna | Tambah/ubah pengguna + peran | admin/pengguna | 1 | Cek dulu: mungkin sudah disediakan IAM platform |
| FormPenyaring | Pembungkus form GET yang menjaga parameter tab | monitor-sumberdaya | 1 | - |
| FormRisiko | Pemetaan risiko ARKB dengan hitung level real-time | arkb | 1 | ARKB — out-of-scope |
| FormStrategi | Isi strategi pemulihan layanan kritis (SKB) | skb | 1 | SKB — out-of-scope |
| FormUnit | Tambah/ubah unit organisasi | admin/unit | 1 | - |
| HapusCallTree | Hapus satu orang dari call tree unit | rpkk | 1 | RPKK — out-of-scope |
| HapusRtdb | Hapus baris grab list/nomor darurat | rtdb | 1 | RTDB — out-of-scope |
| KelolaKejadian | Catat kejadian bencana manual tambahan (Sekjen) | data-bencana | 1 | Bagian 1.1 — in-scope |
| LihatLpkb | Buka isi satu LPKB tanpa pindah halaman | lpkb | 1 | LPKB — out-of-scope |
| LoginForm | Form login + kartu pilih peran akun demo | login | 1 | Digantikan SSO platform — tidak diporting |
| Lonceng | Ikon lonceng notifikasi topbar | - | 1 | Hanya dipakai Shell; kemungkinan besar bagian topbar platform |
| NomorDarurat | Kartu nomor telepon darurat nasional | lapor, safety-check | 2 | - |
| Notis | Penampil hasil aksi + hook `useAksi` | - | 23 | Tidak dipakai langsung dari halaman; 23 komponen lain memakainya |
| Paginasi | Navigasi halaman generik (2 varian export: `PaginasiTabel`, `PaginasiDaftar`) | 28+ halaman (adb, admin, admin/pengguna, admin/unit, arkb, asesmen, data-bencana, dokumen/[kode], info-bmkg, jadwal-simulasi, katalog-kantor, kepatuhan, lapor, log-pemulihan, lpkb, monitor-sumberdaya, monitor-wilayah, nasional, rilis-komunikasi, rkbu, rpkk, rtdb, safety-check, simulasi, skb, standar-pengendalian, status-aset, status-layanan, trigger-safety-check, verifikasi) | 31+ | Komponen paling sering dipakai ulang di seluruh prototipe — data awal dari sub-agen riset salah, sudah diverifikasi ulang manual |
| PanduanList | Daftar panduan darurat offline | panduan | 1 | Modul Panduan & Dok. MKB — out-of-scope |
| PanelAset | Kelola aset kritis terdampak dalam satu risiko ARKB | arkb | 1 | ARKB — out-of-scope |
| PanelBroadcast | Status safety check nasional yang berjalan | info-bmkg | 1 | Bagian alur 1 — in-scope |
| PanelEksekutif | Dasbor tingkat kementerian untuk Sekjen | (dashboard) beranda | 1 | - |
| PanelKoreksi | Panel tersembunyi berisi form koreksi generik | log-pemulihan, lapor | 2 | Pola generik reusable |
| PanelStatusAset | Tabel status aset unit + form update | monitor-sumberdaya | 1 | - |
| PemicuOtomatis | Komponen tak-terlihat pemicu cek BMKG otomatis | info-bmkg | 1 | Bagian alur 1 — in-scope, logika bisnis inti |
| PemulihanLogForm | Tambah catatan log pemulihan | log-pemulihan | 1 | Pasca-bencana — out-of-scope |
| PenilaianLpkb | Keputusan Pimpinan atas LPKB | lpkb | 1 | LPKB — out-of-scope |
| PenyediaSesi | Pembungkus `SessionProvider` next-auth | app/layout | 1 | Digantikan SSO platform — tidak diporting |
| PetaBencana | Peta SVG sebaran kejadian bencana per provinsi | data-bencana | 1 | Bagian 1.1 — in-scope |
| PetunjukPasang | Ajakan pasang PWA ke layar utama | - | 1 | Hanya dipakai LoginForm; terkait auth prototipe — tidak diporting |
| PilihBencana | 3 sub-form: jenis bencana, level kerusakan, lokasi | asesmen, trigger-safety-check, lapor | 4 | +1 dipakai DeklarasiWilayah |
| PilihLayananTerdampak | Daftar layanan kritis + pilihan status | asesmen | 1 | Bagian 2.5 — in-scope |
| RekapSafetyCheck | Tabel rekap safety check per pegawai, tab per status | asesmen, deklarasi, monitor-sumberdaya, safety-check | 4 | - |
| RincianAsesmen | Tampilan baca-saja rincian asesmen 5 aspek | monitor-sumberdaya | 1 | Bagian 2.5/2.6 — in-scope |
| RiwayatDeklarasi | Riwayat deklarasi darurat | deklarasi | 1 | - |
| RujukanRisiko | Tabel kriteria dampak & matriks risiko | arkb | 1 | ARKB — out-of-scope |
| SafetyCheckButtons | Tombol "Saya Aman"/"Butuh Bantuan" | safety-check | 1 | Bagian 2.1 — in-scope |
| Shell | Kerangka app: topbar, sidebar, popup broadcast | (dashboard) layout | 1 | **Digantikan Shell platform — dilarang dibangun ulang** |
| SuntingLpkbPimpinan | Sunting isi LPKB oleh Pimpinan sebelum sah | lpkb | 1 | LPKB — out-of-scope |
| TabSeksi | Tab generik pembagi bagian halaman | 12 halaman | 12 | +1 dipakai RekapSafetyCheck |
| TambahLayananManual | Tambah layanan kritis unit manual | asesmen | 1 | Bagian 2.5 — in-scope |
| TombolAkhiriPicu | Padamkan safety check yang berjalan | trigger-safety-check | 1 | Bagian 2.3 — in-scope |
| TombolHapusAdb | Hapus satu layanan dari daftar ADB | adb | 1 | ADB — out-of-scope |
| TombolHapusKejadian | Hapus catatan kejadian bencana manual | - | 1 | Hanya dipakai KelolaKejadian |
| TombolHapusRisiko | Hapus baris pemetaan risiko ARKB | arkb | 1 | ARKB — out-of-scope |
| TombolHapusStandar | Tarik versi standar sistem pengendalian | standar-pengendalian | 1 | Bagian RKBU — out-of-scope |
| TombolHapusUnit | Hapus unit organisasi | admin/unit | 1 | - |
| TombolKeluar | Logout kustom (tidak pakai signOut next-auth) | - | 1 | Digantikan SSO platform — tidak diporting |
| TombolPulih | Nyatakan layanan terganggu sudah pulih | status-layanan | 1 | Kemungkinan duplikat Aspek Layanan Terdampak — perlu konfirmasi |
| TombolStatusPengguna | Nonaktif/aktif/hapus pengguna | admin/pengguna | 1 | Cek dulu: mungkin sudah disediakan IAM platform |
| TombolTanggapDarurat | Pimpinan Satker setujui asesmen & aktifkan tanggap darurat | deklarasi | 1 | Bagian 2.5/alur 3 — in-scope |
| VerifyAlertButtons | Satgas verifikasi/tolak laporan potensi bencana | verifikasi | 1 | Bagian 2.4 — in-scope |
| visual/BilahPeringkat | Bilah horizontal berperingkat (top-N) | data-bencana, katalog-kantor | 3 | +1 dipakai PanelEksekutif |
| visual/BilahTumpuk | Bilah komposisi bertumpuk (stacked bar) | 7 halaman | 9 | +2 dipakai DasborAspek, RekapSafetyCheck |
| visual/Donat | Grafik donat SVG (tanpa pustaka eksternal) | katalog-kantor | 3 | +2 dipakai DasborAspek, PanelEksekutif |
| visual/KartuKpi | Kartu KPI angka utama + tautan telusur | data-bencana, katalog-kantor | 4 | +2 dipakai DasborAspek, PanelEksekutif |
| visual/Ringkas | Baris angka ringkas horizontal | 5 halaman | 5 | - |

---

## 2. Klasifikasi

### (a) Kemungkinan besar sudah ada padanan di design system Kemenkeu

Pola generik — styling dan interaksi tanpa logika domain MKB yang mengikat. Kalau katalog
komponen SCSS platform (lihat `docs/PLAYBOOK.md` Lampiran A: `.page-header`, `.stats-row`,
`.table-card`, dll.) sudah punya padanannya, **pakai itu, jangan dibangun ulang** — ini aturan
mutlak proyek.

| Komponen | Kenapa kandidat design system |
| --- | --- |
| Paginasi | Navigasi halaman generik — paling sering dipakai ulang di seluruh prototipe (31+ lokasi, 28+ halaman) — kandidat design system paling kuat di seluruh daftar |
| TabSeksi | Tab/segmented control generik, dipakai di 12+ halaman tanpa logika domain |
| BadgeLingkup | Badge/chip generik (isinya teks cakupan, bukan logika khusus) |
| Notis + hook `useAksi` | Pola toast/alert hasil aksi — harusnya lewat layanan notifikasi platform, bukan komponen sendiri |
| Lonceng | Ikon notifikasi topbar — **kemungkinan besar bagian dari Shell platform**, bukan tanggung jawab remote SIGAP sama sekali |
| FormAksi | Pembungkus form-submit-hasil generik, dipakai 22+ tempat |
| AksesDitolak | Halaman "403 access denied" — pola generik lintas remote MFE |
| FiturDitutup | Placeholder "fitur belum tersedia" — pola generik |
| visual/KartuKpi | Kartu statistik — **persis contoh eksplisit Anda** ("kartu statistik") |
| visual/Donat, visual/BilahPeringkat, visual/BilahTumpuk, visual/Ringkas | Primitif chart generik (donut, bar berperingkat, stacked bar, ringkasan angka) — kemungkinan platform sudah punya pustaka chart standar |
| FormPengguna, FormUnit, FilterUnit, TombolStatusPengguna, TombolHapusUnit | Pola CRUD generik (`.table-card` + form standar + confirm-delete) — **tapi cek dulu apakah IAM 3-lapis platform sudah menyediakan admin pengguna/unit terpusat**, supaya tidak dibangun dua kali (lihat catatan di bagian 3) |
| DocApprovalRow, AksiCatatan, AksiRilis, PenilaianLpkb (visualnya saja) | Pola tombol approve/reject/batal generik — logika bisnis di baliknya tetap spesifik SIGAP, tapi kontrol UI-nya generik |

### (b) Khusus SIGAP — harus dibangun sendiri

Logika atau visualisasi yang cuma masuk akal untuk domain MKB, dan berada dalam scope Fase 1.

| Komponen | Kenapa khusus SIGAP |
| --- | --- |
| PetaBencana | Peta sebaran bencana per provinsi — **persis contoh Anda** |
| RekapSafetyCheck (+ CatatKeadaanPegawai) | Rekap safety check per kondisi (tab Aman/Butuh Bantuan/Belum Merespons) — **persis contoh Anda**, plus koreksi #11 (tampil sebagai tab, bukan list scroll) |
| SafetyCheckButtons | Tombol "Saya Aman"/"Butuh Bantuan" — bentuk form tetap sesuai koreksi #2 |
| BroadcastPopup | Modal safety check saat broadcast aktif — UX inti alur 1 |
| PemicuOtomatis | Komponen pemicu otomatis parsing BMKG (MMI ≥ V) — logika bisnis inti alur 1, bukan sekadar tampilan |
| PanelBroadcast | Status broadcast safety check nasional yang berjalan |
| PilihBencana (jenis/level/lokasi) | Field form domain bencana, dipakai di alur 2 & 3 |
| PilihLayananTerdampak, TambahLayananManual, PanelStatusAset | Form 5 aspek asesmen (Layanan, Aset) sesuai koreksi #6/#7 |
| RincianAsesmen | Tampilan baca-saja struktur 5 aspek |
| VerifyAlertButtons | Verifikasi/tolak laporan — logika eskalasi ke Pimpinan Satker terikat di dalamnya (koreksi #3) |
| DeklarasiWilayah, RiwayatDeklarasi, TombolTanggapDarurat, DeklarasiForm (hanya `ResolveButton`) | Alur aktivasi/pengakhiran status tanggap darurat |
| TombolAkhiriPicu | Padamkan safety check aktif — aturan "satu broadcast aktif per jenis bencana per unit" (API_CONTRACT bagian 3.3.1) |
| KelolaKejadian | Input manual kejadian bencana oleh Sekjen (bagian 1.1) |
| NomorDarurat | Konten kontak darurat spesifik instansi |
| DasborAspek, PanelEksekutif | Komposisi dashboard yang menyusun primitif (a) dengan data SIGAP — bukan widget reusable lintas aplikasi, tapi tetap perlu disusun ulang manual per halaman |

### (c) Sebaiknya tidak diporting (di luar scope Fase 1, duplikatif, atau digantikan platform)

**c.1 — Di luar scope Fase 1** (bukan "dihapus dari prototipe", cuma **tidak dibangun sekarang**;
prototipe tetap hidup sampai UAT selesai per aturan mutlak #1 di `docs/PLAYBOOK.md`):

AksiRilis, AksiSimulasi, ChecklistEksekusi, DocApprovalRow (pemakaian di modul dokumen),
FilterKatalog, FormAdb, FormAjukanDokumen, FormLpkb, FormRisiko, FormStrategi, HapusCallTree,
HapusRtdb, LihatLpkb, PanduanList, PanelAset, PemulihanLogForm, PenilaianLpkb, RujukanRisiko,
SuntingLpkbPimpinan, TombolHapusAdb, TombolHapusRisiko, TombolHapusStandar
— seluruhnya melayani modul ADB/ARKB/SKB/RTDB/RKBU/RPKK, LPKB, eksekusi-RKB, dokumen MKB,
panduan, komunikasi kebencanaan, atau katalog kantor, yang semuanya **eksplisit out-of-scope
Fase 1** di [MIGRATION_NOTES.md](MIGRATION_NOTES.md) bagian 1.3.

**c.2 — Digantikan komponen bersama platform (dilarang dibangun ulang):**

| Komponen | Alasan |
| --- | --- |
| Shell | Platform ICS sudah menyediakan Shell & SSO terpusat — remote MFE dilarang membangun ulang shell sendiri (Standar Arsitektur ICS) |
| LoginForm, PenyediaSesi, TombolKeluar, PetunjukPasang | Seluruh alur login/sesi digantikan SSO platform; PetunjukPasang (PWA install) cuma dipakai LoginForm |

**c.3 — Duplikatif atau perlu konfirmasi sebelum diputuskan:**

| Komponen/halaman | Alasan |
| --- | --- |
| status-aset (halaman) | Koreksi stakeholder #4 eksplisit: **hapus menu "Status Aset Unit", sudah tercakup di Aspek Aset** pada asesmen |
| FormGangguan, TombolPulih (halaman status-layanan) | Kemungkinan duplikat Aspek Layanan Terdampak (koreksi #7, input manual lewat asesmen) — **perlu konfirmasi ke pemilik proses bisnis** apakah ini modul terpisah (pelacakan gangguan berkelanjutan) atau memang harus digabung ke asesmen |
| monitor-wilayah, nasional (halaman) | Kemungkinan cuma varian ter-filter dari Dashboard Monitor SC & Sumber Daya (2.6) dengan lingkup wilayah/nasional berbeda — di platform baru, filter lingkup semestinya ditangani lewat **Scope di klausa WHERE** (Standar Arsitektur ICS, keamanan lapis 2), bukan route/komponen frontend terpisah. Perlu dicek apakah ini benar-benar halaman berbeda atau bisa digabung jadi satu dashboard dengan filter dinamis |
| AdminPurge, AdminPurgeTotal | Utilitas hapus data demo/simulasi murni untuk kebutuhan testing prototipe, bukan fitur produksi — tidak relevan untuk diporting sama sekali |
| DeklarasiForm — export `DeclareButton` | Dead code, tidak dipakai di mana pun bahkan di prototipe |

---

## 3. Catatan tambahan (perlu klarifikasi, di luar 3 kelompok di atas)

- **FormPengguna, FormUnit, FilterUnit, TombolStatusPengguna, TombolHapusUnit** (halaman
  admin/pengguna, admin/unit): sebelum diporting sebagai (a), pastikan dulu apakah manajemen
  pengguna/unit sudah disediakan oleh **IAM 3 lapis** platform (komponen bersama yang dilarang
  dibangun ulang, lihat [MIGRATION_NOTES.md](MIGRATION_NOTES.md) bagian 3). Kalau sudah ada,
  seluruh 5 komponen ini pindah ke kelompok (c). Cocok ditambahkan ke daftar pertanyaan BaTII
  (Lampiran E PLAYBOOK).
