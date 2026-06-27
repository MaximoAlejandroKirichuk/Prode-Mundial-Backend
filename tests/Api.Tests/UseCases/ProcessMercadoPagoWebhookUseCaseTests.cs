using Api.Application.Abstractions.Notifications;
using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Application.UseCases.Webhooks;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Moq;

namespace Api.Tests.UseCases;

public sealed class ProcessMercadoPagoWebhookUseCaseTests
{
    private readonly Mock<IMercadoPagoService> _mpServiceMock;
    private readonly Mock<IRegistrationRepository> _registrationRepoMock;
    private readonly Mock<IWebhookIdempotencyRepository> _idempotencyRepoMock;
    private readonly Mock<IRegistrationPaymentRepository> _paymentRepoMock;
    private readonly Mock<IRegistrationAnomalyRepository> _anomalyRepoMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly ProcessMercadoPagoWebhookUseCase _sut;

    private readonly Guid _tournamentId;
    private readonly Guid _registrationId;
    private readonly Tournament _tournament;
    private readonly Registration _registration;

    public ProcessMercadoPagoWebhookUseCaseTests()
    {
        _mpServiceMock = new Mock<IMercadoPagoService>();
        _registrationRepoMock = new Mock<IRegistrationRepository>();
        _idempotencyRepoMock = new Mock<IWebhookIdempotencyRepository>();
        _paymentRepoMock = new Mock<IRegistrationPaymentRepository>();
        _anomalyRepoMock = new Mock<IRegistrationAnomalyRepository>();
        _emailServiceMock = new Mock<IEmailService>();

        _sut = new ProcessMercadoPagoWebhookUseCase(
            _mpServiceMock.Object,
            _registrationRepoMock.Object,
            _idempotencyRepoMock.Object,
            _paymentRepoMock.Object,
            _anomalyRepoMock.Object,
            _emailServiceMock.Object);

        _tournamentId = Guid.NewGuid();
        _tournament = new Tournament("Copa America 2026", 1500m, "ARS");

        _registrationId = Guid.NewGuid();
        _registration = new Registration("Juan Perez", "juan@example.com", _tournamentId);
    }

