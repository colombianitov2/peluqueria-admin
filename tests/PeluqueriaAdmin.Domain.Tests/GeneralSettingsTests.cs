using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class GeneralSettingsTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateDefault_UsesApprovedExactValues()
    {
        GeneralSettings settings = GeneralSettings.CreateDefault(UtcNow);

        Assert.Equal(GeneralSettings.SingletonId, settings.Id);
        Assert.Equal(1_200, settings.WeeklyUsageFee.MinorUnits);
        Assert.Equal(12.00m, settings.WeeklyUsageFee.ToDecimal());
        Assert.Equal(2_000, settings.CollaboratorProfit.BasisPoints);
        Assert.Equal(20.00m, settings.CollaboratorProfit.ToPercent());
        Assert.Equal(0, settings.OptionalSuppliesMonthlyBudget.MinorUnits);
        Assert.Equal(0, settings.TotalChairs);
        Assert.Equal("USD", settings.CurrencyCode.Value);
        Assert.Equal(UtcNow, settings.CreatedUtc);
        Assert.Equal(UtcNow, settings.UpdatedUtc);
    }

    [Fact]
    public void Money_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromDecimal(-0.01m));
    }

    [Fact]
    public void Money_RejectsMoreThanTwoDecimals()
    {
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(12.001m));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Percentage_RejectsValuesOutsideApprovedRange(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.FromPercent((decimal)value));
    }

    [Fact]
    public void Percentage_RejectsMoreThanTwoDecimals()
    {
        Assert.Throws<ArgumentException>(() => Percentage.FromPercent(20.001m));
    }

    [Fact]
    public void Update_RejectsNegativeChairs()
    {
        GeneralSettings settings = GeneralSettings.CreateDefault(UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Update(
            Money.FromDecimal(12m),
            Percentage.FromPercent(20m),
            Money.FromDecimal(0m),
            -1,
            CurrencyCode.From("USD"),
            UtcNow.AddMinutes(1)));
    }

    [Fact]
    public void CurrencyCode_NormalizesWhitespaceAndCase()
    {
        Assert.Equal("USD", CurrencyCode.From(" usd ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U1D")]
    [InlineData("U$D")]
    public void CurrencyCode_RejectsInvalidValues(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => CurrencyCode.From(value));
    }
}
