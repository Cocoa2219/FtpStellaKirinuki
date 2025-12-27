using System.Numerics;
using Spectre.Console;

namespace FtpStellaKirinuki;

public enum ChannelType
{
    AyatsunoYuni,
    SakihaneHuya,
    SirayukiHina,
    NenekoMashiro,
    AkaneLize,
    ArahashiTabi,
    TenkoShibuki,
    AokumoRin,
    HanakoNana,
    YuzuhaRiko
}

public static class ChannelTypeExtensions
{
    public static IReadOnlyDictionary<ChannelType, string> Names { get; } = new Dictionary<ChannelType, string>
    {
        { ChannelType.AyatsunoYuni, "아야츠노 유니" },
        { ChannelType.SakihaneHuya, "사키하네 후야" },
        { ChannelType.SirayukiHina, "시라유키 히나" },
        { ChannelType.NenekoMashiro, "네네코 마시로" },
        { ChannelType.AkaneLize, "아카네 리제" },
        { ChannelType.ArahashiTabi, "아라하시 타비" },
        { ChannelType.TenkoShibuki, "텐코 시부키" },
        { ChannelType.AokumoRin, "아오쿠모 린" },
        { ChannelType.HanakoNana, "하나코 나나" },
        { ChannelType.YuzuhaRiko, "유즈하 리코" }
    };

    public static IReadOnlyDictionary<ChannelType, Gradient> ChannelColors { get; }
        = new Dictionary<ChannelType, Gradient>
        {
            {
                ChannelType.AyatsunoYuni,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#ad99f4") },
                    { 0.65f, Color.FromHex("#afa8f6") },
                    { 1.0f, Color.FromHex("#a6b7f1") }
                })
            },
            {
                ChannelType.SakihaneHuya,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#5c4070") },
                    { 0.5f, Color.FromHex("#775396") },
                    { 1.0f, Color.FromHex("#8c75b1") }
                })
            },
            {
                ChannelType.SirayukiHina,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#e8686b") },
                    { 1.0f, Color.FromHex("#ff9680") }
                })
            },
            {
                ChannelType.NenekoMashiro,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#25282a") },
                    { 1.0f, Color.FromHex("#444444") }
                })
            },
            {
                ChannelType.AkaneLize,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#000000") },
                    { 1.0f, Color.FromHex("#890000") }
                })
            },
            {
                ChannelType.ArahashiTabi,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#89d1ff") },
                    { 1.0f, Color.FromHex("#96baf2") }
                })
            },
            {
                ChannelType.TenkoShibuki,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#dab9fb") },
                    { 1.0f, Color.FromHex("#bc97e8") }
                })
            },
            {
                ChannelType.AokumoRin,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#7abfe5") },
                    { 1.0f, Color.FromHex("#0a4df3") }
                })
            },
            {
                ChannelType.HanakoNana,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#fbb4ca") },
                    { 1.0f, Color.FromHex("#ff8ca1") }
                })
            },
            {
                ChannelType.YuzuhaRiko,
                new Gradient(new Dictionary<float, Color>
                {
                    { 0.0f, Color.FromHex("#8fe1b0") },
                    { 1.0f, Color.FromHex("#058891") }
                })
            }
        };


    public static string GetColoredName(this ChannelType type)
    {
        var name = Names[type];
        var color = ChannelColors[type];

        return color.GetText(name);
    }

    public static string GetGradientText(string text, IDictionary<float, Color> colors)
    {
        var gradient = new Gradient(colors);
        var result = string.Empty;
        var length = text.Length;

        for (var i = 0; i < length; i++)
        {
            var t = (float)i / (length - 1);
            var color = gradient.GetColorAt(t);
            result += Markup.Escape($"{AnsiRgb.Fg(color)}{text[i]}");
        }

        return result;
    }

    public class Gradient(IDictionary<float, Color> colors)
    {
        private readonly SortedDictionary<float, Color> _colors = new(colors);

        public Color GetColorAt(float t)
        {
            if (t <= 0) return _colors.First().Value;
            if (t >= 1) return _colors.Last().Value;

            var lower = _colors.Last(kv => kv.Key <= t);
            var upper = _colors.First(kv => kv.Key >= t);

            if (Math.Abs(lower.Key - upper.Key) < 0.1f) return lower.Value;

            var ratio = (t - lower.Key) / (upper.Key - lower.Key);
            return lower.Value.Lerp(upper.Value, ratio);
        }

        public string GetText(string text)
        {
            var result = string.Empty;
            var length = text.Length;

            for (var i = 0; i < length; i++)
            {
                var t = (float)i / (length - 1);
                var color = GetColorAt(t);
                result += Markup.Escape($"{AnsiRgb.Fg(color)}{text[i]}");
            }

            return result;
        }
    }

    public static IReadOnlyDictionary<ChannelType, string> ChannelPlaylistIds { get; } =
        new Dictionary<ChannelType, string>
        {
            { ChannelType.AyatsunoYuni, "UUlbYIn9LDbbFZ9w2shX3K0g" },
            { ChannelType.SakihaneHuya, "UU0YQnenKBCu5sGb7H61n6HA" },
            { ChannelType.SirayukiHina, "UU1afpiIuBDcjYlmruAa0HiA" },
            { ChannelType.NenekoMashiro, "UU_eeSpMBz8PG4ssdBPnP07g" },
            { ChannelType.AkaneLize, "UU7-m6jQLinZQWIbwm9W-1iw" },
            { ChannelType.ArahashiTabi, "UUAHVQ44O81aehLWfy9O6Elw" },
            { ChannelType.TenkoShibuki, "UUYxLMfeX1CbMBll9MsGlzmw" },
            { ChannelType.AokumoRin, "UUQmcltnre6aG9SkDRYZqFIg" },
            { ChannelType.HanakoNana, "UUcA21_PzN1EhNe7xS4MJGsQ" },
            { ChannelType.YuzuhaRiko, "UUj0c1jUr91dTetIQP2pFeLA" }
        };
}

public static class ColorExtensions
{
    public static string FromHex(this Vector3 color)
    {
        return $"#{(int)color.X:X2}{(int)color.Y:X2}{(int)color.Z:X2}";
    }

    public static Color Lerp(this Color from, Color to, float t)
    {
        var r = (byte)(from.R + (to.R - from.R) * t);
        var g = (byte)(from.G + (to.G - from.G) * t);
        var b = (byte)(from.B + (to.B - from.B) * t);
        return new Color(r, g, b);
    }
}

public static class AnsiRgb
{
    public static string Fg(Color c)
        => $"\u001b[38;2;{c.R};{c.G};{c.B}m";

    public const string Reset = "\u001b[0m";
}
