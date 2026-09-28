namespace Kemenkeu.Iam;

/// <summary>
/// Lapis 3 (Sieve). Menandai properti DTO respons yang di-<c>null</c>-kan untuk peran yang tidak
/// berhak. Atribut hanya membawa KUNCI; siapa yang boleh melihat ada di kebijakan IAM (data),
/// bukan di kode. Properti tetap muncul di JSON dengan nilai <c>null</c>, tidak dihapus.
/// [ASUMSI] Nama dan bentuk atribut ini tebakan kita — lihat DUMMY_REGISTRY.md.
/// </summary>
/// <remarks>
/// Hanya berlaku saat respons diserialisasi. Nilai asli tetap ada di objek DTO di memori — jangan
/// menulis DTO itu ke log atau cache.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SieveAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
