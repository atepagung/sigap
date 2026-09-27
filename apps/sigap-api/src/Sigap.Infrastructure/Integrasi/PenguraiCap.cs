using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>Satu butir RSS peringatan dini cuaca BMKG. <see cref="Id"/> = <c>guid</c> = identifier CAP.</summary>
internal sealed record ButirRssCap(string Id, string Judul, string Deskripsi, string? Tautan, DateTimeOffset? Terbit);

/// <summary>
/// Penguraian feed peringatan dini cuaca BMKG: RSS daftar peringatan (<c>alerts/nowcast/id/rss.xml</c>) dan berkas
/// CAP 1.2 per peringatan. Sama seperti <see cref="PenguraiBmkg"/>: toleran terhadap isian yang hilang, tegas
/// terhadap XML yang rusak (<see cref="XmlException"/>, supaya pemanggil memakai cadangan).
///
/// <para>
/// XML dari luar dibaca tanpa DTD dan tanpa resolver (tidak ada entitas eksternal, tidak ada "billion laughs"),
/// dan dibatasi ukurannya: berkas CAP BMKG memuat poligon wilayah yang panjang (puluhan KB), tetapi tidak
/// pernah megabita.
/// </para>
/// </summary>
internal static partial class PenguraiCap
{
    public const long BatasKarakter = 2_000_000;

    private static readonly XNamespace Cap = "urn:oasis:names:tc:emergency:cap:1.2";

    private static readonly string[] FormatIso = ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    public static IReadOnlyList<ButirRssCap> UraiRss(string xml)
    {
        var channel = Muat(xml).Root?.Element("channel");
        if (channel is null)
        {
            return [];
        }

        return
        [
            .. channel.Elements("item")
                .Select(i => new ButirRssCap(
                    Teks(i.Element("guid")) ?? string.Empty,
                    Teks(i.Element("title")) ?? string.Empty,
                    Teks(i.Element("description")) ?? string.Empty,
                    Teks(i.Element("link")),
                    WaktuRss(Teks(i.Element("pubDate")))))
                .Where(b => b.Id.Length > 0)
        ];
    }

    /// <summary>
    /// Satu berkas CAP menjadi <see cref="PeringatanCuaca"/>. <c>null</c> bila peringatan itu bukan untuk
    /// ditampilkan: bukan <c>status=Actual</c> (latihan, uji), bukan <c>scope=Public</c>, atau <c>msgType=Cancel</c>.
    /// </summary>
    public static PeringatanCuaca? UraiPeringatan(string xml, string? tautan)
    {
        var alert = Muat(xml).Root;
        if (alert is null || alert.Name != Cap + "alert")
        {
            throw new XmlException("Akar dokumen bukan <alert> CAP 1.2.");
        }

        if (!Sama(Isi(alert, "status"), "Actual") || !Sama(Isi(alert, "scope"), "Public") || Sama(Isi(alert, "msgType"), "Cancel"))
        {
            return null;
        }

        // CAP boleh memuat beberapa <info> per bahasa; feed "id" BMKG memuat satu.
        var info = alert.Elements(Cap + "info").FirstOrDefault(i => Isi(i, "language")?.StartsWith("id", StringComparison.OrdinalIgnoreCase) ?? true)
            ?? alert.Element(Cap + "info");

        return new PeringatanCuaca(
            Id: Isi(alert, "identifier") ?? string.Empty,
            Judul: Isi(info, "headline") ?? Isi(info, "event") ?? string.Empty,
            Peristiwa: Isi(info, "event") ?? string.Empty,
            Wilayah: string.Join(", ", info?.Elements(Cap + "area").Select(a => Isi(a, "areaDesc")).OfType<string>() ?? []),
            Deskripsi: Isi(info, "description") ?? string.Empty,
            Keparahan: Isi(info, "severity") ?? string.Empty,
            Urgensi: Isi(info, "urgency") ?? string.Empty,
            Kepastian: Isi(info, "certainty") ?? string.Empty,
            Terkirim: WaktuIso(Isi(alert, "sent")),
            MulaiBerlaku: WaktuIso(Isi(info, "effective")),
            Kedaluwarsa: WaktuIso(Isi(info, "expires")),
            Tautan: tautan,
            Infografis: Isi(info, "web"));
    }

    /// <summary>
    /// Peringatan yang hanya terbaca dari RSS karena berkas CAP-nya tidak terjangkau: tanpa kedaluwarsa dan
    /// tanpa isian CAP lain. Lebih baik ditampilkan dengan keterangan seadanya daripada disembunyikan.
    /// </summary>
    public static PeringatanCuaca DariRss(ButirRssCap b) =>
        new(b.Id, b.Judul, string.Empty, string.Empty, b.Deskripsi, string.Empty, string.Empty, string.Empty,
            b.Terbit, null, null, b.Tautan, null);

    private static XDocument Muat(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var pengaturan = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = BatasKarakter
        };

        // BMKG kadang mengirim BOM (lihat PenguraiBmkg); XmlReader atas string menolak BOM di tengah teks.
        using var pembaca = XmlReader.Create(new StringReader(xml.TrimStart('﻿')), pengaturan);
        return XDocument.Load(pembaca);
    }

    private static string? Isi(XElement? induk, string nama) => Teks(induk?.Element(Cap + nama));

    private static string? Teks(XElement? e) => e is null || string.IsNullOrWhiteSpace(e.Value) ? null : e.Value.Trim();

    private static bool Sama(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Hanya ISO 8601 berzona (wajib menurut CAP 1.2), seperti <c>PenilaiKejadianBmkg.TryUraiWaktu</c>: teks tanpa
    /// zona akan dibaca sebagai waktu setempat server, sehingga kedaluwarsa peringatan bergantung pada mesin.
    /// </summary>
    internal static DateTimeOffset? WaktuIso(string? teks)
    {
        if (string.IsNullOrWhiteSpace(teks))
        {
            return null;
        }

        string t = teks.Trim();
        if (t.EndsWith('Z'))
        {
            t = t[..^1] + "+00:00";
        }

        return DateTimeOffset.TryParseExact(t, FormatIso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var w) ? w : null;
    }

    [GeneratedRegex(@"\s(?<jam>[+-]\d{2})(?<menit>\d{2})$")]
    private static partial Regex ZonaRfc822();

    /// <summary><c>pubDate</c> RSS (RFC 822, mis. <c>Sun, 27 Sep 2026 10:25:00 +0800</c>); zona wajib ada.</summary>
    internal static DateTimeOffset? WaktuRss(string? teks)
    {
        if (string.IsNullOrWhiteSpace(teks))
        {
            return null;
        }

        string t = teks.Trim();
        if (t.EndsWith(" GMT", StringComparison.Ordinal) || t.EndsWith(" UT", StringComparison.Ordinal))
        {
            t = t[..t.LastIndexOf(' ')] + " +00:00";
        }
        else
        {
            t = ZonaRfc822().Replace(t, " ${jam}:${menit}");
        }

        return DateTimeOffset.TryParseExact(t, "ddd, dd MMM yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var w)
            ? w
            : null;
    }
}
