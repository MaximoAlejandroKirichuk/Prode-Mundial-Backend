using MercadoPago.Client.Preference;
using MercadoPago.Resource.Preference;

namespace Api.Infrastructure.Payments;

/// <summary>
/// Production implementation that delegates to the real Mercado Pago SDK
/// <see cref="PreferenceClient"/>.
/// </summary>
internal sealed class SdkPreferenceClient : IPreferenceClient
{
    private readonly PreferenceClient _client = new();

    public Task<Preference> CreateAsync(PreferenceRequest request, CancellationToken cancellationToken = default)
    {
        return _client.CreateAsync(request, cancellationToken: cancellationToken);
    }
}
