using System.Text.Json;
using Api.Application.UseCases.Webhooks;
using Api.Presentation.Contracts.Requests;
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

    // ── DTO deserialization tests (Task 1.1) ──────────────────────────

    [Fact]
    public void MercadoPagoWebhookRequest_ShouldDeserialize_FullJsonBody()
    {
        // RED: MercadoPagoWebhookRequest does not exist yet
        const string json = """{"type":"payment","action":"payment.updated","data":{"id":"123456"}}""";

        var request = JsonSerializer.Deserialize<MercadoPagoWebhookRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("payment", request!.Type);
        Assert.Equal("payment.updated", request.Action);
        Assert.NotNull(request.Data);
        Assert.Equal("123456", request.Data!.Id?.GetString());
    }

    [Fact]
    public void MercadoPagoWebhookRequest_ShouldDeserialize_IntegerDataId()
    {
        const string json = """{"type":"payment","data":{"id":789012}}""";

        var request = JsonSerializer.Deserialize<MercadoPagoWebhookRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("payment", request!.Type);
        Assert.NotNull(request.Data);
        Assert.Equal(JsonValueKind.Number, request.Data!.Id!.Value.ValueKind);
        Assert.Equal("789012", request.Data.Id!.Value.GetRawText());
    }

    [Fact]
    public void MercadoPagoWebhookRequest_ShouldDeserialize_MissingAction()
    {
        const string json = """{"type":"payment","data":{"id":"555"}}""";

        var request = JsonSerializer.Deserialize<MercadoPagoWebhookRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("payment", request!.Type);
        Assert.Null(request.Action);
        Assert.Equal("555", request.Data!.Id?.GetString());
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn200_WhenPaymentProcessed()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        // Act
        var result = await _sut.HandleWebhook(null, "payment", "12345", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "12345", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "99999", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "11111", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "22222", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "33333", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "error-id", CancellationToken.None);

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
        var result = await _sut.HandleWebhook(null, "payment", "55555", CancellationToken.None);

        // Assert
        Assert.IsAssignableFrom<OkResult>(result);
    }

    // ── Body normalization tests (Tasks 3.1–3.5) ──────────────────────

    [Fact]
    public async Task HandleWebhook_ShouldUseBodyPayload_WhenFullJsonProvided()
    {
        // RED: payload parameter does not exist on method yet
        var payload = new MercadoPagoWebhookRequest
        {
            Type = "payment",
            Action = "payment.updated",
            Data = new MercadoPagoWebhookData
            {
                Id = JsonSerializer.Deserialize<JsonElement>(@"""123456""")
            }
        };

        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        var result = await _sut.HandleWebhook(payload, null, null, CancellationToken.None);

        Assert.IsAssignableFrom<OkResult>(result);
        _useCaseMock.Verify(
            u => u.ExecuteAsync("payment", "123456", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_ShouldNormalizeIntegerDataId()
    {
        var payload = new MercadoPagoWebhookRequest
        {
            Type = "payment",
            Data = new MercadoPagoWebhookData
            {
                Id = JsonSerializer.Deserialize<JsonElement>("789012")
            }
        };

        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "789012", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        var result = await _sut.HandleWebhook(payload, null, null, CancellationToken.None);

        Assert.IsAssignableFrom<OkResult>(result);
        _useCaseMock.Verify(
            u => u.ExecuteAsync("payment", "789012", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_ShouldUseQueryFallback_WhenNoBody()
    {
        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "111222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        var result = await _sut.HandleWebhook(null, "payment", "111222", CancellationToken.None);

        Assert.IsAssignableFrom<OkResult>(result);
        _useCaseMock.Verify(
            u => u.ExecuteAsync("payment", "111222", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_ShouldFallbackTopicToQuery_WhenBodyMissingType()
    {
        var payload = new MercadoPagoWebhookRequest
        {
            Action = "payment.updated"
        };

        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        var result = await _sut.HandleWebhook(payload, "payment", "999", CancellationToken.None);

        Assert.IsAssignableFrom<OkResult>(result);
        _useCaseMock.Verify(
            u => u.ExecuteAsync("payment", "999", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_ShouldFallbackIdToQuery_WhenBodyMissingDataId()
    {
        var payload = new MercadoPagoWebhookRequest
        {
            Type = "payment"
        };

        _useCaseMock
            .Setup(u => u.ExecuteAsync("payment", "999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessWebhookResult(WebhookOutcome.Processed));

        var result = await _sut.HandleWebhook(payload, null, "999", CancellationToken.None);

        Assert.IsAssignableFrom<OkResult>(result);
        _useCaseMock.Verify(
            u => u.ExecuteAsync("payment", "999", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Missing-input validation tests (Spec: Missing-input validation) ─

    [Fact]
    public async Task HandleWebhook_ShouldReturn400_WhenNoBodyAndNoQuery()
    {
        var result = await _sut.HandleWebhook(null, null, null, CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_ShouldReturn400_WhenBodyHasNoDataIdAndNoQueryId()
    {
        var payload = new MercadoPagoWebhookRequest
        {
            Type = "payment"
        };

        var result = await _sut.HandleWebhook(payload, null, null, CancellationToken.None);

        Assert.IsAssignableFrom<BadRequestObjectResult>(result);
    }
}
