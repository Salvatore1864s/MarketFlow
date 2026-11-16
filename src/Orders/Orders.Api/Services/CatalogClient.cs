using System.Net;
using System.Net.Http.Json;
using Orders.Api.DTOs;

namespace Orders.Api.Services;

public class CatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogProductResponse?> GetProductByIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/products/{productId}",
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<CatalogProductResponse>(
                cancellationToken: cancellationToken
            );
    }
}