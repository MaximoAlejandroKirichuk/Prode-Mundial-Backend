namespace Api.Domain.Entities;

public sealed class Tournament
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public bool Active { get; private set; } = true;
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

#pragma warning disable CS8618
    private Tournament() { } // EF Core
#pragma warning restore CS8618

    public Tournament(string name, decimal priceAmount, string currency)
    {
        Id = Guid.NewGuid();
        Name = name;
        PriceAmount = priceAmount;
        Currency = currency;
        Active = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsActive() => Active;

    public void Deactivate(DateTimeOffset closedAt)
    {
        Active = false;
        ClosedAt = closedAt;
    }
}
