using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Sigap.Domain.Umum;
using Sigap.Infrastructure.Lampiran;

namespace Sigap.Infrastructure.Tests.Lampiran;

/// <summary>
/// Tes yang butuh MinIO lokal (docker-compose.yml, layanan <c>minio</c>). Bila tidak terjangkau,
/// tes dilaporkan <b>Skipped</b> beserta alasannya — sama seperti <see cref="FaktaDatabaseAttribute"/>
/// untuk PostgreSQL.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FaktaMinioAttribute : FactAttribute
{
    private static readonly Lazy<string?> AlasanLewat = new(() =>
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var respons = http.GetAsync(FiksturMinio.Endpoint + "/minio/health/live").GetAwaiter().GetResult();
            return respons.IsSuccessStatusCode
                ? null
                : $"MinIO lokal menjawab {(int)respons.StatusCode} — periksa docker compose up -d minio";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return $"MinIO lokal tidak terjangkau di {FiksturMinio.Endpoint} ({e.Message}). Jalankan: docker compose up -d minio";
        }
    });

    public FaktaMinioAttribute()
    {
        if (AlasanLewat.Value is { } alasan)
        {
            Skip = alasan;
        }
    }
}

internal static class FiksturMinio
{
    /// <summary>MinIO lokal (docker-compose.yml). Kredensial dev, bukan rahasia asli.</summary>
    public const string Endpoint = "http://localhost:9000";

    public static S3Options Opsi(string bucket) => new()
    {
        Endpoint = Endpoint,
        Bucket = bucket,
        AccessKey = "minioadmin",
        SecretKey = "miniopassword",
        ForcePathStyle = true
    };

    public static string KunciAcak(string ekstensi = ".jpg") =>
        $"sigap-uji/{DateTime.UtcNow:yyyy-MM-dd}/{Guid.NewGuid():N}{ekstensi}";

    private static MemoryStream Isi(string teks) => new(Encoding.UTF8.GetBytes(teks));

    public static Task SimpanTeksAsync(PenyimpanLampiranS3 penyimpan, string kunci, string teks) =>
        penyimpan.SimpanAsync(kunci, Isi(teks), CancellationToken.None);

    public static async Task<string> BacaTeksAsync(PenyimpanLampiranS3 penyimpan, string kunci)
    {
        await using var isi = await penyimpan.BukaAsync(kunci, CancellationToken.None);
        Assert.NotNull(isi);
        using var baca = new StreamReader(isi);
        return await baca.ReadToEndAsync();
    }
}

/// <summary>Bucket bersama "sigap-lampiran-uji", dipakai berulang antar-tes lewat kunci acak (idempoten, aman paralel).</summary>
public sealed class PenyimpanLampiranS3Tests : IAsyncLifetime
{
    private const string Bucket = "sigap-lampiran-uji";
    private AmazonS3Client _s3 = null!;
    private PenyimpanLampiranS3 _penyimpan = null!;

    public async Task InitializeAsync()
    {
        _s3 = PenyimpanLampiranS3.BuatKlien(FiksturMinio.Opsi(Bucket));
        _penyimpan = new PenyimpanLampiranS3(_s3, Bucket);

        // Unggahan pertama tes membuat bucket ini otomatis (lihat test khusus di bawah); di sini
        // dipastikan lebih dulu supaya tes lain di kelas ini tidak bergantung urutan.
        await FiksturMinio.SimpanTeksAsync(_penyimpan, "sigap-uji/.bootstrap", "bootstrap");
    }

    public Task DisposeAsync()
    {
        _s3.Dispose();
        return Task.CompletedTask;
    }

    [FaktaMinio]
    public async Task Simpan_lalu_buka_mengembalikan_isi_yang_sama()
    {
        string kunci = FiksturMinio.KunciAcak();

        await FiksturMinio.SimpanTeksAsync(_penyimpan, kunci, "isi lampiran uji");

        Assert.Equal("isi lampiran uji", await FiksturMinio.BacaTeksAsync(_penyimpan, kunci));
    }

    [FaktaMinio]
    public async Task Kunci_yang_tidak_ada_dibuka_sebagai_null_bukan_galat()
    {
        Assert.Null(await _penyimpan.BukaAsync(FiksturMinio.KunciAcak(), CancellationToken.None));
    }

    [FaktaMinio]
    public async Task Hapus_lalu_buka_mengembalikan_null()
    {
        string kunci = FiksturMinio.KunciAcak();
        await FiksturMinio.SimpanTeksAsync(_penyimpan, kunci, "akan dihapus");

        await _penyimpan.HapusAsync(kunci, CancellationToken.None);

        Assert.Null(await _penyimpan.BukaAsync(kunci, CancellationToken.None));
    }

    [FaktaMinio]
    public async Task Hapus_kunci_yang_tidak_ada_diam_bukan_galat() =>
        await _penyimpan.HapusAsync(FiksturMinio.KunciAcak(), CancellationToken.None);

