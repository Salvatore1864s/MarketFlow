using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orders.Api.Data;
using Orders.Api.Messaging;
using Orders.Api.Services;

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

builder.Services.AddDbContext<OrdersDbContext>(options =>
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

var catalogBaseUrl = builder.Configuration["Services:Catalog:BaseUrl"]
    ?? throw new InvalidOperationException("Catalog BaseUrl not found.");

builder.Services.AddHttpClient<CatalogClient>(client =>
{
    client.BaseAddress = new Uri(catalogBaseUrl);
});

// RabbitMQ connection
builder.Services.AddSingleton<RabbitMqConnection>(_ =>
    RabbitMqConnection.CreateAsync(builder.Configuration)
        .GetAwaiter()
        .GetResult());

// Message publishers
builder.Services.AddSingleton<InventoryCommandPublisher>();
builder.Services.AddSingleton<PaymentCommandPublisher>();
builder.Services.AddSingleton<InventoryReservationCommandPublisher>();

// Message consumers
builder.Services.AddHostedService<InventoryReservationResultConsumer>();
builder.Services.AddHostedService<PaymentResultConsumer>();
builder.Services.AddHostedService<InventoryReservationCommandResultConsumer>();

builder.Services.AddAuthorization();

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