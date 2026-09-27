namespace Sigap.Infrastructure.Lampiran;

/// <summary>
/// Konfigurasi bagian <c>"Lampiran"</c>: driver penyimpanan berkas lampiran dan pengaturannya.
///
/// <para>
/// Padanan <c>STORAGE_DRIVER</c> pada <c>storage.ts</c> prototipe: <c>disk</c> (bawaan, dipakai
/// pengembangan) menulis ke folder pada disk lewat <see cref="PenyimpanLampiranDisk"/>; <c>s3</c>
/// menyimpan ke object storage berprotokol S3 (MinIO lokal, atau layanan lain yang disediakan
/// BaTII) lewat <see cref="PenyimpanLampiranS3"/>. Tidak ada perubahan kode fitur saat berpindah
/// driver — keduanya implementasi <see cref="Sigap.Application.Lampiran.IPenyimpanLampiran"/>.
/// </para>
/// </summary>
public sealed class LampiranOptions
{
    public const string Bagian = "Lampiran";

    public const string DriverDisk = "disk";
    public const string DriverS3 = "s3";

    /// <summary><c>disk</c> (bawaan) atau <c>s3</c>.</summary>
    public string Driver { get; set; } = DriverDisk;

    /// <summary>Wajib bila <see cref="Driver"/> = <c>disk</c>. Path relatif dihitung dari folder keluaran.</summary>
    public string? Folder { get; set; }

    public S3Options S3 { get; set; } = new();
}

/// <summary>
/// Pengaturan object storage S3. Padanan variabel <c>S3_*</c> pada <c>storage.ts</c> prototipe.
/// <see cref="AccessKey"/> dan <see cref="SecretKey"/> kosong dengan sengaja di berkas yang masuk
/// git — di production diisi <c>Lampiran__S3__AccessKey</c> / <c>Lampiran__S3__SecretKey</c> dari
/// vault (aturan mutlak proyek no. 4); di pengembangan lewat <c>appsettings.Development.json</c>
/// dengan kredensial MinIO lokal (bukan rahasia asli).
/// </summary>
public sealed class S3Options
{
    /// <summary>Wajib bila driver <c>s3</c>. MinIO lokal: <c>http://localhost:9000</c>.</summary>
    public string? Endpoint { get; set; }

    public string Bucket { get; set; } = "sigap-lampiran";

    public string Region { get; set; } = "us-east-1";

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>
    /// Wajib <c>true</c> untuk MinIO dan sebagian besar object storage selain S3 asli (URL berbentuk
    /// <c>endpoint/bucket/kunci</c>, bukan <c>bucket.endpoint/kunci</c>).
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;
}
