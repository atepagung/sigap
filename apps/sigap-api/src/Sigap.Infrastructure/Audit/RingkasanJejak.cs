using System.Globalization;
using System.Text.Json;

namespace Sigap.Infrastructure.Audit;

/// <summary>
/// Menyusun isi <c>"JejakPerubahan"."ringkasan"</c>: JSON <c>{ sebelum, sesudah, oleh }</c>. Tabelnya tidak
/// punya kolom nilai sebelum/sesudah, jadi keduanya dititipkan di sini (API_CONTRACT 1.7). <c>oleh</c>
/// memuat peran dan unit pelaku <b>pada saat itu</b> — peran hidup di token, tidak di tabel, jadi tanpa
/// ini tidak dapat direkonstruksi kemudian (API_CONTRACT 3.3.1 butir 7).
///
/// <para>
/// Kolom rahasia disamarkan, bukan dibuang: jejak tetap menunjukkan bahwa kolomnya berubah tanpa
/// menyimpan nilainya. Teks panjang dipotong supaya satu catatan tidak membengkakkan tabel jejak.
/// </para>
/// </summary>
internal static class RingkasanJejak
{
    public const string Disamarkan = "[DISAMARKAN]";
    public const int TeksMaksimal = 2000;

    /// <summary>
    /// Nama properti yang nilainya tidak boleh masuk jejak: kata sandi, kontak, kunci perangkat push, dan dua catatan
    /// SDM asesmen yang dapat memuat nama serta keadaan medis pegawai. Yang terakhir di-Sieve di API
    /// (<c>asesmen.sdm.*</c>); tanpa ini tabel jejak menjadi jalan belakang untuk membaca isinya.
    /// </summary>
    public static IReadOnlySet<string> Rahasia { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "PasswordHash", "Email", "Endpoint", "P256dh", "Auth", "CatatanPegawai", "SdmCatatan"
    };

    private static readonly JsonSerializerOptions Opsi = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Susun(
        IReadOnlyDictionary<string, object?>? sebelum,
        IReadOnlyDictionary<string, object?>? sesudah,
        IReadOnlyCollection<string> peran,
        string? unitId)
    {
        var isi = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (sebelum is not null)
        {
            isi["sebelum"] = Normalkan(sebelum);
        }

        if (sesudah is not null)
        {
            isi["sesudah"] = Normalkan(sesudah);
        }

        isi["oleh"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["peran"] = peran.Order(StringComparer.Ordinal).ToArray(),
            ["unitId"] = unitId
        };

        return JsonSerializer.Serialize(isi, Opsi);
    }

    private static Dictionary<string, object?> Normalkan(IReadOnlyDictionary<string, object?> nilai)
    {
        var hasil = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (nama, isi) in nilai)
        {
            hasil[JsonNamingPolicy.CamelCase.ConvertName(nama)] = Rahasia.Contains(nama) ? Disamarkan : Ubah(isi);
        }

        return hasil;
    }

    private static object? Ubah(object? nilai) => nilai switch
    {
        null => null,
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        Enum e => e.ToString(),
        string s when s.Length > TeksMaksimal => string.Concat(s.AsSpan(0, TeksMaksimal), "…"),
        bool or string or int or long or double or float or decimal => nilai,
        _ => Convert.ToString(nilai, CultureInfo.InvariantCulture)
    };
}
