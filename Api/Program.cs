using Api.Application.Abstractions.Notifications;
using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Application.UseCases.Registrations;
using Api.Application.UseCases.Webhooks;
using Api.Infrastructure.Notifications;
using Api.Infrastructure.Payments;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Persistence
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=ProdeMundial;Username=postgres;Password=postgres;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IRegistrationPaymentRepository, RegistrationPaymentRepository>();
builder.Services.AddScoped<IWebhookIdempotencyRepository, WebhookIdempotencyRepository>();
builder.Services.AddScoped<IRegistrationAnomalyRepository, RegistrationAnomalyRepository>();

// Payments
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();

// Notifications
builder.Services.Configure<ResendOptions>(
    builder.Configuration.GetSection(ResendOptions.SectionName));
builder.Services.AddScoped<IEmailService, ResendEmailService>();

// Use Cases
builder.Services.AddScoped<CreateRegistrationUseCase>();
builder.Services.AddScoped<ProcessMercadoPagoWebhookUseCase>();

var app = builder.Build();

// ---- Database migration (bounded retry for Render cold starts) ----
{
    const int maxAttempts = 5;
    Exception? lastException = null;
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            logger.LogInformation("Database migration succeeded on attempt {Attempt}", attempt);
            lastException = null;
            break;
        }
        catch (Exception ex)
        {
            lastException = ex;
            var delaySeconds = (int)Math.Pow(2, attempt);
            logger.LogWarning(ex, "Migration attempt {Attempt}/{Max} failed; retrying in {Delay}s", attempt, maxAttempts, delaySeconds);

            if (attempt < maxAttempts)
                await Task.Delay(delaySeconds * 1000);
        }
    }

    if (lastException is not null)
    {
        logger.LogError(lastException, "All {Max} migration attempts failed; aborting startup", maxAttempts);
        throw lastException;
    }
}

// ---- Middleware ----

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();

app.Run();
