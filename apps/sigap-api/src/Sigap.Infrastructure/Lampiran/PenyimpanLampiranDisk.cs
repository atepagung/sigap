using Sigap.Application.Lampiran;
using Sigap.Domain.Umum;

namespace Sigap.Infrastructure.Lampiran;

/// <summary>
/// Penyimpan lampiran ke disk — padanan driver <c>local</c> pada <c>storage.ts</c> prototipe.
/// Object storage S3/MinIO (P5.2) cukup menjadi implementasi <see cref="IPenyimpanLampiran"/> lain;
/// kode fitur tidak berubah.
///
/// <para>
/// Kunci berasal dari kode kita sendiri (<c>tanggal/guid.ekstensi</c>), tetapi <b>kunci yang dibaca
/// dari database dianggap tidak tepercaya</b>: setiap kunci diselesaikan ke path penuh dan wajib
/// berada di bawah folder akar. Kunci yang melarikan diri (<c>../</c>, path mutlak) ditolak.
/// </para>
/// </summary>
internal sealed class PenyimpanLampiranDisk : IPenyimpanLampiran
{
    private readonly string _akar;

    public PenyimpanLampiranDisk(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        // Path relatif dihitung dari folder keluaran (ContentRoot), bukan folder kerja proses.
        _akar = Path.GetFullPath(folder, AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
    }

    public async Task SimpanAsync(string kunci, Stream isi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(isi);
        string tujuan = Tujuan(kunci);

        // Yang dibersihkan hanya berkas yang dibuat panggilan ini. FileMode.CreateNew menolak kunci
        // yang sudah ada dan tidak pernah menimpa; kegagalan karena itu TIDAK boleh menghapus berkas
        // milik lampiran lain yang kebetulan berkunci sama.
        bool dibuat = false;
        bool selesai = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tujuan)!);
            await using (var berkas = new FileStream(
                tujuan, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                dibuat = true;
                await isi.CopyToAsync(berkas, ct);
            }

            selesai = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Laporannya sendiri tetap ada dan unggahan boleh diulang (API_CONTRACT #8).
            throw new AturanBisnisException(
                KodeGalat.LampiranGagalDisimpan,
                "Lampiran gagal disimpan",
                "Lampiran belum dapat disimpan. Laporan Anda tetap tercatat; coba unggah lagi.",
                StatusHttp.LayananTidakTersedia);
        }
        finally
        {
            // Tulisan setengah jadi (gagal, atau dibatalkan di tengah jalan) tidak boleh tertinggal.
            if (dibuat && !selesai)
            {
                TryHapus(tujuan);
            }
        }
    }
    public Task<Stream?> BukaAsync(string kunci, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string tujuan = Tujuan(kunci);

        try
        {
            return Task.FromResult<Stream?>(new FileStream(
                tujuan, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task HapusAsync(string kunci, CancellationToken ct)
    {
        TryHapus(Tujuan(kunci));
        return Task.CompletedTask;
    }

    private string Tujuan(string kunci)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kunci);

        string penuh = Path.GetFullPath(Path.Combine(_akar, kunci));
        if (!penuh.StartsWith(_akar + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Kunci penyimpanan keluar dari folder lampiran.", nameof(kunci));
        }

        return penuh;
    }

    private static void TryHapus(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Pembersihan sebisanya; tidak boleh menutupi galat asal.
        }
    }
}
