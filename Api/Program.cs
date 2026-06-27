using Api.Application.Abstractions.Payments;
using Api.Application.Abstractions.Persistence;
using Api.Application.UseCases.Registrations;
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

// Payments
builder.Services.Configure<MercadoPagoOptions>(
    builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();

// Use Cases
builder.Services.AddScoped<CreateRegistrationUseCase>();

var app = builder.Build();

// ---- Middleware ----

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
