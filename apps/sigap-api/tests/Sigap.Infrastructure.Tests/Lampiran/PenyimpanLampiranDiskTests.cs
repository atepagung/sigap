using System.Text;
using Sigap.Domain.Umum;
using Sigap.Infrastructure.Lampiran;

namespace Sigap.Infrastructure.Tests.Lampiran;

public sealed class PenyimpanLampiranDiskTests : IDisposable
{
    private readonly string _akar = Path.Combine(Path.GetTempPath(), "sigap-uji-disk-" + Guid.NewGuid().ToString("N"));
    private readonly PenyimpanLampiranDisk _penyimpan;

    public PenyimpanLampiranDiskTests()
    {
        _penyimpan = new PenyimpanLampiranDisk(_akar);
    }

    public void Dispose()
    {
        if (Directory.Exists(_akar))
        {
            Directory.Delete(_akar, recursive: true);
        }
    }

    private static MemoryStream Isi(string teks) => new(Encoding.UTF8.GetBytes(teks));

    [Fact]
    public async Task Simpan_lalu_buka_mengembalikan_isi_yang_sama_dan_membuat_subfolder()
    {
        await _penyimpan.SimpanAsync("2026-09-21/a.jpg", Isi("isi berkas"), CancellationToken.None);

        await using var buka = await _penyimpan.BukaAsync("2026-09-21/a.jpg", CancellationToken.None);
        using var baca = new StreamReader(buka!);
        Assert.Equal("isi berkas", await baca.ReadToEndAsync());
        Assert.True(File.Exists(Path.Combine(_akar, "2026-09-21", "a.jpg")));
    }

    [Fact]
    public async Task Berkas_yang_tidak_ada_dibuka_sebagai_null_bukan_galat()
    {
        Assert.Null(await _penyimpan.BukaAsync("2026-09-21/tidak-ada.jpg", CancellationToken.None));
        Assert.Null(await _penyimpan.BukaAsync("folder-tidak-ada/x.jpg", CancellationToken.None));
    }

    [Fact]
    public async Task Hapus_membuang_berkas_dan_diam_bila_tidak_ada()
    {
        await _penyimpan.SimpanAsync("2026-09-21/b.jpg", Isi("x"), CancellationToken.None);

        await _penyimpan.HapusAsync("2026-09-21/b.jpg", CancellationToken.None);
        await _penyimpan.HapusAsync("2026-09-21/b.jpg", CancellationToken.None);

        Assert.Null(await _penyimpan.BukaAsync("2026-09-21/b.jpg", CancellationToken.None));
    }

    [Theory]
    [InlineData("../luar.jpg")]
    [InlineData("2026-09-21/../../luar.jpg")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Kunci_yang_keluar_dari_folder_akar_ditolak_di_semua_operasi(string kunci)
    {
        // Kunci dari database dianggap tidak tepercaya: baris yang dirusak tidak boleh membaca,
        // menulis, atau menghapus berkas di luar folder lampiran.
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.SimpanAsync(kunci, Isi("x"), CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.BukaAsync(kunci, CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.HapusAsync(kunci, CancellationToken.None));
    }

    [Fact]
    public async Task Path_mutlak_apa_pun_ditolak_di_semua_operasi()
    {
        // Dibentuk saat berjalan supaya valid di Windows maupun Linux. "C:\Windows\..." sengaja tidak
        // ditulis: di Linux "\" hanya karakter nama berkas, bukan pemisah, sehingga bukan path mutlak.
        string mutlak = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "luar-akar.jpg"));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.SimpanAsync(mutlak, Isi("x"), CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.BukaAsync(mutlak, CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.HapusAsync(mutlak, CancellationToken.None));
        Assert.False(File.Exists(mutlak));
    }

    [Fact]
    public async Task Kunci_yang_menyerupai_akar_tetapi_di_luarnya_ditolak()
    {
        // "<akar>-lain/x" berawalan sama dengan "<akar>" sebagai teks tetapi bukan di dalamnya.
        string saudara = "../" + Path.GetFileName(_akar) + "-lain/x.jpg";

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _penyimpan.SimpanAsync(saudara, Isi("x"), CancellationToken.None));
    }

    [Fact]
    public async Task Kunci_yang_sama_tidak_pernah_menimpa_berkas_yang_ada()
    {
        await _penyimpan.SimpanAsync("2026-09-21/c.jpg", Isi("asli"), CancellationToken.None);

        var galat = await Assert.ThrowsAsync<AturanBisnisException>(
            () => _penyimpan.SimpanAsync("2026-09-21/c.jpg", Isi("timpa"), CancellationToken.None));

        Assert.Equal(503, galat.Status);
        await using var buka = await _penyimpan.BukaAsync("2026-09-21/c.jpg", CancellationToken.None);
        using var baca = new StreamReader(buka!);
        Assert.Equal("asli", await baca.ReadToEndAsync());
    }

    [Fact]
    public async Task Pembatalan_di_tengah_penulisan_tidak_meninggalkan_berkas_setengah_jadi()
    {
        using var isi = new StreamPutus();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _penyimpan.SimpanAsync("2026-09-21/e.jpg", isi, CancellationToken.None));

        Assert.False(File.Exists(Path.Combine(_akar, "2026-09-21", "e.jpg")));
    }

    [Fact]
    public async Task Kegagalan_tulis_menjadi_503_LAMPIRAN_GAGAL_DISIMPAN()
    {
        // Berkas biasa menghalangi pembuatan subfolder dengan nama yang sama.
        Directory.CreateDirectory(_akar);
        await File.WriteAllTextAsync(Path.Combine(_akar, "2026-09-21"), "penghalang");

        var galat = await Assert.ThrowsAsync<AturanBisnisException>(
            () => _penyimpan.SimpanAsync("2026-09-21/d.jpg", Isi("x"), CancellationToken.None));

        Assert.Equal(503, galat.Status);
        Assert.Equal(KodeGalat.LampiranGagalDisimpan, galat.Kode);
    }

    [Fact]
    public void Folder_wajib_diisi()
    {
        Assert.Throws<ArgumentException>(() => new PenyimpanLampiranDisk(""));
        Assert.Throws<ArgumentNullException>(() => new PenyimpanLampiranDisk(null!));
    }

    [Fact]
    public async Task Folder_relatif_dihitung_dari_folder_keluaran_bukan_folder_kerja()
    {
        string relatif = "uji-relatif-" + Guid.NewGuid().ToString("N");
        var penyimpan = new PenyimpanLampiranDisk(relatif);
        try
        {
            await penyimpan.SimpanAsync("k/x.jpg", Isi("x"), CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, relatif, "k", "x.jpg")));
        }
        finally
        {
            Directory.Delete(Path.Combine(AppContext.BaseDirectory, relatif), recursive: true);
        }
    }

    /// <summary>Stream yang memberi sebagian isi lalu dibatalkan, seperti klien yang memutus koneksi.</summary>
    private sealed class StreamPutus : Stream
    {
        private bool _sudahMemberi;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_sudahMemberi)
            {
                throw new OperationCanceledException();
            }

            _sudahMemberi = true;
            "separuh".AsSpan().ToString().Select(c => (byte)c).ToArray().CopyTo(buffer);
            return ValueTask.FromResult(7);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
