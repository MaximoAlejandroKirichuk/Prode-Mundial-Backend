using Api.Application.Abstractions.Payments;
using Api.Application.UseCases.Registrations;
using Api.Domain.Entities;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests.UseCases;

public sealed class CreateRegistrationUseCaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly RegistrationRepository _repository;
    private readonly Mock<IMercadoPagoService> _mercadoPagoMock;
    private readonly CreateRegistrationUseCase _sut;

    public CreateRegistrationUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new RegistrationRepository(_context);
        _mercadoPagoMock = new Mock<IMercadoPagoService>();
        _sut = new CreateRegistrationUseCase(_repository, _mercadoPagoMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewRegistration_WhenNoRecentPendingExists()
    {
        // Arrange
        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                "Juan Perez", "juan@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePaymentPreferenceResult("pref-123", "https://mp.com/pay"));

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com");

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
            m => m.CreatePreferenceAsync("Juan Perez", "juan@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnExistingPaymentUrl_WhenRecentPendingExists()
    {
        // Arrange: seed an existing pending registration with a payment URL
        var existing = new Registration("Juan Perez", "juan@example.com");
        existing.SetPaymentPreference("pref-existing", "https://mp.com/existing");
        _context.Registrations.Add(existing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ExecuteAsync("Juan Perez", "juan@example.com");

        // Assert
        Assert.True(result.IsExisting);
        Assert.Equal(existing.Id, result.RegistrationId);
        Assert.Equal("https://mp.com/existing", result.PaymentUrl);

        // Verify Mercado Pago was NOT called again
        _mercadoPagoMock.Verify(
            m => m.CreatePreferenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify no duplicate was created
        Assert.Equal(1, await _context.Registrations.CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotPersistRegistration_WhenPaymentPreferenceFails()
    {
        // Arrange
        _mercadoPagoMock
            .Setup(m => m.CreatePreferenceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mercado Pago API error"));

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync("Juan Perez", "juan@example.com"));

        // Verify nothing was persisted
        Assert.Empty(await _context.Registrations.ToListAsync());
    }

    [Theory]
    [InlineData("", "juan@example.com")]
    [InlineData("  ", "juan@example.com")]
    [InlineData("Juan", "")]
    [InlineData("Juan", "  ")]
    public async Task ExecuteAsync_ShouldThrowArgumentException_WhenInputsAreInvalid(string name, string email)
    {
        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExecuteAsync(name, email));
    }
}
