using System.Text;
using Inventory.Api.Clients;
using Inventory.Api.Data;
using Inventory.Api.Messaging;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret not found.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer not found.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience not found.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var catalogBaseUrl = builder.Configuration[
    "Services:Catalog:BaseUrl"] ?? "http://localhost:5022/";

builder.Services.AddHttpClient<CatalogClient>(client =>
{
    client.BaseAddress = new Uri(catalogBaseUrl);
});

// RabbitMQ
builder.Services.AddSingleton<RabbitMqConnection>(_ =>
    RabbitMqConnection.CreateAsync(builder.Configuration)
        .GetAwaiter()
        .GetResult());

// Services and publishers
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReservationResultPublisher>();
builder.Services.AddSingleton<ReservationCommandResultPublisher>();;

// Consumers
builder.Services.AddHostedService<InventoryMessageConsumer>();
builder.Services.AddHostedService<ReservationCommandConsumer>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();