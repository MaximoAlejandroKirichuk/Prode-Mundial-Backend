using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Application.UseCases.Registrations;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Domain.Exceptions;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests.UseCases;

public sealed class CreateRegistrationUseCaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RegistrationRepository _registrationRepo;
    private readonly TournamentRepository _tournamentRepo;
    private readonly Mock<IMercadoPagoService> _mercadoPagoMock;
    private readonly CreateRegistrationUseCase _sut;

    private readonly Guid _tournamentId;
    private readonly Guid _inactiveTournamentId;
    private readonly Guid _nonexistentTournamentId = Guid.NewGuid();

    public CreateRegistrationUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _registrationRepo = new RegistrationRepository(_context);
        _tournamentRepo = new TournamentRepository(_context);
        _mercadoPagoMock = new Mock<IMercadoPagoService>();

        // Seed active tournament
        var activeTournament = new Tournament("Copa America 2026", 1500m, "ARS");
        _context.Tournaments.Add(activeTournament);
        _tournamentId = activeTournament.Id;

        // Seed inactive tournament
        var inactiveTournament = new Tournament("Closed Cup", 1000m, "ARS");
        inactiveTournament.Deactivate(DateTimeOffset.UtcNow.AddDays(-1));
        _context.Tournaments.Add(inactiveTournament);
        _inactiveTournamentId = inactiveTournament.Id;

        _context.SaveChanges();

        _sut = new CreateRegistrationUseCase(
            _registrationRepo,
            _tournamentRepo,
            _mercadoPagoMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---- Happy path ----

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewRegistration_WhenNoRecentPendingExists()
    {
        // Arrange
        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                "Juan Perez", "juan@example.com", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePaymentPreferenceResult("pref-123", "https://mp.com/pay"));

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId);

        // Assert
        Assert.False(result.IsExisting);
        Assert.NotEqual(Guid.Empty, result.RegistrationId);
        Assert.Equal("https://mp.com/pay", result.PaymentUrl);

        var saved = await _context.Registrations.FirstOrDefaultAsync(r => r.Id == result.RegistrationId);
        Assert.NotNull(saved);
        Assert.Equal("Juan Perez", saved!.Name);
        Assert.Equal("juan@example.com", saved.Email);
        Assert.Equal("pref-123", saved.PaymentPreferenceId);
        Assert.Equal("https://mp.com/pay", saved.PaymentUrl);

        _mercadoPagoMock.Verify(
            m => m.CreatePreferenceAsync("Juan Perez", "juan@example.com", 1500m, "ARS", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnExistingPaymentUrl_WhenRecentPendingExists()
    {
        // Arrange: seed an existing pending registration with a payment URL
        var existing = new Registration("Juan Perez", "juan@example.com", _tournamentId);
        existing.SetPaymentPreference("pref-existing", "https://mp.com/existing");
        _context.Registrations.Add(existing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId);

        // Assert
        Assert.True(result.IsExisting);
        Assert.Equal(existing.Id, result.RegistrationId);
        Assert.Equal("https://mp.com/existing", result.PaymentUrl);

        _mercadoPagoMock.Verify(
            m => m.CreatePreferenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.Equal(1, await _context.Registrations.CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotPersistRegistration_WhenPaymentPreferenceFails()
    {
        // Arrange
        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mercado Pago API error"));

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId));

        Assert.Empty(await _context.Registrations.ToListAsync());
    }

    // ---- Tournament validation ----

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenTournamentNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<TournamentNotFoundException>(
            () => _sut.ExecuteAsync("Juan", "juan@example.com", _nonexistentTournamentId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenTournamentNotActive()
    {
        // Act & Assert
        await Assert.ThrowsAsync<TournamentNotActiveException>(
            () => _sut.ExecuteAsync("Juan", "juan@example.com", _inactiveTournamentId));
    }

    // ---- Duplicate rules ----

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenPaidRegistrationExists()
    {
        // Arrange: seed a paid registration
        var paid = new Registration("Juan Perez", "juan@example.com", _tournamentId);
        paid.MarkAsPaid(DateTimeOffset.UtcNow);
        _context.Registrations.Add(paid);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<DuplicatePaidRegistrationException>(
            () => _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenNotifiedRegistrationExists()
    {
        // Arrange: seed a notified registration (which implies paid)
        var notified = new Registration("Juan Perez", "juan@example.com", _tournamentId);
        notified.MarkAsPaid(DateTimeOffset.UtcNow);
        notified.MarkAsNotified(DateTimeOffset.UtcNow);
        _context.Registrations.Add(notified);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<DuplicatePaidRegistrationException>(
            () => _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewRegistration_WhenPreviousWasRejected()
    {
        // Arrange: seed a rejected registration
        var rejected = new Registration("Juan Perez", "juan@example.com", _tournamentId);
        rejected.MarkAsRejected(DateTimeOffset.UtcNow, "fraud");
        _context.Registrations.Add(rejected);
        await _context.SaveChangesAsync();

        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePaymentPreferenceResult("pref-retry", "https://mp.com/retry"));

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId);

        // Assert
        Assert.False(result.IsExisting);
        Assert.NotEqual(rejected.Id, result.RegistrationId); // new row, not reuse
        Assert.Equal("https://mp.com/retry", result.PaymentUrl);

        // Verify 2 registrations now exist (old + new)
        Assert.Equal(2, await _context.Registrations.CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewRegistration_WhenPreviousWasExpired()
    {
        // Arrange: seed an expired registration
        var expired = new Registration("Juan Perez", "juan@example.com", _tournamentId);
        expired.MarkAsExpired();
        _context.Registrations.Add(expired);
        await _context.SaveChangesAsync();

        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePaymentPreferenceResult("pref-fresh", "https://mp.com/fresh"));

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId);

        // Assert
        Assert.False(result.IsExisting);
        Assert.NotEqual(expired.Id, result.RegistrationId);
        Assert.Equal(2, await _context.Registrations.CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewRegistration_WhenExistingPendingIsOlderThanWindow()
    {
        // Arrange: seed an old pending registration (created 10 minutes ago via EF)
        var oldPending = new Registration("Juan Perez", "juan@example.com", _tournamentId);
        oldPending.SetPaymentPreference("pref-old", "https://mp.com/old");

        // Manually set CreatedAt to 10 min ago via reflection (InMemory doesn't respect constructor DateTimeOffset.UtcNow for this)
        var createdAtField = typeof(Registration).GetProperty(nameof(Registration.CreatedAt))!;
        createdAtField.GetSetMethod(true)!.Invoke(oldPending, [DateTimeOffset.UtcNow.AddMinutes(-10)]);

        _context.Registrations.Add(oldPending);
        await _context.SaveChangesAsync();

        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePaymentPreferenceResult("pref-new", "https://mp.com/new"));

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com", _tournamentId);

        // Assert
        Assert.False(result.IsExisting);
        Assert.NotEqual(oldPending.Id, result.RegistrationId);
        Assert.Equal(2, await _context.Registrations.CountAsync());
    }

    // ---- Input validation ----

    [Theory]
    [InlineData("", "juan@example.com")]
    [InlineData("  ", "juan@example.com")]
    [InlineData("Juan", "")]
    [InlineData("Juan", "  ")]
    public async Task ExecuteAsync_ShouldThrowArgumentException_WhenInputsAreInvalid(string name, string email)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteAsync(name, email, _tournamentId));
    }
}
