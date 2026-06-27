namespace Api.Domain.Entities;

public sealed class RegistrationAnomaly
{
    public Guid Id { get; private set; }
    public Guid? RegistrationId { get; private set; }
    public string Type { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }

#pragma warning disable CS8618
    private RegistrationAnomaly() { } // EF Core
#pragma warning restore CS8618

    public RegistrationAnomaly(Guid? registrationId, string type, string description)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type is required.", nameof(type));

        Id = Guid.NewGuid();
        RegistrationId = registrationId;
        Type = type;
        Description = description;
        DetectedAt = DateTimeOffset.UtcNow;
    }
}
