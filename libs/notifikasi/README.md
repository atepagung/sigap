# notifikasi

Abstraksi kanal notifikasi SIGAP. **Bukan dummy** — lapisan ini tetap dipakai setelah seluruh
komponen platform tersedia.

```bash
dotnet test libs/notifikasi/notifikasi.slnx   # 58 tes
```

Yang dummy hanya kanal log dan pengisi port saat pengembangan, dan itu ada di
[libs/notifikasi-dummy](../notifikasi-dummy/README.md) (DUMMY_REGISTRY bagian 1.8).

## Mengapa lapisan ini ada

Dua hal belum dijawab BaTII:

| Pertanyaan | Akibatnya bila dijawab |
| --- | --- |
| Lampiran E #9 — apakah platform menyediakan layanan notifikasi bersama di antara 20 layanan data/API? | Bertambah satu kanal |
| Lampiran E #13 — apakah Web Push diizinkan di domain platform? | `web-push` dinyalakan atau dibuang |

Apa pun jawabannya, yang berubah adalah **daftar kanal di konfigurasi**, bukan kode fitur.
Kode fitur memanggil `IPengirimNotifikasi` dan tidak pernah tahu kanal mana yang berjalan.

## Kanal

| Nama | Letak | Tahan luring | Keterangan |
| --- | --- | --- | --- |
| `dalam-aplikasi` | `libs/notifikasi` | **ya** | Dijemput lewat `GET /notifikasi` |
| `web-push` | `libs/notifikasi` | tidak | Nonaktif sampai Lampiran E #13 dijawab |
| `log` | `libs/notifikasi-dummy` | tidak | Pengembangan saja |

**Tahan luring** berarti penerima yang sedang luring saat pemberitahuan dibuat tetap
melihatnya ketika kembali daring — syarat PLAYBOOK P5.3. Konfigurasi yang tidak memuat satu
pun kanal bersifat itu **ditolak saat proses mulai**.

### Mengapa `dalam-aplikasi` tidak mengirim apa pun

Kanal itu sengaja tidak berbuat apa-apa saat dipanggil. Jangan "diperbaiki" dengan menyimpan
baris pemberitahuan:

- API_CONTRACT #43 menetapkan peringatan **dihitung saat diminta** dan tidak punya status
  "sudah dibaca", karena skema 32 tabel tidak punya tabelnya. Menyimpannya berarti tabel
  ke-34, dan itu perlu persetujuan pemilik proyek lebih dulu.
- Karena dihitung ulang, peringatan basi mustahil ada dan syarat luring P5.3 terpenuhi tanpa
  kode tambahan.

## Cara kode aplikasi (P4, P5.3) memakainya

```csharp
// Program.cs
builder.Services.AddNotifikasi(builder.Configuration, kanal =>
{
    kanal.Tambah<KanalDalamAplikasi>();
    kanal.Tambah<KanalWebPush>();
#if DEBUG
    kanal.Tambah<KanalLog>();
#endif
});
```

`Tambah` hanya **menyediakan** jenis kanal. Yang **menyalakan** hanya konfigurasi:

```jsonc
// appsettings.json
"Notifikasi": {
  "Kanal": [ "dalam-aplikasi", "web-push" ],
  "WebPush": {
    "Aktif": false,                                  // menunggu Lampiran E #13
    "Subjek": "mailto:organta@kemenkeu.go.id",
    "TtlDetik": 3600
  }
}
```

```csharp
// Kode fitur — sesudah transaksi bisnis (API_CONTRACT bagian 3.3)
await pengirim.KirimAsync(penerimaIds, new Pemberitahuan
{
    Kode = "SC_BELUM_DIJAWAB",
    Tingkat = TingkatPemberitahuan.Genting,
    Judul = "Broadcast safety check aktif di unit Anda",
    Pesan = "Mohon pilih Saya Aman atau Butuh Bantuan.",
    Terkait = new Terkait("BROADCAST", broadcast.Id),
    KunciIdempotensi = $"sc-{broadcast.Id}"          // opsional; kirim sekali saja
}, ct);
```

Aturan untuk kode aplikasi:

- **Jangan menyuntik `IKanalNotifikasi`.** Hanya `IPengirimNotifikasi`. Menyentuh kanal
  langsung berarti memilih kanal di kode, dan itu persis yang lapisan ini hindari.
- **Jangan `if (kanal == "web-push")`.** Perbedaan kanal ditangani kanal itu sendiri.
- **Penerima ditentukan pemanggil**, dari lingkup yang dipicu (P5.3). Kanal tidak tahu peran,
  unit, maupun Scope.
- **Panggil sesudah transaksi bisnis di-commit.** Pengirim tidak melempar saat kanal gagal —
  broadcast yang sudah tercatat tidak boleh dibatalkan hanya karena pemberitahuannya tidak
  sampai. Periksa `RingkasanKirim.SemuaKanalGagal` bila perlu.
- **`KunciIdempotensi` untuk keadaan yang dapat terpicu berulang** (mis. pelanggaran RTO yang
  dinilai ulang tiap permintaan halaman). Tanpa itu, penerima berhenti memperhatikan
  pemberitahuan.
- **Kunci VAPID tidak pernah masuk `appsettings` maupun git** — environment variable atau
  vault (aturan mutlak proyek no. 4).

## Port yang harus diisi aplikasi

Lapisan ini tidak bergantung pada EF Core, karena `DbContext`-nya baru ada di P4.2. Tiga port
berikut diimplementasikan Infrastructure:

| Port | Sumber data | Wajib bila |
| --- | --- | --- |
| `ICatatanKiriman` | tabel `"KirimanPush"` (sudah ada, indeks unik `kunci`) | selalu |
| `IGudangLanggananPush` | tabel `"LanggananPush"` (sudah ada) | kanal `web-push` dipakai |
| `IPengirimWebPush` | paket `WebPush` + kunci VAPID | kanal `web-push` dipakai |

Tidak ada perubahan skema. `"KirimanPush"` namanya berbau push karena sejarahnya, tetapi
komentar skemanya sendiri menyebut kegunaannya umum: mencegah kiriman berulang untuk keadaan
yang sama.

Selama P3, ketiganya diisi versi dalam memori dari `libs/notifikasi-dummy`.

## Yang belum dibangun

- **Sisi Angular** — `NotifikasiService` yang memoll `GET /notifikasi` dan pendaftaran
  langganan Web Push. Menyusul di P4, saat endpoint dan UI-nya ada.
- **`IPengirimWebPush` sungguhan** di atas paket `WebPush`. Menyusul di P5.3; baru berguna
  setelah Lampiran E #13 dijawab dan kunci VAPID tersedia.
- **Katalog kode pemberitahuan** (`SC_BELUM_DIJAWAB` dan kawan-kawan) ditetapkan bersama
  `GET /notifikasi` di P4/P5.3. Lapisan ini hanya menjaga bentuk kodenya.
- **Jejak audit** pemberitahuan — senada DUMMY_REGISTRY butir 74, menunggu Lampiran E #10.
