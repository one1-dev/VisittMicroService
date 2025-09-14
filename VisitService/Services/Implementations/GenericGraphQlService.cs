using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using MPACKAGE.LibDomain.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VisitService.Helper;
using VisitService.Repos;


namespace VisitService.Services.Implementations;
public class GenericGraphQlService<T> where T : class
{
    private readonly GraphQlClient _client;
    private readonly ILogger<GenericGraphQlService<T>> _logger;
    private readonly string _cachedFields;

    public GenericGraphQlService(
        GraphQlClient client,
        ILogger<GenericGraphQlService<T>> logger,
        int maxDepth = 2, 
        List<LimitSkipObject>? limitSkipObjects = null)
    {
        _client = client;
        _logger = logger;
        var graphQlFieldGenerator1 = new GraphQlFieldGenerator(limitSkipObjects);
        _cachedFields = graphQlFieldGenerator1.GetGraphQlFieldsFromDataMembers<T>(maxDepth);
    }
    
    public async Task<List<T>> ExecuteBulkUpdate(
        string entityName,
        string mutationName,
        string resultFieldName,
        object variables,
        CancellationToken cancellationToken)
    {
        var query = $@"
            mutation {mutationName}(${ToCamelCase(entityName)}Ids: [String], $input: {entityName}UpdateInput) {{
                {mutationName}({ToCamelCase(entityName)}Ids: ${ToCamelCase(entityName)}Ids, input: $input) {{
                    {resultFieldName} {{
                        {_cachedFields}
                    }}
                }}
            }}";
        
        return await _client.ExecuteQueryForListAsync<T>(
            query, 
            $"{mutationName}.{resultFieldName}", 
            variables, 
            cancellationToken
        );
    }

    public async Task<T> Create(
        string entityName,
        string mutationName,
        string resultFieldName,
        object input,
        CancellationToken cancellationToken)
    {
        var mutation = $@"
            mutation {mutationName}($input: {entityName}Input!) {{
                {mutationName}(input: $input) {{
                    {resultFieldName} {{
                        {_cachedFields}
                    }}
                }}
            }}";
        
        var inputJObject = JObject.FromObject(new { input });
    
        if (inputJObject["input"]?["updatedAt"] == null)
        {
            inputJObject["input"]["updatedAt"] = DateTime.UtcNow.ToString("O"); // ISO format
        }
    
        var obInput = inputJObject.ToString();
        _logger.LogInformation($"Creating {entityName} {mutationName}");
        _logger.LogInformation("input {SerializeObject}", obInput);
        var response = await _client.ExecuteQueryWithJsonAsync<dynamic>(
            mutation, 
            obInput, 
            cancellationToken);


        if (response?.Data is not JsonElement jsonElement) return default(T);
        var data = NavigateToPath(jsonElement, $"{mutationName}.{resultFieldName}");
            
        return data.HasValue ? JsonConvert.DeserializeObject<T>(data.Value.GetRawText()) : null;
    }

    public async Task<T> Update(
        string entityName,
        string mutationName,
        string entityId,
        object input,
        CancellationToken cancellationToken)
    {
        var functionName = FixQueryName(mutationName, out var _);

        var mutation = $@"
            mutation {functionName}(${ToCamelCase(entityName)}Ids: [String!], $input: {entityName}UpdateInput!) {{
                {functionName}({ToCamelCase(entityName)}Ids: ${ToCamelCase(entityName)}Ids, input: $input) {{
                    {ToCamelCase(entityName)}s {{
                        {_cachedFields}
                    }}
                }}
            }}";
        
        var fixInput = ConvertEnumsToStrings(input);
    
        var variables = new Dictionary<string, object>
        {
            [$"{ToCamelCase(entityName)}Ids"] = new[] { entityId }, 
            ["input"] = fixInput
        };
        
        var response = await _client.ExecuteQueryAsync<dynamic>(
            mutation, 
            variables, 
            cancellationToken);
        
        if (response?.Data is not JsonElement jsonElement) return default(T);
        var data = NavigateToPath(jsonElement, $"{functionName}.{ToCamelCase(entityName)}s");
        return data.HasValue ? JsonConvert.DeserializeObject<T>(data.Value.EnumerateArray().First().GetRawText()) : null;
    }

