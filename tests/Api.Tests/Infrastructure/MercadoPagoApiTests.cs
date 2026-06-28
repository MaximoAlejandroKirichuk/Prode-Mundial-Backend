using Api.Infrastructure.Payments;
using MercadoPago.Client.Preference;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Options;
using Moq;

namespace Api.Tests.Infrastructure;

public sealed class MercadoPagoApiTests
{
    [Fact]
    public async Task CreatePreferenceAsync_ShouldSetBackUrlsAndAutoReturn_WhenConfigured()
    {
        // Arrange
        var options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = "test-token",
            UseSandbox = true,
            BackUrls = new BackUrlsConfig
            {
                Success = "https://app.example.com/pago/exito",
                Failure = "https://app.example.com/pago/error",
                Pending = "https://app.example.com/pago/pendiente"
            },
            AutoReturn = "approved"
        });

        PreferenceRequest? capturedRequest = null;
        var preferenceClientMock = new Mock<IPreferenceClient>();
        preferenceClientMock
            .Setup(c => c.CreateAsync(It.IsAny<PreferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PreferenceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new Preference { Id = "pref-abc", InitPoint = "https://mp.com/pay/abc" });

        var sut = new MercadoPagoApi(options, preferenceClientMock.Object);

        // Act
        await sut.CreatePreferenceAsync("Test", 100m, "ARS", "ext-ref-1", CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.BackUrls);
        Assert.Equal("https://app.example.com/pago/exito", capturedRequest.BackUrls!.Success);
        Assert.Equal("https://app.example.com/pago/error", capturedRequest.BackUrls!.Failure);
        Assert.Equal("https://app.example.com/pago/pendiente", capturedRequest.BackUrls!.Pending);
        Assert.Equal("approved", capturedRequest.AutoReturn);
    }

    [Fact]
    public async Task CreatePreferenceAsync_ShouldNotSetBackUrls_WhenNotConfigured()
    {
        // Arrange
        var options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = "test-token",
            UseSandbox = true,
            BackUrls = null,
            AutoReturn = null
        });

        PreferenceRequest? capturedRequest = null;
        var preferenceClientMock = new Mock<IPreferenceClient>();
        preferenceClientMock
            .Setup(c => c.CreateAsync(It.IsAny<PreferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PreferenceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new Preference { Id = "pref-xyz", InitPoint = "https://mp.com/pay/xyz" });

        var sut = new MercadoPagoApi(options, preferenceClientMock.Object);

        // Act
        await sut.CreatePreferenceAsync("Test", 100m, "ARS", "ext-ref-2", CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Null(capturedRequest!.BackUrls);
        Assert.Null(capturedRequest.AutoReturn);
    }

    [Fact]
    public async Task CreatePreferenceAsync_ShouldSetOnlyConfiguredBackUrls_WhenPartiallyConfigured()
    {
        // Arrange
        var options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = "test-token",
            UseSandbox = true,
            BackUrls = new BackUrlsConfig
            {
                Success = "https://app.example.com/pago/exito",
                Failure = null,
                Pending = null
            },
            AutoReturn = null
        });

        PreferenceRequest? capturedRequest = null;
        var preferenceClientMock = new Mock<IPreferenceClient>();
        preferenceClientMock
            .Setup(c => c.CreateAsync(It.IsAny<PreferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PreferenceRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new Preference { Id = "pref-abc", InitPoint = "https://mp.com/pay/abc" });

        var sut = new MercadoPagoApi(options, preferenceClientMock.Object);

        // Act
        await sut.CreatePreferenceAsync("Test", 100m, "ARS", "ext-ref-3", CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.BackUrls);
        Assert.Equal("https://app.example.com/pago/exito", capturedRequest.BackUrls!.Success);
        Assert.Null(capturedRequest.BackUrls!.Failure);
        Assert.Null(capturedRequest.BackUrls!.Pending);
        Assert.Null(capturedRequest.AutoReturn);
    }
}
