namespace Sigap.Domain.Notifikasi;

/// <summary>Tingkat sebuah peringatan (<c>GET /notifikasi</c>, #43), terurut dari yang paling mendesak.</summary>
public static class TingkatPeringatan
{
    public const string Genting = "GENTING";
    public const string Peringatan = "PERINGATAN";
    public const string Informasi = "INFORMASI";

    /// <summary>Urutan tampil: Genting dulu, lalu Peringatan, lalu Informasi.</summary>
    public static int Urutan(string tingkat) => tingkat switch
    {
        Genting => 0,
        Peringatan => 1,
        Informasi => 2,
        _ => 3
    };
}
