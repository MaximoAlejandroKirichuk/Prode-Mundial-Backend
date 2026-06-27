using Api.Application.Abstractions.Payments;
using Api.Infrastructure.Payments;
using Moq;

namespace Api.Tests.Infrastructure;

public sealed class MercadoPagoServiceTests
{
    private readonly Mock<IMercadoPagoApi> _apiMock;
    private readonly MercadoPagoService _sut;

    public MercadoPagoServiceTests()
    {
        _apiMock = new Mock<IMercadoPagoApi>();
        _sut = new MercadoPagoService(_apiMock.Object);
    }

    [Fact]
    public async Task CreatePreferenceAsync_ShouldReturnPreferenceData_WhenApiSucceeds()
    {
        // Arrange
        _apiMock
            .Setup(a => a.CreatePreferenceAsync(
                "Tournament registration — Juan Perez",
                1500m,
                "ARS",
                "ext-ref-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePreferenceResponse("pref-abc", "https://mp.com/pay/abc"));

        // Act
        var result = await _sut.CreatePreferenceAsync("Juan Perez", "juan@test.com", 1500m, "ARS", "ext-ref-123");

        // Assert
        Assert.Equal("pref-abc", result.PreferenceId);
        Assert.Equal("https://mp.com/pay/abc", result.PaymentUrl);
    }

    [Fact]
    public async Task CreatePreferenceAsync_ShouldUseExternalReferenceFromParameter()
    {
        // Arrange
        var capturedExternalRef = string.Empty;
        var capturedAmount = 0m;
        var capturedCurrency = string.Empty;

        _apiMock
            .Setup(a => a.CreatePreferenceAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, string, CancellationToken>(
                (title, price, currency, extRef, ct) =>
                {
                    capturedAmount = price;
                    capturedCurrency = currency;
                    capturedExternalRef = extRef;
                })
            .ReturnsAsync(new CreatePreferenceResponse("pref-xyz", "https://mp.com/xyz"));

        // Act
        await _sut.CreatePreferenceAsync("Maria", "maria@test.com", 2000m, "USD", "550e8400-e29b-41d4-a716-446655440000");

        // Assert
        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", capturedExternalRef);
        Assert.Equal(2000m, capturedAmount);
        Assert.Equal("USD", capturedCurrency);
    }

    [Fact]
    public async Task CreatePreferenceAsync_ShouldPropagateApiException()
    {
        // Arrange
        _apiMock
            .Setup(a => a.CreatePreferenceAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MP API unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreatePreferenceAsync("Test", "test@test.com", 100m, "ARS", "ref-1"));
    }

    [Fact]
    public async Task GetPaymentAsync_ShouldMapApiResponseToResult()
    {
        // Arrange
        _apiMock
            .Setup(a => a.GetPaymentAsync(12345L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentResponse(
                12345L, "approved", 1500m, "ARS", "ext-ref-1",
                new DateTimeOffset(2026, 6, 27, 10, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await _sut.GetPaymentAsync(12345L);

        // Assert
        Assert.Equal(12345L, result.PaymentId);
        Assert.Equal("approved", result.Status);
        Assert.Equal(1500m, result.Amount);
        Assert.Equal("ARS", result.Currency);
        Assert.Equal("ext-ref-1", result.ExternalReference);
    }

    [Fact]
    public async Task GetPaymentAsync_ShouldHandleRejectedPaymentStatus()
    {
        // Arrange
        _apiMock
            .Setup(a => a.GetPaymentAsync(67890L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentResponse(
                67890L, "rejected", 1500m, "ARS", null,
                DateTimeOffset.UtcNow));

        // Act
        var result = await _sut.GetPaymentAsync(67890L);

        // Assert
        Assert.Equal(67890L, result.PaymentId);
        Assert.Equal("rejected", result.Status);
        Assert.Null(result.ExternalReference);
    }

    [Fact]
    public async Task GetPaymentAsync_ShouldPropagateApiException()
    {
        // Arrange
        _apiMock
            .Setup(a => a.GetPaymentAsync(99999L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.GetPaymentAsync(99999L));
    }
}
