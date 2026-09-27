using System.Net;
using Sigap.Api.Tests.Basisdata;
using Sigap.Api.Tests.Monitor;

namespace Sigap.Api.Tests.Scope;

/// <summary>Detail unit di monitor: Perwakilan provinsi lain dan Subkoordinator Eselon I lain tidak boleh membedakan unit yang ada dari yang tidak ada.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class KeberadaanMonitorTests(AplikasiUjiDb app) : TesMonitor(app)
{
    [FaktaDb]
    public async Task Detail_unit_di_luar_provinsi_dan_Eselon_I_tidak_membedakan_ada_dari_tidak_ada()
    {
        var asal = await LingkunganBaruAsync("Riau", "djp");
        var lain = await LingkunganBaruAsync("Sumatera Utara", "djbc");

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Perwakilan, $"{Monitor}/unit/{asal.UnitId}")).Respons.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Subkoordinator, $"{Monitor}/unit/{asal.UnitId}")).Respons.StatusCode);

        await TandaJawaban.TegaskanSamaAsync("GET /monitor/unit/{unitId} (Perwakilan provinsi lain)", asal.UnitId, u => AmbilAsync(lain.Perwakilan, $"{Monitor}/unit/{u}"));
        await TandaJawaban.TegaskanSamaAsync("GET /monitor/unit/{unitId} (Subkoordinator Eselon I lain)", asal.UnitId, u => AmbilAsync(lain.Subkoordinator, $"{Monitor}/unit/{u}"));
    }

    [FaktaDb]
    public async Task Provinsi_sama_tetapi_Eselon_I_lain_tidak_membedakan_ada_dari_tidak_ada_bagi_Subkoordinator()
    {
        var asal = await LingkunganBaruAsync("Riau", "djp");
        var lain = await LingkunganBaruAsync("Riau", "djbc");

        await TandaJawaban.TegaskanSamaAsync("GET /monitor/unit/{unitId} (Eselon I lain, provinsi sama)", asal.UnitId, u => AmbilAsync(lain.Subkoordinator, $"{Monitor}/unit/{u}"));
    }
}
