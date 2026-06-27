using Api.Domain.Entities;

namespace Api.Tests.Domain;

public sealed class TournamentTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithGivenValues()
    {
        // Act
        var tournament = new Tournament("Copa América 2026", 5000m, "ARS");

        // Assert
        Assert.NotEqual(Guid.Empty, tournament.Id);
        Assert.Equal("Copa América 2026", tournament.Name);
        Assert.Equal(5000m, tournament.PriceAmount);
        Assert.Equal("ARS", tournament.Currency);
        Assert.True(tournament.Active);
        Assert.Null(tournament.ClosedAt);
    }

    [Fact]
    public void IsActive_ShouldReturnTrue_WhenActiveFlagIsTrue()
    {
        var tournament = new Tournament("Mundial 2026", 10000m, "USD");

        Assert.True(tournament.IsActive());
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenActiveFlagIsFalse()
    {
        var tournament = new Tournament("Mundial 2026", 10000m, "USD");
        tournament.Deactivate(DateTimeOffset.UtcNow);

        Assert.False(tournament.IsActive());
    }

    [Fact]
    public void Deactivate_ShouldSetClosedAtAndActiveFlag()
    {
        var tournament = new Tournament("Mundial 2026", 10000m, "USD");
        var closedAt = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        tournament.Deactivate(closedAt);

        Assert.False(tournament.Active);
        Assert.Equal(closedAt, tournament.ClosedAt);
    }

    [Fact]
    public void Deactivate_ShouldReplacePreviousClosedAt()
    {
        var tournament = new Tournament("Mundial 2026", 10000m, "USD");
        var firstClose = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var secondClose = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        tournament.Deactivate(firstClose);
        tournament.Deactivate(secondClose);

        Assert.False(tournament.Active);
        Assert.Equal(secondClose, tournament.ClosedAt);
    }
}
