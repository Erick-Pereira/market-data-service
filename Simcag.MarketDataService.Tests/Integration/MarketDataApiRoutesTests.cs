using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Simcag.Shared.Security;

namespace Simcag.MarketDataService.Tests.Integration;

public sealed class MarketDataApiRoutesTests : IClassFixture<MarketDataApiTestFactory>
{
    private readonly MarketDataApiTestFactory _factory;

    public MarketDataApiRoutesTests(MarketDataApiTestFactory factory) => _factory = factory;

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.ApplyGatewayAuthHeaders();
        return client;
    }

    [Fact]
    public async Task Get_Benchmarks_WithSeed_Returns_200_And_Envelope()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            "/api/market-data/benchmarks?category=Limpeza%20Predial&region=BR-SP");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.GetProperty("sampleSize").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Get_Benchmarks_MissingQuery_Returns_400()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/market-data/benchmarks");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Price_WithDeclaredReference_Returns_200()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            "/api/market-data/price?productName=Detergente%205L&declaredReferenceBrl=42.50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();
        body!.Success.Should().BeTrue();
        body.Data.GetProperty("price").GetDecimal().Should().Be(42.50m);
    }

    [Fact]
    public async Task Get_Price_WithoutProductOrCategory_Returns_400()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/market-data/price");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_History_WithSeed_Returns_200_And_Points()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            "/api/market-data/history?productName=Detergente%205L&days=30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<JsonElement>>();
        body!.Success.Should().BeTrue();
        body.Data.GetArrayLength().Should().BeGreaterThan(0);
    }

    private sealed record ApiEnvelope<T>(bool Success, T Data, string? Error);
}