    [FaktaMinio]
    public async Task Kunci_dengan_subfolder_tanggal_tersimpan_dan_terbaca()
    {
        string kunci = $"2026-09-27/{Guid.NewGuid():N}.pdf";

        await FiksturMinio.SimpanTeksAsync(_penyimpan, kunci, "dokumen uji");

        Assert.Equal("dokumen uji", await FiksturMinio.BacaTeksAsync(_penyimpan, kunci));
    }

    [FaktaMinio]
    public async Task Isi_biner_utuh_bulat_pergi()
    {
        byte[] acak = RandomBytes(50_000);
        string kunci = FiksturMinio.KunciAcak(".bin");

        await _penyimpan.SimpanAsync(kunci, new MemoryStream(acak), CancellationToken.None);

        await using var dibaca = await _penyimpan.BukaAsync(kunci, CancellationToken.None);
        using var salinan = new MemoryStream();
        await dibaca!.CopyToAsync(salinan);
        Assert.Equal(acak, salinan.ToArray());
    }

    private static byte[] RandomBytes(int panjang)
    {
        var acak = new byte[panjang];
        Random.Shared.NextBytes(acak);
        return acak;
    }
}

/// <summary>Bucket sendiri, sengaja belum ada, untuk membuktikan pembuatan otomatis saat unggahan pertama.</summary>
public sealed class PenyimpanLampiranS3BucketBelumAdaTests : IAsyncLifetime
{
    private readonly string _bucket = "sigap-uji-bucket-baru-" + Guid.NewGuid().ToString("N")[..12];
    private AmazonS3Client _s3 = null!;

    public Task InitializeAsync()
    {
        _s3 = PenyimpanLampiranS3.BuatKlien(FiksturMinio.Opsi(_bucket));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            var isi = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket });
            foreach (var objek in isi.S3Objects ?? [])
            {
                await _s3.DeleteObjectAsync(_bucket, objek.Key);
            }

            await _s3.DeleteBucketAsync(_bucket);
        }
        catch (AmazonS3Exception)
        {
            // Bucket memang belum sempat dibuat di tes yang tidak sampai menulis apa pun; abaikan.
        }
        finally
        {
            _s3.Dispose();
        }
    }

    [FaktaMinio]
    public async Task Unggahan_pertama_membuat_bucket_secara_otomatis()
    {
        var penyimpan = new PenyimpanLampiranS3(_s3, _bucket);
        string kunci = FiksturMinio.KunciAcak();

        await FiksturMinio.SimpanTeksAsync(penyimpan, kunci, "bucket baru");

        Assert.Equal("bucket baru", await FiksturMinio.BacaTeksAsync(penyimpan, kunci));
    }

    [FaktaMinio]
    public async Task Membuat_bucket_yang_ternyata_sudah_ada_tidak_melempar()
    {
        // Meniru dua replika yang mulai bersamaan: bucket dibuat lebih dulu di luar penyimpan ini,
        // lalu penyimpan tetap mencoba membuatnya lagi (bukan lewat jalur NoSuchBucket -> retry).
        var penyimpan = new PenyimpanLampiranS3(_s3, _bucket);
        await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket });

        await penyimpan.BuatBucketAsync(CancellationToken.None);
    }
}

public class PenyimpanLampiranS3GagalTests
{
    [FaktaMinio]
    public async Task Endpoint_tidak_terjangkau_melempar_AturanBisnisException_503()
    {
        using var s3 = PenyimpanLampiranS3.BuatKlien(new S3Options
        {
            // Port ditutup di loopback: koneksi ditolak segera, bukan menunggu waktu habis DNS.
            Endpoint = "http://127.0.0.1:1",
            Bucket = "tidak-relevan",
            AccessKey = "x",
            SecretKey = "x"
        });
        var penyimpan = new PenyimpanLampiranS3(s3, "tidak-relevan");

        var galat = await Assert.ThrowsAsync<AturanBisnisException>(
            () => penyimpan.SimpanAsync("k.jpg", new MemoryStream([1, 2, 3]), CancellationToken.None));

        Assert.Equal(KodeGalat.LampiranGagalDisimpan, galat.Kode);
        Assert.Equal(StatusHttp.LayananTidakTersedia, galat.Status);
    }

    [FaktaMinio]
    public async Task Endpoint_tidak_terjangkau_saat_hapus_diam_bukan_galat()
    {
        using var s3 = PenyimpanLampiranS3.BuatKlien(new S3Options
        {
            Endpoint = "http://127.0.0.1:1",
            Bucket = "tidak-relevan",
            AccessKey = "x",
            SecretKey = "x"
        });
        var penyimpan = new PenyimpanLampiranS3(s3, "tidak-relevan");

        await penyimpan.HapusAsync("k.jpg", CancellationToken.None);
    }

    [Fact]
    public async Task Pembatalan_sebelum_mulai_melempar_OperationCanceledException_bukan_dianggap_galat_penyimpanan()
    {
        using var s3 = PenyimpanLampiranS3.BuatKlien(FiksturMinio.Opsi("tidak-relevan"));
        var penyimpan = new PenyimpanLampiranS3(s3, "tidak-relevan");
        using var batal = new CancellationTokenSource();
        await batal.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => penyimpan.SimpanAsync("k.jpg", new MemoryStream([1]), batal.Token));
    }
}
