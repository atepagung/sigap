# notifikasi-dummy

**DUMMY.** Tidak boleh naik ke production. Tercatat di
[DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md) bagian 1.8.

Abstraksi dan kanal nyata ada di [libs/notifikasi](../notifikasi/README.md) — itu **bukan**
dummy dan tidak boleh ikut dibuang saat penukaran.

## Isi

| Berkas | Menggantikan | Sampai |
| --- | --- | --- |
| `KanalLog` | — tidak meniru apa pun | selamanya; hanya untuk pengembangan |
| `CatatanKirimanMemori` | tabel `"KirimanPush"` lewat EF Core | P4.2 |
| `GudangLanggananPushMemori` | tabel `"LanggananPush"` lewat EF Core | P4.2 |
| `PengirimWebPushTiruan` | paket `WebPush` + kunci VAPID | P5.3 |

`KanalLog` berbeda dari dummy lain di repo ini: ia tidak meniru kontrak platform mana pun,
karena tidak ada "kanal log" di ICS Keuangan. Ia ada supaya pemberitahuan yang terpicu sebuah
alur dapat dilihat tanpa kunci VAPID, peladen push, maupun peramban yang terbuka.

`PengirimWebPushTiruan` membuat jalur Web Push tetap dapat dijalankan dari ujung ke ujung,
termasuk penghapusan langganan usang — daftarkan endpoint lewat `TolakSelamanya` (410) atau
`TolakSementara` (503).

## Cara memakai

```csharp
#if DEBUG
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddNotifikasiDummy();
}
#endif
```

Kanal log **tidak** didaftarkan oleh `AddNotifikasiDummy`: pemilihan kanal ada di konfigurasi,
bukan di kode. Sediakan jenisnya lalu nyalakan lewat `appsettings.Development.json`:

```jsonc
"Notifikasi": {
  "Kanal": [ "dalam-aplikasi", "log" ]
}
```

`AddNotifikasiDummy` memakai `TryAdd`, jadi begitu implementasi sungguhan didaftarkan (P4.2)
yang ini tidak lagi terpakai.

## Aturan dummy #4

Build production harus gagal bila dummy masih ter-resolve. Di sigap-api, rujukannya dibatasi
konfigurasi build:

```xml
<ProjectReference Include="../../libs/notifikasi-dummy/src/Sigap.Notifikasi.Dummy/Sigap.Notifikasi.Dummy.csproj"
                  Condition="'$(Configuration)' == 'Debug'" />
```

sehingga `dotnet build -c Release` tidak menyusun proyek ini sama sekali, dan blok `#if DEBUG`
di `Program.cs` ikut hilang. Pemeriksa otomatis menyeluruh tetap dipasang di P4.3/P6.2.

## Penukaran

1. **P4.2** — implementasikan `ICatatanKiriman` dan `IGudangLanggananPush` di atas EF Core,
   lalu hapus `CatatanKirimanMemori` dan `GudangLanggananPushMemori`.
2. **P5.3** — implementasikan `IPengirimWebPush` di atas paket `WebPush`, lalu hapus
   `PengirimWebPushTiruan`.
3. Keluarkan `"log"` dari konfigurasi lingkungan mana pun selain pengembangan.
4. Hapus `libs/notifikasi-dummy/` setelah ketiganya selesai.
