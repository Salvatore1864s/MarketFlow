namespace Identity.Api.DTOs;

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string Email,
    string Role
);