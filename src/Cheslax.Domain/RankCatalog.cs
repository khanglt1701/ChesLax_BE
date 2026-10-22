namespace Cheslax.Domain;

public sealed record RankDefinition(
    string Id,
    int Level,
    string Name,
    int MinRating,
    int? MaxRating);

public static class RankCatalog
{
    public const int StartingRating = 1000;
    public const int DefaultKFactor = 32;

    public static IReadOnlyList<RankDefinition> All { get; } =
    [
        new("tap-su", 1, "Tập Sự", 0, 599),
        new("nhap-mon", 2, "Nhập Môn", 600, 799),
        new("hoc-ky", 3, "Học Kỳ", 800, 999),
        new("ky-do", 4, "Kỳ Đồ", 1000, 1199),
        new("ky-si", 5, "Kỳ Sĩ", 1200, 1399),
        new("ky-khach", 6, "Kỳ Khách", 1400, 1599),
        new("ky-su", 7, "Kỳ Sư", 1600, 1799),
        new("cao-thu", 8, "Cao Thủ", 1800, 1999),
        new("dai-su", 9, "Đại Sư", 2000, 2199),
        new("tong-su", 10, "Tông Sư", 2200, 2399),
        new("danh-ky", 11, "Danh Kỳ", 2400, 2599),
        new("ky-vuong", 12, "Kỳ Vương", 2600, 2799),
        new("ky-hoang", 13, "Kỳ Hoàng", 2800, 2999),
        new("ky-de", 14, "Kỳ Đế", 3000, 3199),
        new("ky-thanh", 15, "Kỳ Thánh", 3200, null),
    ];

    public static RankDefinition Max => All[^1];

    public static int SanitizeRating(int rating) => Math.Max(0, rating);

    public static RankDefinition GetByRating(int rating)
    {
        var value = SanitizeRating(rating);
        var current = All[0];
        foreach (var rank in All)
        {
            if (value >= rank.MinRating)
            {
                current = rank;
            }
            else
            {
                break;
            }
        }

        return current;
    }

    public static RankDefinition? GetNext(int rating)
    {
        var current = GetByRating(rating);
        return current.Level >= Max.Level ? null : All.First(r => r.Level == current.Level + 1);
    }

    public static int RatingToNext(int rating)
    {
        var next = GetNext(rating);
        return next is null ? 0 : Math.Max(0, next.MinRating - SanitizeRating(rating));
    }

    public static double ProgressPercent(int rating)
    {
        var value = SanitizeRating(rating);
        var current = GetByRating(value);
        var next = GetNext(value);
        if (next is null)
        {
            return 100;
        }

        var span = next.MinRating - current.MinRating;
        if (span <= 0)
        {
            return 100;
        }

        var raw = (value - current.MinRating) / (double)span * 100;
        return Math.Clamp(raw, 0, 100);
    }
}
