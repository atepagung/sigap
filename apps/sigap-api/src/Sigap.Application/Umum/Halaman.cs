namespace Sigap.Application.Umum;

/// <summary>
/// Amplop koleksi berhalaman (API_CONTRACT bagian 1.4). <b>[asumsi — bentuk amplop]</b>
/// </summary>
public sealed record Halaman<T>(IReadOnlyList<T> Data, int NomorHalaman, int Ukuran, int Total);

/// <summary>Parameter paginasi yang diterima seluruh endpoint koleksi.</summary>
public sealed record PermintaanHalaman
{
    public const int UkuranBawaan = 20;
    public const int UkuranMaks = 100;

    private readonly int _nomor = 1;
    private readonly int _ukuran = UkuranBawaan;

    /// <summary>Mulai dari 1.</summary>
    public int Halaman
    {
        get => _nomor;
        init => _nomor = value < 1 ? 1 : value;
    }

    public int Ukuran
    {
        get => _ukuran;
        init => _ukuran = value switch
        {
            < 1 => UkuranBawaan,
            > UkuranMaks => UkuranMaks,
            _ => value
        };
    }

    public int Lewati => (Halaman - 1) * Ukuran;
}
