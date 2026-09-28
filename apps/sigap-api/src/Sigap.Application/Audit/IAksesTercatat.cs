namespace Sigap.Application.Audit;

/// <summary>
/// Penanda respons yang memuat data paling sensitif — daftar keadaan per pegawai dan koordinatnya (API_CONTRACT 1.7).
/// Setiap kali respons bertanda ini dikirim, aksesnya dicatat <see cref="AksiJejak.Diakses"/> secara terpusat oleh
/// filter di lapisan Api: endpoint baru yang mengembalikan DTO ini tercatat tanpa satu baris kode pun di endpointnya.
///
/// <para>Implementasikan <b>secara eksplisit</b> supaya rujukannya tidak ikut diserialkan ke respons.</para>
/// </summary>
public interface IAksesTercatat
{
    RujukanAkses RujukanAkses();
}

/// <summary>Yang dicatat untuk satu akses: entitas induk yang dibaca dan keterangan singkat tanpa isi datanya.</summary>
public sealed record RujukanAkses(string Entitas, string EntitasId, string Keterangan);
