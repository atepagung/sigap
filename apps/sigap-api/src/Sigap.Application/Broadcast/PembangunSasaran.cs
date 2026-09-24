using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Broadcast;
using Sigap.Domain.Umum;

namespace Sigap.Application.Broadcast;

/// <summary>
/// Menyusun kriteria (<see cref="SasaranPemicu"/>) dan daftar kandidat unit untuk #12 dan #13, sama
/// persis kecuali #13 mengunci hasilnya. Lingkup dipilih lewat <c>DataScope.Terluas()</c>, <b>bukan</b>
/// dengan membaca peran pemanggil sendiri — lihat ACCESS_RULES.md A1.
/// </summary>
internal static class PembangunSasaran
{
    public sealed record Hasil(ScopeGrant Grant, SasaranPemicu Kriteria, IReadOnlyList<RingkasUnit> Kandidat);

    public static async Task<Hasil> ResolveAsync(
        ICurrentUserContext pengguna, IBroadcastStore store, PenyempitPermintaan? penyempit, CancellationToken ct)
    {
        var grant = pengguna.GetScope(Izin.BroadcastTrigger).Terluas()
            ?? throw new TidakBerwenangException("Peran Anda tidak memegang lingkup pemicu safety check.");

        if (grant.Note is not null)
        {
            throw new AturanBisnisException(
                KodeGalat.DataUnitPemicuTidakLengkap,
                "Data unit pemicu tidak lengkap",
                "Provinsi atau Eselon I unit Anda belum tercatat, sehingga lingkup pemicuan tidak dapat ditentukan.",
                422);
        }

        var tidakBerlaku = AturanPenyempit.TidakBerlaku(
            grant.Profile, penyempit?.UnitId, penyempit?.Provinsi, penyempit?.KabupatenKota, penyempit?.EselonI);
        if (tidakBerlaku.Count > 0)
        {
            throw new AturanBisnisException(
                KodeGalat.PenyempitTidakBerlaku,
                "Penyempit tidak berlaku",
                $"Field berikut tidak berlaku untuk lingkup Anda: {string.Join(", ", tidakBerlaku)}.",
                StatusHttp.PermintaanTidakSah);
        }

        switch (grant.Profile)
        {
            case "UNIT":
                {
                    var unit = await store.UnitAsync(pengguna.UnitId!, ct) ?? throw new TidakDitemukanException("Unit Anda tidak ditemukan.");
                    return new Hasil(grant, SasaranPemicu.KeUnit(new UnitPemicu(unit.Id, unit.Nama, unit.Provinsi, unit.EselonI)), [unit]);
                }

            case "WILAYAH" when !string.IsNullOrEmpty(penyempit?.UnitId):
                {
                    var unit = await store.UnitAsync(penyempit.UnitId, ct);
                    if (unit is null || !grant.Area.UnitIds.Contains(unit.Id))
                    {
                        // Di luar provinsinya: 404, bukan pesan "hanya boleh di provinsi Anda" (ACCESS_RULES A3 butir 4).
                        throw new TidakDitemukanException("Unit tidak ditemukan.");
                    }

                    return new Hasil(grant, SasaranPemicu.KeUnit(new UnitPemicu(unit.Id, unit.Nama, unit.Provinsi, unit.EselonI)), [unit]);
                }

            case "WILAYAH":
                {
                    var kriteria = SasaranPemicu.KeWilayah(pengguna.Provinsi!, penyempit?.KabupatenKota, penyempit?.EselonI);
                    var kandidat = await store.KandidatAsync(false, grant.Area.UnitIds, null, penyempit?.KabupatenKota, penyempit?.EselonI, ct);
                    return new Hasil(grant, kriteria, kandidat);
                }

            case "ESELON_I":
                {
                    var unitPemicu = new UnitPemicu(pengguna.UnitId!, string.Empty, pengguna.Provinsi, pengguna.EselonIKey);
                    var kriteria = SasaranPemicu.KeEselonI(unitPemicu, penyempit?.Provinsi, penyempit?.KabupatenKota);
                    var kandidat = await store.KandidatAsync(false, grant.Area.UnitIds, penyempit?.Provinsi, penyempit?.KabupatenKota, null, ct);
                    return new Hasil(grant, kriteria, kandidat);
                }

            case "NASIONAL":
                {
                    var kriteria = SasaranPemicu.KeNasional(penyempit?.Provinsi, penyempit?.KabupatenKota, penyempit?.EselonI);
                    var kandidat = await store.KandidatAsync(true, [], penyempit?.Provinsi, penyempit?.KabupatenKota, penyempit?.EselonI, ct);
                    return new Hasil(grant, kriteria, kandidat);
                }

            default:
                throw new InvalidOperationException($"Profil lingkup '{grant.Profile}' tidak dikenal untuk trigger.");
        }
    }
}
