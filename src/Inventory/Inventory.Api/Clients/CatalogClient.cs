using System.Net;
using System.Net.Http.Json;
using Inventory.Api.DTOs;

namespace Inventory.Api.Clients;

public class CatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogProductResponse?> GetProductAsync(
        Guid productId)
    {
        var response = await _httpClient.GetAsync(
            $"api/products/{productId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<CatalogProductResponse>();
    }
}