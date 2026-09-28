using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigap.Application.Audit;

namespace Sigap.Api.Umum;

/// <summary>
/// Satu-satunya tempat akses baca data sensitif dicatat (API_CONTRACT 1.7): setiap respons sukses yang nilainya
/// <see cref="IAksesTercatat"/> meninggalkan jejak <c>DIAKSES</c> sebelum dikirim. Terpasang global, jadi tidak ada
/// endpoint yang dapat lupa. Bila jejak gagal ditulis, respons ikut gagal (500): akses tanpa jejak tidak terjadi.
/// Hanya rujukan yang dicatat, bukan isi respons — isinya masih memuat nilai sebelum Sieve.
/// </summary>
internal sealed class CatatAksesFilter(IJejakAudit jejak) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext konteks, ResultExecutionDelegate lanjut)
    {
        if (konteks.Result is ObjectResult { Value: IAksesTercatat data } hasil
            && (hasil.StatusCode ?? StatusCodes.Status200OK) is >= 200 and < 300)
        {
            var rujukan = data.RujukanAkses();
            await jejak.CatatAksesAsync(rujukan.Entitas, rujukan.EntitasId, rujukan.Keterangan, konteks.HttpContext.RequestAborted);
        }

        await lanjut();
    }
}
