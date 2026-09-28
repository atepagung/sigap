using Sigap.Domain.Umum;

namespace Sigap.Domain.Tests.Umum;

/// <summary>
/// Perbedaan kecil antara JavaScript dan .NET yang akan mengubah perilaku porting tanpa terlihat.
/// Tiap tes menunjukkan bahwa padanan bawaan .NET memang berbeda, supaya alasan adanya
/// <see cref="SemantikJs"/> tidak hilang saat seseorang tergoda "menyederhanakannya".
/// </summary>
public class SemantikJsTests
{
    private static readonly string Nel = ((char)0x85).ToString();
    private static readonly string Bom = ((char)0xFEFF).ToString();

    [Fact]
    public void Trim_memakai_himpunan_spasi_javascript()
    {
        Assert.Equal("abc", SemantikJs.Trim($"{Bom} abc\t{Bom}"));
        Assert.Equal($"{Nel}abc", SemantikJs.Trim($"{Nel}abc "));

        // Pembanding: string.Trim() .NET kebalikannya.
        Assert.Equal("abc", $"{Nel}abc".Trim());
        Assert.Equal($"{Bom}abc", $"{Bom}abc".Trim());
    }

    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -2)]
    [InlineData(0.5, 1)]
    [InlineData(1.4999, 1)]
    [InlineData(59.94, 60)]
    public void Bulatkan_seperti_math_round(double x, double harap)
    {
        Assert.Equal(harap, SemantikJs.Bulatkan(x));
    }

    [Fact]
    public void Math_round_dotnet_memang_berbeda()
    {
        Assert.Equal(2, Math.Round(2.5));
        Assert.Equal(3, SemantikJs.Bulatkan(2.5));
    }

    [Theory]
    [InlineData(" 6 ", 6)]
    [InlineData("", 0)]
    [InlineData("0x1A", 26)]
    [InlineData("0b101", 5)]
    [InlineData("1e1", 10)]
    [InlineData(".5", 0.5)]
    [InlineData("-3", -3)]
    public void DariTeks_seperti_Number(string teks, double harap)
    {
        Assert.Equal(harap, SemantikJs.DariTeks(teks));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("infinity")]
    [InlineData("1,5")]
    [InlineData("0x")]
    [InlineData("0xG")]
    [InlineData("-0x10")]
    public void DariTeks_menghasilkan_NaN(string? teks)
    {
        Assert.True(double.IsNaN(SemantikJs.DariTeks(teks)));
    }

    [Theory]
    [InlineData(10_747_904, "10.3")] // 10,25 MB: nilai tengah persis
    [InlineData(16_515_072, "15.8")] // 15,75 MB
    [InlineData(10_485_761, "10.0")]
    [InlineData(0, "0.0")]
    public void ToFixed_satu_desimal_seperti_javascript(long bytes, string harap)
    {
        Assert.Equal(harap, SemantikJs.ToFixedSatuDesimal(bytes, 1_048_576));
    }

    [Fact]
    public void ToString_F1_dotnet_memang_berbeda_pada_nilai_tengah()
    {
        Assert.Equal("10.2", 10.25.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Potong_menghitung_satuan_utf16()
    {
        Assert.Equal("ab", SemantikJs.Potong("abc", 2));
        Assert.Equal("abc", SemantikJs.Potong("abc", 190));
    }
}
