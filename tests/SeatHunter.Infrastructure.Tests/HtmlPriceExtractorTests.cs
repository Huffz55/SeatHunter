using FluentAssertions;
using SeatHunter.Infrastructure.Providers.Price;
using Xunit;

namespace SeatHunter.Infrastructure.Tests;

public class HtmlPriceExtractorTests
{
    [Theory]
    [InlineData("{\"@type\":\"Event\",\"price\":3000,\"priceCurrency\":\"TRY\"}", 3000)]
    [InlineData("{\"price\":\"2500.00\",\"currency\":\"TRY\"}", 2500)]
    [InlineData("{\"lowestPrice\": 1750.50}", 1750.50)]
    [InlineData("{\"minPrice\":\"1200\"}", 1200)]
    public void ExtractPrice_ShouldExtractJsonLdPrices_Correctly(string jsonSnippet, decimal expectedPrice)
    {
        var result = HtmlPriceExtractor.ExtractPrice(jsonSnippet);
        result.Should().Be(expectedPrice);
    }

    [Theory]
    [InlineData("<span>3.000,00₺<small>'den itibaren</small></span>", 3000)]
    [InlineData("<div class=\"price\">3.000 TL</div>", 3000)]
    [InlineData("<b>750,00 TL</b>", 750)]
    [InlineData("<strong>1.250,50 ₺</strong>", 1250.50)]
    [InlineData("<span>350 TRY</span>", 350)]
    [InlineData("<span>₺ 4.500</span>", 4500)]
    public void ExtractPrice_ShouldExtractTurkishCurrencyFormats_Correctly(string htmlSnippet, decimal expectedPrice)
    {
        var result = HtmlPriceExtractor.ExtractPrice(htmlSnippet);
        result.Should().Be(expectedPrice);
    }

    [Fact]
    public void ExtractPrice_WhenMultiplePricesExist_ShouldReturnMinimumStartingPrice()
    {
        var html = @"
            <div class='cat1'>VIP Kategori: 6.000,00 TL</div>
            <div class='cat2'>Kategori 1: 4.500 TL</div>
            <div class='cat3'>Kategori 2: 3.000,00₺'den itibaren</div>
        ";

        var result = HtmlPriceExtractor.ExtractPrice(html);
        result.Should().Be(3000m);
    }

    [Fact]
    public void ExtractPrice_WhenNoPricePresent_ShouldReturnNull()
    {
        var html = "<div>Bu etkinlik için biletler yakında satışta.</div>";
        var result = HtmlPriceExtractor.ExtractPrice(html);
        result.Should().BeNull();
    }
}
