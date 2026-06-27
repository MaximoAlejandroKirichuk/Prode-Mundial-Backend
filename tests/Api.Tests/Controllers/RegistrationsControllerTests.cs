using Api.Application.UseCases.Registrations;
using Api.Domain.Exceptions;
using Api.Presentation.Contracts.Requests;
using Api.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

public sealed class RegistrationsControllerTests
{
    private readonly Mock<CreateRegistrationUseCase> _useCaseMock;
    private readonly RegistrationsController _sut;

    public RegistrationsControllerTests()
    {
        _useCaseMock = new Mock<CreateRegistrationUseCase>(
            null!, null!, null!); // nulls are ok because we never call the real use case

        _sut = new RegistrationsController(_useCaseMock.Object);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenSuccessful()
    {
        // Arrange
        var registrationId = Guid.NewGuid();
        _useCaseMock
            .Setup(u => u.ExecuteAsync("Juan", "juan@test.com", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateRegistrationResult(registrationId, "https://mp.com/pay", false));

        var request = new CreateRegistrationRequest
        {
            Name = "Juan",
            Email = "juan@test.com",
            TournamentId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsAssignableFrom<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenTournamentNotFound()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        _useCaseMock
            .Setup(u => u.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), tournamentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TournamentNotFoundException(tournamentId));

        var request = new CreateRegistrationRequest
        {
            Name = "Juan",
            Email = "juan@test.com",
            TournamentId = tournamentId
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(422, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("not found", problemDetails.Detail?.ToLowerInvariant());
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenTournamentNotActive()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        _useCaseMock
            .Setup(u => u.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), tournamentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TournamentNotActiveException(tournamentId));

        var request = new CreateRegistrationRequest
        {
            Name = "Juan",
            Email = "juan@test.com",
            TournamentId = tournamentId
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(422, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("not active", problemDetails.Detail?.ToLowerInvariant());
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenDuplicatePaidRegistration()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        _useCaseMock
            .Setup(u => u.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), tournamentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicatePaidRegistrationException("juan@test.com", tournamentId));

        var request = new CreateRegistrationRequest
        {
            Name = "Juan",
            Email = "juan@test.com",
            TournamentId = tournamentId
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn503_WhenMercadoPagoNotConfigured()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("Mercado Pago is not configured."));

        var request = new CreateRegistrationRequest
        {
            Name = "Juan",
            Email = "juan@test.com",
            TournamentId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn422_WhenValidationFails()
    {
        // Arrange
        _useCaseMock
            .Setup(u => u.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Name is required."));

        var request = new CreateRegistrationRequest
        {
            Name = "",
            Email = "juan@test.com",
            TournamentId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Create(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(422, objectResult.StatusCode);
    }
}
