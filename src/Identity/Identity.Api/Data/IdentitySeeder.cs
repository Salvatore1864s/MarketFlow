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

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(role));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        "; ", result.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        $"Failed to create role {role}: {errors}");
                }
            }
        }
    }
}