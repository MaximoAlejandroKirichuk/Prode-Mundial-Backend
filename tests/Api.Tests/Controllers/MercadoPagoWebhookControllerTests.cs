using Api.Application.UseCases.Webhooks;
using Api.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

public sealed class MercadoPagoWebhookControllerTests
{
    private readonly Mock<ProcessMercadoPagoWebhookUseCase> _useCaseMock;
    private readonly MercadoPagoWebhookController _sut;

    public MercadoPagoWebhookControllerTests()
    {
        _useCaseMock = new Mock<ProcessMercadoPagoWebhookUseCase>(
            null!, null!, null!, null!, null!, null!);
        _sut = new MercadoPagoWebhookController(_useCaseMock.Object);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenPaymentProcessed()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        // Act
        var result = await _sut.HandleWebhook("payment", "12345", CancellationToken.None);

        // Assert
        var okResult = Assert.IsAssignableFrom<OkResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenAlreadyProcessed()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.AlreadyProcessed));

        // Act
        var result = await _sut.HandleWebhook("payment", "12345", CancellationToken.None);

        // Assert - always 200 per spec
        Assert.IsAssignableFrom<OkResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenOrphan()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "99999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Orphan));

        // Act
        var result = await _sut.HandleWebhook("payment", "99999", CancellationToken.None);

        // Assert - still 200 per spec
        Assert.IsAssignableFrom<OkResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenMismatch()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "11111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Mismatch));

        // Act
        var result = await _sut.HandleWebhook("payment", "11111", CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<OkResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenLateApproval()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "22222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.LateApproval));

        // Act
        var result = await _sut.HandleWebhook("payment", "22222", CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<OkResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenStalePayment()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "33333", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.StalePayment));

        // Act
        var result = await _sut.HandleWebhook("payment", "33333", CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<OkResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenUseCaseThrows()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _sut.HandleWebhook("payment", "error-id", CancellationToken.None);

        // Assert - controller must catch and return 200 to prevent MP retry storms
        Assert.IsAssignableFrom<OkResult>(result);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenNotificationFailed()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "55555", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.NotificationFailed));

        // Act
        var result = await _sut.HandleWebhook("payment", "55555", CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<OkResult>(result);
    }
}
