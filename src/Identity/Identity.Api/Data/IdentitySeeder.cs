
using Identity.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Data;

public static class IdentitySeeder
{
    private static readonly string[] Roles =
    [
        "Buyer",
        "Seller",
        "Admin"
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        using var scope = services.CreateScope();

        var serviceProvider = scope.ServiceProvider;

        var roleManager = serviceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = serviceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        // Создаём роли, если их ещё нет.
        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var roleResult = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    roleResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"Failed to create role {role}: {errors}");
            }
        }

        // Получаем настройки администратора.
        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Admin credentials are missing from configuration.");
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);

        // Создаём пользователя, если его ещё нет.
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Администратор",
                LastName = "MarketFlow",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(
                admin,
                adminPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    createResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"Failed to create admin: {errors}");
            }
        }

        // Проверяем наличие роли Admin.
        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(
                admin,
                "Admin");

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    roleResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"Failed to assign Admin role: {errors}");
            }
        }
    }
}
