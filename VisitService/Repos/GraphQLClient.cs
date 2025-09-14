using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VisitService.CModels;

namespace VisitService.Repos;

public class GraphQlClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphQlClient> _logger;
    
    public GraphQlClient(
        HttpClient httpClient, 
        IOptions<VisittApiOptions> options,
        ILogger<GraphQlClient> logger)
    {
        _httpClient = httpClient;
        var options1 = options.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(options1.GraphQlEndpoint);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(options1.ApiToken);
    }
    
    public async Task<GraphQLResponse<T>> ExecuteQueryAsync<T>(string query, object? variables = null, CancellationToken cancellationToken = default)
    {
        var request = new GraphQLRequest
        {
            Query = query,
            Variables = variables
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        DocumentVisitQuery(query, variables);
        var response = await SendRequest(cancellationToken,content);
        var graphQlResponse =await ProsesResponse<T>(response,cancellationToken);
        return graphQlResponse;
    }

    public async Task<GraphQLResponse<T>> ExecuteQueryWithJsonAsync<T>(
        string query, 
        string? variablesJson = null, 
        CancellationToken cancellationToken = default)
    {
        var requestJson = string.IsNullOrEmpty(variablesJson) 
            ? $@"{{
            ""query"": {JsonSerializer.Serialize(query)}
        }}": $@"{{
            ""query"": {JsonSerializer.Serialize(query)},
            ""variables"": {variablesJson}
        }}";


        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        DocumentVisitQuery(query, variablesJson);
    
        var response = await SendRequest(cancellationToken, content);
        var graphQlResponse = await ProsesResponse<T>(response,cancellationToken);
        return graphQlResponse;
    }
    
    public async Task<List<T>> ExecuteQueryForListAsync<T>(
        string query, 
        string dataPath, 
        object? variables = null, 
        CancellationToken cancellationToken = default)
    {
        var response = await ExecuteQueryAsync<dynamic>(query, variables, cancellationToken);
        
        DocumentVisitQuery(query, variables);
        if (response?.Data != null)
        {
            var jsonElement = (JsonElement)response.Data;
            var data = NavigateToPath(jsonElement, dataPath);
        
            if (data.HasValue)
            {
                return JsonSerializer.Deserialize<List<T>>(data.Value.GetRawText()) ?? new List<T>();
            }
        }
    
        return [];
    }

    private void DocumentVisitQuery(string query, object? variables)
    {
        var jsonRequest = JsonSerializer.Serialize(query, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("=== Sending GraphQL Request to Visitt ===");
        _logger.LogInformation("Query: {Query}", query);
        _logger.LogInformation("Variables: {Variables}", JsonSerializer.Serialize(variables, new JsonSerializerOptions { WriteIndented = true }));
        _logger.LogInformation("Full Request: {Request}", jsonRequest);
    }

    private async Task<GraphQLResponse<T>> ProsesResponse<T>(HttpResponseMessage response,CancellationToken cancellationToken)
    {
        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var graphQlResponse = JsonSerializer.Deserialize<GraphQLResponse<T>>(jsonResponse);

        if (graphQlResponse != null && graphQlResponse.Errors != null && graphQlResponse.Errors.Any())
        {
            var errors = string.Join(", ", graphQlResponse.Errors.Select(e => e.Message));
            _logger.LogError("GraphQL errors: {Errors}", errors);
        }

        return graphQlResponse!;
    }
    
    private async Task<HttpResponseMessage> SendRequest(CancellationToken cancellationToken, StringContent content)
    {
        var response = await _httpClient.PostAsync("", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed GraphQL request with status {StatusCode}: {ReasonPhrase}", 
                response.StatusCode, response.ReasonPhrase);
            throw new HttpRequestException($"GraphQL request failed with status code {response.StatusCode}");
        }

        return response;
    }
    
    private JsonElement? NavigateToPath(JsonElement element, string path)
    {
        var parts = path.Split('.');
        var current = element;
    
        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
            {
                current = property;
            }
            else
            {
                return null;
            }
        }
    
        return current;
    }
    
}