    private object ConvertEnumsToStrings(object input)
    {
        if (input == null) return null;

        var inputType = input.GetType();
        var convertedDict = new Dictionary<string, object>();

        foreach (var property in inputType.GetProperties())
        {
            var value = property.GetValue(input);
            
            if (value == null)
            {
                convertedDict[ToCamelCase(property.Name)] = null;
                continue;
            }

            var propertyType = property.PropertyType;
            
            if (propertyType.IsEnum)
            {
                convertedDict[ToCamelCase(property.Name)] = ConvertEnumToString((Enum)value);
            }
            else if (Nullable.GetUnderlyingType(propertyType)?.IsEnum == true)
            {
                convertedDict[ToCamelCase(property.Name)] = ConvertEnumToString((Enum)value);
            }
            else
            {
                convertedDict[ToCamelCase(property.Name)] = value;
            }
        }

        return convertedDict;
    }

    private string ConvertEnumToString(Enum enumValue)
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        var attribute = field?.GetCustomAttribute<EnumMemberAttribute>();
        
        if (attribute?.Value != null)
        {
            return attribute.Value;
        }
        
        return enumValue.ToString().ToLowerInvariant();
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;
        
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
    
    public async Task<T> GetById(
        string queryName,
        string idParameterName,
        string entityId,
        CancellationToken cancellationToken)
    {
        var query = $@"
        query {queryName}(${idParameterName}: String!) {{
            {queryName}({idParameterName}: ${idParameterName}) {{
                {_cachedFields}
            }}
        }}";

        var variables = $@"{{
        ""{idParameterName}"": ""{entityId}""
        }}";

        _logger.LogInformation("Direct query: {Query}", query);
        _logger.LogInformation("Variables: {Variables}", variables);

        var response = await _client.ExecuteQueryWithJsonAsync<dynamic>(
            query, 
            variables, 
            cancellationToken);

        if (response?.Data is not JsonElement jsonElement) return default(T);
    
        var resultElement = NavigateToPath(jsonElement, queryName);

        if (!resultElement.HasValue || resultElement.Value.ValueKind == JsonValueKind.Null)
            throw new KeyNotFoundException($"{queryName} with id {entityId} not found");
        var result = JsonConvert.DeserializeObject<T>(resultElement.Value.GetRawText());
        return result;

    }

    public async Task<TPaginated?> GetPaginated<TPaginated>(
        string queryName,
        object? filters,
        CancellationToken cancellationToken,
        int skip = 0,
        int limit = 20) where TPaginated : class
    {
        var functionName = FixQueryName(queryName, out var singularName);
        var filterName = char.ToUpper(singularName[0]) + singularName[1..] + "Filter";

        var hasFilters = filters != null && !IsEmptyFilter(filters);
        var filtersParam = hasFilters ? $", $filters: [{filterName}]" : "";
        var filtersArg = hasFilters ? ", filters: $filters" : "";

        var query = $@"
            query {functionName}($skip: Int, $limit: Int{filtersParam}) {{
                {functionName}(skip: $skip, limit: $limit{filtersArg}) {{
                    items {{
                        {_cachedFields}
                    }}
                    pageInfo {{
                        hasNext
                        totalCount
                        skip
                        limit
                    }}
                }}
            }}";

        var variables = new Dictionary<string, object>
        {
            ["skip"] = skip,
            ["limit"] = limit
        };

        if (hasFilters)
        {
            var cleanedFilters = filters;
    
            if (filters is IList list)
            {
                cleanedFilters = list.Cast<object>().Select(CleanNullValues).ToList();
            }
            else
            {
                cleanedFilters = new[] { CleanNullValues(filters) };
            }
    
            variables["filters"] = cleanedFilters;
        }
        
        var response = await _client.ExecuteQueryAsync<dynamic>(
            query, 
            variables, 
            cancellationToken);
        
        if (response?.Data is not JsonElement jsonElement) return null;
        var data = NavigateToPath(jsonElement, functionName);

        if (!data.HasValue) return null;
        TPaginated result = JsonConvert.DeserializeObject<TPaginated>(data.Value.GetRawText());
        return result;

    }

    private static string FixQueryName(string queryName, out string singularName)
    {
        var functionName = queryName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) 
            ? char.ToLower(queryName[3]) + queryName.Substring(4)
            : queryName;
        
        if (!functionName.EndsWith("s"))
        {
            functionName += "s";
        }

        singularName = functionName.EndsWith("s") ? functionName.Substring(0, functionName.Length - 1) : functionName;
        return functionName;
    }

    private bool IsEmptyFilter(object filter)
    {
        if (filter == null) return true;
    
        if (filter is JsonElement jsonElement)
        {
            return jsonElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                   (jsonElement.ValueKind == JsonValueKind.Object && !jsonElement.EnumerateObject().Any());
        }

        return filter.GetType()
            .GetProperties()
            .All(prop => IsEmptyValue(prop.GetValue(filter)));
    }

    private static bool IsEmptyValue(object value) => value switch
    {
        null => true,
        string str => string.IsNullOrEmpty(str),
        Array arr => arr.Length == 0,
        _ => false
    };

    private JsonElement? NavigateToPath(JsonElement element, string path)
    {
        _logger.LogInformation($"Navigating path: {path} in JSON");
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = element;
        
        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
            {
                current = property;
            }
            else
            {
                _logger.LogWarning($"Path segment '{part}' not found in JSON while navigating '{path}'");
                return null;
            }
        }
        
        _logger.LogInformation($"Successfully navigated to path: {path}");
        return current;
    }
    
    private object CleanNullValues(object input)
    {
        if (input == null) return null;

        var inputType = input.GetType();
        var cleanedDict = new Dictionary<string, object>();

        foreach (var property in inputType.GetProperties())
        {
            var value = property.GetValue(input);
        
            if (value == null) continue;
        
            if (property.PropertyType.IsEnum && Convert.ToInt32(value) == 0)
                continue;
            
            var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
            if (underlyingType?.IsEnum == true && Convert.ToInt32(value) == 0)
                continue;

            var cleanedValue = value;
            if (property.PropertyType.IsEnum)
            {
                cleanedValue = ConvertEnumToString((Enum)value);
            }
            else if (underlyingType?.IsEnum == true)
            {
                cleanedValue = ConvertEnumToString((Enum)value);
            }
            else if (GraphQlFieldGenerator.IsComplexType(property.PropertyType))
            {
                cleanedValue = CleanNullValues(value);
                if (cleanedValue is Dictionary<string, object> dict && !dict.Any())
                    continue;
            }
            else if (value is IList list && list.Count > 0)
            {
                var cleanedList = new List<object>();
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        var cleanedItem = GraphQlFieldGenerator.IsComplexType(item.GetType()) ? CleanNullValues(item) : item;
                        if (cleanedItem != null)
                            cleanedList.Add(cleanedItem);
                    }
                }
                if (cleanedList.Any())
                    cleanedValue = cleanedList;
                else
                    continue;
            }

            cleanedDict[GetJsonPropertyName(property)] = cleanedValue;
        }

        return cleanedDict;
    }

    private string GetJsonPropertyName(PropertyInfo property)
    {
        var dataMember = property.GetCustomAttribute<DataMemberAttribute>();
        return dataMember?.Name ?? ToCamelCase(property.Name);
    }
    
}