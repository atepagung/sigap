namespace Sigap.Infrastructure.Audit;

/// <summary>
/// Keadaan sementara per permintaan yang dibagi interseptor dan <c>JejakAudit</c>: nama aksi bisnis dan
/// alasan untuk penyimpanan berikutnya. Dipisah supaya interseptor (yang dibangun bersama opsi DbContext)
/// tidak bergantung pada DbContext itu sendiri.
/// </summary>
internal sealed class KonteksAudit
{
    public string? Aksi { get; private set; }

    public string? Alasan { get; private set; }

    public void Tandai(string aksi, string? alasan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aksi);
        Aksi = aksi;
        Alasan = alasan;
    }

    public void Bersihkan()
    {
        Aksi = null;
        Alasan = null;
    }
}