    // ====================================================================
    // Blocker 3: Transient MP fetch failure
    // ====================================================================

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTransientFailure_WhenMercadoPagoFails()
    {
        // Arrange
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(12345L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("MP timeout"));

        // Act
        var result = await _sut.ExecuteAsync("payment", "12345");

        // Assert
        Assert.Equal(WebhookOutcome.TransientFailure, result.Outcome);

        // Verify no persistence occurred
        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Never);
        _idempotencyRepoMock.Verify(
            r => r.IsDuplicateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ====================================================================
    // Blocker 2: Idempotency by (paymentId, status)
    // ====================================================================

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAlreadyProcessed_WhenSameStatusIsDuplicate()
    {
        // Arrange
        var paymentId = "12345";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(12345L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                12345L, "approved", 1500m, "ARS", _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.AlreadyProcessed, result.Outcome);

        // Verify no further processing
        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Never);
        _registrationRepoMock.Verify(
            r => r.GetByExternalReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowDifferentStatus_WhenPaymentIdExists()
    {
        // Arrange: a "pending" delivery already exists; "approved" should not be blocked
        var paymentId = "12345";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(12345L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                12345L, "approved", 1500m, "ARS", _registrationId.ToString(), DateTimeOffset.UtcNow));

        // "pending" was already processed, but "approved" is new
        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegistrationWithTournament());

        _emailServiceMock
            .Setup(e => e.SendAccessEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSendResult(true));

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert: should process the "approved" notification
        Assert.Equal(WebhookOutcome.Processed, result.Outcome);

        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.Is<WebhookDelivery>(d => d.Status == "approved"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ====================================================================
    // Blocker 2: Non-approved doesn't downgrade already-paid registration
    // ====================================================================

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAlreadyProcessed_WhenLateNonApprovedArrivesAfterApproved()
    {
        // Arrange: "pending" notification arrives after "approved" was already processed
        var paymentId = "99997";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(99997L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                99997L, "pending", 1500m, "ARS", _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Registration is already Paid
        var paidRegistration = CreateRegistrationWithTournament();
        paidRegistration.MarkAsPaid(DateTimeOffset.UtcNow.AddHours(-1));

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paidRegistration);

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.AlreadyProcessed, result.Outcome);

        // Payment snapshot still saved (for audit)
        _paymentRepoMock.Verify(
            p => p.AddAsync(It.IsAny<RegistrationPayment>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Delivery is marked processed
        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ====================================================================
    // Original tests (adapted for restructured flow)
    // ====================================================================

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOrphan_WhenExternalReferenceNotFound()
    {
        // Arrange
        var paymentId = "99999";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(99999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(99999L, "approved", 1500m, "ARS", "missing-ref", DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync("missing-ref", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Registration?)null);

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.Orphan, result.Outcome);

        _anomalyRepoMock.Verify(
            a => a.AddAsync(It.Is<RegistrationAnomaly>(an => an.Type == "OrphanWebhook"), It.IsAny<CancellationToken>()),
            Times.Once);
        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMismatch_WhenAmountDiffers()
    {
        // Arrange
        var paymentId = "88888";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(88888L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                88888L, "approved", 500m /* wrong amount */, "ARS",
                _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegistrationWithTournament());

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.Mismatch, result.Outcome);

        _anomalyRepoMock.Verify(
            a => a.AddAsync(It.Is<RegistrationAnomaly>(an => an.Type == "IntegrityMismatch"), It.IsAny<CancellationToken>()),
            Times.Once);
        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMismatch_WhenCurrencyDiffers()
    {
        // Arrange
        var paymentId = "77777";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(77777L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                77777L, "approved", 1500m, "USD" /* wrong currency */,
                _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegistrationWithTournament());

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.Mismatch, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkPaidAndNotified_WhenApprovedAndIntegrityMatches()
    {
        // Arrange
        var paymentId = "12345";
        var now = DateTimeOffset.UtcNow;

        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(12345L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                12345L, "approved", 1500m, "ARS",
                _registrationId.ToString(), now));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegistrationWithTournament());

        _emailServiceMock
            .Setup(e => e.SendAccessEmailAsync(
                "juan@example.com", "Juan Perez", "Copa America 2026",
                It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSendResult(true));

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.Processed, result.Outcome);

        _paymentRepoMock.Verify(
            p => p.AddAsync(It.Is<RegistrationPayment>(rp =>
                rp.PaymentId == "12345" && rp.Status == "approved" && rp.Amount == 1500m),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _idempotencyRepoMock.Verify(
            r => r.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkPendingNotification_WhenEmailFails()
    {
        // Arrange
        var paymentId = "12345";
        var now = DateTimeOffset.UtcNow;

        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(12345L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                12345L, "approved", 1500m, "ARS",
                _registrationId.ToString(), now));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegistrationWithTournament());

        _emailServiceMock
            .Setup(e => e.SendAccessEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSendResult(false, "Resend API timeout"));

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.NotificationFailed, result.Outcome);

        _paymentRepoMock.Verify(
            p => p.AddAsync(It.IsAny<RegistrationPayment>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnLateApproval_WhenTournamentClosedRecently()
    {
        // Arrange
        var paymentId = "54321";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(54321L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                54321L, "approved", 1500m, "ARS",
                _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Tournament closed 12 hours ago (< 24h).
        var closedAt = DateTimeOffset.UtcNow.AddHours(-12);
        var closedTournament = new Tournament("Closed Cup", 1500m, "ARS");
        closedTournament.Deactivate(closedAt);

        var registration = CreateRegistrationForTournament(closedTournament);

        // Backdate registration creation to 13 hours ago (before tournament closed)
        var createdAtProp = typeof(Registration).GetProperty(nameof(Registration.CreatedAt))!;
        createdAtProp.SetValue(registration, closedAt.AddHours(-1));

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.LateApproval, result.Outcome);

        _anomalyRepoMock.Verify(
            a => a.AddAsync(It.Is<RegistrationAnomaly>(an => an.Type == "LateApproval"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnStalePayment_WhenTournamentClosedLongAgo()
    {
        // Arrange
        var paymentId = "54321";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(54321L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                54321L, "approved", 1500m, "ARS",
                _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Tournament closed 60 days ago (> 30d).
        var closedAt = DateTimeOffset.UtcNow.AddDays(-60);
        var oldTournament = new Tournament("Old Cup", 1500m, "ARS");
        oldTournament.Deactivate(closedAt);

        var registration = CreateRegistrationForTournament(oldTournament);

        // Backdate registration creation to 61 days ago
        var createdAtProp = typeof(Registration).GetProperty(nameof(Registration.CreatedAt))!;
        createdAtProp.SetValue(registration, closedAt.AddDays(-1));

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.StalePayment, result.Outcome);

        _anomalyRepoMock.Verify(
            a => a.AddAsync(It.Is<RegistrationAnomaly>(an => an.Type == "StalePayment"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnStalePayment_WhenRegistrationCreatedAfterTournamentClosed()
    {
        // Arrange: defense-in-depth — registration created after tournament was already closed.
        var paymentId = "99998";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(99998L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                99998L, "approved", 1500m, "ARS",
                _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "approved", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Tournament closed 10 days ago, registration created 5 days ago (after close)
        var closedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var closedTournament = new Tournament("Expired Cup", 1500m, "ARS");
        closedTournament.Deactivate(closedAt);

        var registration = CreateRegistrationForTournament(closedTournament);

        // Backdate registration to 5 days ago (AFTER tournament closed)
        var createdAtProp = typeof(Registration).GetProperty(nameof(Registration.CreatedAt))!;
        createdAtProp.SetValue(registration, DateTimeOffset.UtcNow.AddDays(-5));

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.StalePayment, result.Outcome);

        _anomalyRepoMock.Verify(
            a => a.AddAsync(It.Is<RegistrationAnomaly>(an => an.Type == "StaleClosedTournamentPayment"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRejected_WhenMpPaymentIsRejected()
    {
        // Arrange
        var paymentId = "11111";
        _mpServiceMock
            .Setup(m => m.GetPaymentAsync(11111L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPayment(
                11111L, "rejected", 1500m, "ARS",
                _registrationId.ToString(), DateTimeOffset.UtcNow));

        _idempotencyRepoMock
            .Setup(r => r.IsDuplicateAsync(paymentId, "rejected", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _registrationRepoMock
            .Setup(r => r.GetByExternalReferenceAsync(_registrationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRegistrationWithTournament());

        // Act
        var result = await _sut.ExecuteAsync("payment", paymentId);

        // Assert
        Assert.Equal(WebhookOutcome.Rejected, result.Outcome);

        _paymentRepoMock.Verify(
            p => p.AddAsync(It.Is<RegistrationPayment>(rp => rp.Status == "rejected"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _emailServiceMock.Verify(
            e => e.SendAccessEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowArgumentException_WhenPaymentIdIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteAsync("payment", ""));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowArgumentException_WhenPaymentIdIsNotNumeric()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteAsync("payment", "not-a-number"));
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private Registration CreateRegistrationWithTournament()
    {
        var reg = new Registration("Juan Perez", "juan@example.com", _tournamentId);

        var tournamentProp = typeof(Registration).GetProperty(nameof(Registration.Tournament))!;
        tournamentProp.SetValue(reg, _tournament);

        var idProp = typeof(Registration).GetProperty(nameof(Registration.Id))!;
        idProp.SetValue(reg, _registrationId);

        return reg;
    }

    private Registration CreateRegistrationForTournament(Tournament tournament)
    {
        var reg = new Registration("Juan Perez", "juan@example.com", tournament.Id);

        var tournamentProp = typeof(Registration).GetProperty(nameof(Registration.Tournament))!;
        tournamentProp.SetValue(reg, tournament);

        var idProp = typeof(Registration).GetProperty(nameof(Registration.Id))!;
        idProp.SetValue(reg, _registrationId);

        var tournamentIdProp = typeof(Registration).GetProperty(nameof(Registration.TournamentId))!;
        tournamentIdProp.SetValue(reg, tournament.Id);

        return reg;
    }
}
