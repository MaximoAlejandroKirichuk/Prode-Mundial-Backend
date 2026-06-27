using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Application.Abstractions.Notifications;
using Api.Infrastructure.Notifications;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Api.Tests.Infrastructure;

public sealed class ResendEmailServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ResendEmailService _sut;

    public ResendEmailServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.resend.com")
        };
        _options = new ResendOptions
        {
            ApiKey = "re_test_key",
            TimeoutSeconds = 5,
            FromEmail = "noreply@prode.com"
        };
        var optionsWrapper = Options.Create(_options);
        _sut = new ResendEmailService(_httpClient, optionsWrapper);
    }

    [Fact]
    public async Task SendAccessEmailAsync_ShouldReturnSuccess_WhenApiRespondsOk()
    {
        // Arrange
        SetupHandlerResponse(HttpStatusCode.OK, "{\"id\":\"email-001\"}");

        // Act
        var result = await _sut.SendAccessEmailAsync(
            "user@example.com", "Juan Perez", "Copa America 2026",
            DateTimeOffset.UtcNow, "https://prode.com/access/123");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendAccessEmailAsync_ShouldReturnFailure_WhenApiReturnsError()
    {
        // Arrange
        SetupHandlerResponse(HttpStatusCode.BadRequest, "{\"message\":\"Invalid email\"}");

        // Act
        var result = await _sut.SendAccessEmailAsync(
            "invalid", "Juan", "Copa America",
            DateTimeOffset.UtcNow, "https://prode.com/access/1");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SendAccessEmailAsync_ShouldReturnFailure_WhenHttpClientThrows()
    {
        // Arrange: simulate a network error
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network timeout"));

        // Act
        var result = await _sut.SendAccessEmailAsync(
            "user@example.com", "Juan", "Copa",
            DateTimeOffset.UtcNow, "https://prode.com/access/1");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network timeout", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAccessEmailAsync_ShouldSetAuthHeaderWithApiKey()
    {
        // Arrange
        string? capturedAuthHeader = null;
        SetupHandlerCapture(request =>
        {
            capturedAuthHeader = request.Headers.Authorization?.ToString();
        }, HttpStatusCode.OK, "{\"id\":\"email-002\"}");

        // Act
        await _sut.SendAccessEmailAsync(
            "user@example.com", "Juan Perez", "Copa America 2026",
            DateTimeOffset.UtcNow, "https://prode.com/access/456");

        // Assert
        Assert.NotNull(capturedAuthHeader);
        Assert.Equal("Bearer", capturedAuthHeader!.Split(' ')[0]);
        Assert.Equal(_options.ApiKey, capturedAuthHeader.Split(' ')[1]);
    }

    [Fact]
    public async Task SendAccessEmailAsync_ShouldIncludeTournamentAndAccessLinkInBody()
    {
        // Arrange
        string? capturedBody = null;
        SetupHandlerCapture(request =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().Result;
        }, HttpStatusCode.OK, "{\"id\":\"email-003\"}");

        // Act
        await _sut.SendAccessEmailAsync(
            "user@example.com", "Juan Perez", "Copa America 2026",
            DateTimeOffset.UtcNow, "https://prode.com/access/abc");

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("Copa America 2026", capturedBody);
        Assert.Contains("https://prode.com/access/abc", capturedBody);
        Assert.Contains("user@example.com", capturedBody);
    }

    [Fact]
    public async Task SendAccessEmailAsync_ShouldIncludeSupportAndTroubleshootingHints()
    {
        // Arrange
        string? capturedBody = null;
        SetupHandlerCapture(request =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().Result;
        }, HttpStatusCode.OK, "{\"id\":\"email-004\"}");

        // Act
        await _sut.SendAccessEmailAsync(
            "user@example.com", "Juan Perez", "World Cup 2026",
            DateTimeOffset.UtcNow, "https://prode.com/access/xyz");

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("soporte", capturedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mismo correo", capturedBody, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Helpers ----

    private void SetupHandlerResponse(HttpStatusCode statusCode, string responseContent)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupHandlerCapture(
        Action<HttpRequestMessage> capture,
        HttpStatusCode statusCode,
        string responseContent)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capture(request))
            .ReturnsAsync(response);
    }
}
