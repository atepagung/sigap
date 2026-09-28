using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Sigap.Application.Lampiran;
using Sigap.Domain.Umum;

namespace Sigap.Infrastructure.Lampiran;

/// <summary>
/// Penyimpan lampiran ke object storage berprotokol S3 (MinIO lokal, atau layanan lain yang
/// disediakan BaTII) — padanan driver <c>s3</c> pada <c>storage.ts</c> prototipe (P5.2).
///
/// <para>
/// <b>Tidak pernah menghasilkan URL yang bisa diakses tanpa autentikasi.</b> Berbeda dari
/// prototipe (yang menyimpan URL S3 di kolom <c>"url"</c>), <see cref="ILampiranStore"/> selalu
/// menulis path API ber-autentikasi di kolom itu (<c>LampiranStore</c>); kelas ini hanya
/// menyimpan dan membaca <i>byte</i>, tidak pernah menghasilkan presigned URL atau bucket
/// publik. Satu-satunya jalan membaca isi berkas tetap <c>GET /lampiran/{id}</c>, yang memeriksa
/// permission dan Scope lebih dulu (<see cref="BacaLampiran"/>).
/// </para>
/// <para>
/// Kunci berasal dari kode kita sendiri (<c>tanggal/guid.ekstensi</c>, <c>AturanLampiran</c>),
/// tidak pernah dari nama berkas kiriman pengguna.
/// </para>
/// </summary>
internal sealed class PenyimpanLampiranS3 : IPenyimpanLampiran
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public PenyimpanLampiranS3(IAmazonS3 s3, string bucket)
    {
        ArgumentNullException.ThrowIfNull(s3);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        _s3 = s3;
        _bucket = bucket;
    }

    public static AmazonS3Client BuatKlien(S3Options opsi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opsi.Endpoint);

        var konfigurasi = new AmazonS3Config
        {
            ServiceURL = opsi.Endpoint,
            ForcePathStyle = opsi.ForcePathStyle,
            AuthenticationRegion = opsi.Region
        };

        return new AmazonS3Client(new BasicAWSCredentials(opsi.AccessKey, opsi.SecretKey), konfigurasi);
    }

    /// <summary><c>true</c> setelah bucket terbukti ada sekali; menghindari pemeriksaan berulang tiap unggahan.</summary>
    private volatile bool _bucketAda;

    public async Task SimpanAsync(string kunci, Stream isi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(isi);
        ArgumentException.ThrowIfNullOrWhiteSpace(kunci);

        try
        {
            await PutAsync(kunci, isi, ct);
        }
        catch (AmazonS3Exception e) when (!_bucketAda && string.Equals(e.ErrorCode, "NoSuchBucket", StringComparison.Ordinal))
        {
            // Bucket lokal (MinIO dev) belum ada — prototipe mengasumsikan bucket sudah dibuat lebih
            // dulu (storage.ts, "jangan tulis ulang dari nol"), tetapi tidak ada langkah setup MinIO
            // terpisah di repo ini, jadi dibuat sekali di sini. Aman diulang: pembuatan bersamaan
            // atau bucket yang ternyata sudah ada (BucketAlreadyOwnedByYou) diabaikan.
            await BuatBucketAsync(ct);
            isi.Position = 0;
            await PutAsync(kunci, isi, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            // Laporannya sendiri tetap ada dan unggahan boleh diulang (API_CONTRACT #8), sama
            // seperti PenyimpanLampiranDisk.
            throw new AturanBisnisException(
                KodeGalat.LampiranGagalDisimpan,
                "Lampiran gagal disimpan",
                "Lampiran belum dapat disimpan. Laporan Anda tetap tercatat; coba unggah lagi.",
                StatusHttp.LayananTidakTersedia);
        }
    }

    private async Task PutAsync(string kunci, Stream isi, CancellationToken ct)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = kunci,
            InputStream = isi,
            AutoCloseStream = false
        }, ct);
        _bucketAda = true;
    }

    /// <summary>
    /// <c>internal</c> hanya supaya tes dapat memaksa jalur "bucket sudah ada" secara langsung
    /// (dua replika yang mulai bersamaan) tanpa harus benar-benar membalap proses lain.
    /// </summary>
    internal async Task BuatBucketAsync(CancellationToken ct)
    {
        try
        {
            await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // Sudah ada (dibuat proses lain yang bersamaan, atau memang sudah ada) — bukan galat.
        }

        _bucketAda = true;
    }

    public async Task<Stream?> BukaAsync(string kunci, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kunci);

        try
        {
            var respons = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = _bucket, Key = kunci }, ct);
            return new BerkasS3(respons);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound
            || string.Equals(e.ErrorCode, "NoSuchKey", StringComparison.Ordinal))
        {
            return null;
        }
    }

    public async Task HapusAsync(string kunci, CancellationToken ct)
    {
        try
        {
            await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = kunci }, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            // Pembersihan sebisanya; tidak boleh menutupi galat asal (sama seperti PenyimpanLampiranDisk).
        }
    }

    /// <summary>
    /// Mengalirkan isi objek S3 tanpa membufernya di memori, sambil memastikan koneksi HTTP di
    /// bawahnya (dan <see cref="GetObjectResponse"/> yang memilikinya) ikut ditutup saat pemanggil
    /// selesai membaca — <c>GetObjectResponse.ResponseStream</c> saja tidak cukup dipegang lepas
    /// dari respons induknya.
    /// </summary>
    private sealed class BerkasS3(GetObjectResponse respons) : Stream
    {
        private readonly Stream _dalam = respons.ResponseStream;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => respons.ContentLength;

        public override long Position
        {
            get => _dalam.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _dalam.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _dalam.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _dalam.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dalam.Dispose();
                respons.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _dalam.DisposeAsync();
            respons.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
