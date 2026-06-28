using MercadoPago.Client.Preference;
using MercadoPago.Resource.Preference;

namespace Api.Infrastructure.Payments;

/// <summary>
/// Internal abstraction over the Mercado Pago <see cref="PreferenceClient"/>
/// to enable unit testing of <see cref="MercadoPagoApi"/>.
/// </summary>
internal interface IPreferenceClient
{
    Task<Preference> CreateAsync(PreferenceRequest request, CancellationToken cancellationToken = default);
}
