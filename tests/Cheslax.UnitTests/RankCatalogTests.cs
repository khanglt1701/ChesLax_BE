using Cheslax.Domain;

namespace Cheslax.UnitTests;

public class RankCatalogTests
{
    [Theory]
    [InlineData(0, "tap-su")]
    [InlineData(599, "tap-su")]
    [InlineData(600, "nhap-mon")]
    [InlineData(799, "nhap-mon")]
    [InlineData(800, "hoc-ky")]
    [InlineData(999, "hoc-ky")]
    [InlineData(1000, "ky-do")]
    [InlineData(1199, "ky-do")]
    [InlineData(1200, "ky-si")]
    [InlineData(1399, "ky-si")]
    [InlineData(1400, "ky-khach")]
    [InlineData(1599, "ky-khach")]
    [InlineData(1600, "ky-su")]
    [InlineData(1799, "ky-su")]
    [InlineData(1800, "cao-thu")]
    [InlineData(1999, "cao-thu")]
    [InlineData(2000, "dai-su")]
    [InlineData(2199, "dai-su")]
    [InlineData(2200, "tong-su")]
    [InlineData(2399, "tong-su")]
    [InlineData(2400, "danh-ky")]
    [InlineData(2599, "danh-ky")]
    [InlineData(2600, "ky-vuong")]
    [InlineData(2799, "ky-vuong")]
    [InlineData(2800, "ky-hoang")]
    [InlineData(2999, "ky-hoang")]
    [InlineData(3000, "ky-de")]
    [InlineData(3199, "ky-de")]
    [InlineData(3200, "ky-thanh")]
    [InlineData(5000, "ky-thanh")]
    public void GetByRating_matches_every_threshold(int rating, string expectedId)
    {
        Assert.Equal(expectedId, RankCatalog.GetByRating(rating).Id);
    }

    [Fact]
    public void Catalog_has_fifteen_ranks()
    {
        Assert.Equal(15, RankCatalog.All.Count);
        Assert.Equal(1, RankCatalog.All[0].Level);
        Assert.Equal(15, RankCatalog.Max.Level);
    }

    [Fact]
    public void Starting_rating_is_KyDo()
    {
        Assert.Equal(1000, RankCatalog.StartingRating);
        Assert.Equal("ky-do", RankCatalog.GetByRating(RankCatalog.StartingRating).Id);
    }
}
