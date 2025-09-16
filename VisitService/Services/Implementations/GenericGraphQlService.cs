using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VisitService.CModels;
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
            mutation {mutationName}(${ToCamelCase(entityName)}Ids: [String!], $input: {entityName}UpdateInput) {{
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
        var obInput = inputJObject.ToString();
        var response = await _client.ExecuteQueryWithJsonAsync<dynamic>(mutation,obInput, cancellationToken);
        return DeserializeResponse(mutationName, resultFieldName, response);
    }

    public async Task<T> Update(
        string entityName,
        string idName,
        string mutationName,
        string entityId,
        object input,
        CancellationToken cancellationToken)
    {
        var camelCase = ToCamelCase(entityName);
        var upperCase = ToUpperCase(entityName);
        
        var mutation = $@"
            mutation {mutationName}(${idName}: String!, $input: {upperCase}Input!) {{
                {mutationName}({idName}: ${idName}, input: $input) {{
                    {camelCase} {{
                        {_cachedFields}
                    }}
                }}
            }}";
        
        var json = JsonConvert.SerializeObject(input, new JsonSerializerSettings 
        { 
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(), 
            NullValueHandling = NullValueHandling.Ignore 
        });
        
        _logger.LogInformation("Serialized JSON: {Json}", json);
    
        var variablesJson = $@"{{
            ""{idName}"": ""{entityId}"",
            ""input"": {json}
        }}";
    
        var response = await _client.ExecuteQueryWithJsonAsync<dynamic>(mutation, variablesJson, cancellationToken);
        return DeserializeResponse(mutationName, camelCase, response);
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

        var response = await _client.ExecuteQueryWithJsonAsync<dynamic>( query,  variables, cancellationToken);
        return DeserializeResponse(queryName,"", response);
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
        
        var response = await _client.ExecuteQueryAsync<dynamic>(query, variables,  cancellationToken);
        
        if (response?.Data is not JsonElement jsonElement) return null;
        var data = NavigateToPath(jsonElement, functionName);

        if (!data.HasValue) return null;
        var result = JsonConvert.DeserializeObject<TPaginated>(data.Value.GetRawText());
        return result;
        
    }
    
    public async Task<T> Archive(string entityType, string objectId,bool archive ,CancellationToken cancellationToken)
    {
        var supportedArchiveEntities = new[] { "Contact", "Tenant" };
    
        if (!supportedArchiveEntities.Contains(entityType))
        {
            throw new NotSupportedException($"{entityType} doesn't support archive operation");
        }
        var entityLower = ToCamelCase(entityType);
        var mutation = $@"
            mutation archive{entityType}(${entityLower}Id: String!, $archive: Boolean!) {{
               archive{entityType}({entityLower}Id: ${entityLower}Id, archive: $archive) {{
                    {entityLower}{{
                        {_cachedFields}
                    }}
                }}
            }}";
        
    
        var variables = new Dictionary<string, object>
        {
            ["archive"] = archive,
            [$"{entityLower}Id"] =  objectId
        };
        
        var response = await _client.ExecuteQueryAsync<dynamic>( mutation,  variables,  cancellationToken);
        return DeserializeResponse(entityType, entityLower, response);

    }
    private T DeserializeResponse(string mutationName, string resultFieldName, GraphQLResponse<dynamic> response)
    {
        if (response?.Errors?.Any() == true)
        {
            var errorMessage = string.Join("; ", response.Errors.Select(e => e.Message));
            if(response.Errors.Any(e=>e.Message.Contains("not found",StringComparison.OrdinalIgnoreCase))) 
                throw new KeyNotFoundException(errorMessage);
            if (response.Errors.Any(e => e.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(errorMessage);
            if (response != null) throw new ArgumentException(errorMessage);
        }
        
        if (response?.Data is not JsonElement jsonElement) return default(T);
        var data = NavigateToPath(jsonElement, $"{mutationName}.{resultFieldName}");
        _logger.LogInformation(data.ToString());
        return data.HasValue ? JsonConvert.DeserializeObject<T>(data.Value.GetRawText()) : null;
    }
    
    private object ConvertEnumsToStrings(object input)
    {
        if (input == null) return null;

        var inputType = input.GetType();
        var convertedDict = new Dictionary<string, object>();

        foreach (var property in inputType.GetProperties())
        {
            var value = property.GetValue(input);
            
            if (value == null) continue;
            // {
            //     convertedDict[ToCamelCase(property.Name)] = null;
            //     continue;
            // }
            var propertyName = GetJsonPropertyName(property);
            var propertyType = property.PropertyType;
            
            if (propertyType.IsEnum || Nullable.GetUnderlyingType(propertyType)?.IsEnum == true)
            {
                convertedDict[propertyName] = ConvertEnumToString((Enum)value);
            }
            else
            {
                convertedDict[propertyName] = value;
            }
        }

        return convertedDict;
    }

    private string ConvertEnumToString(Enum enumValue)
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        var attribute = field?.GetCustomAttribute<EnumMemberAttribute>();
        
        return attribute?.Value ?? enumValue.ToString().ToLowerInvariant();
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0])) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string ToUpperCase(string name)
    {
        if (string.IsNullOrEmpty(name)|| char.IsUpper(name[0])) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
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

        return filter.GetType().GetProperties().All(prop => IsEmptyValue(prop.GetValue(filter)));
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
        _logger.LogInformation("Navigating path: {Path} in JSON", path);
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
                _logger.LogWarning("Path segment '{Part}' not found in JSON while navigating '{Path}'", part, path);
                return null;
            }
        }
        
        _logger.LogInformation("Successfully navigated to path: {Path}", path);
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
            if (property.PropertyType.IsEnum || underlyingType?.IsEnum == true)
            {
                cleanedValue = ConvertEnumToString((Enum)value);
            }
            else if (GraphQlFieldGenerator.IsComplexType(property.PropertyType))
            {
                cleanedValue = CleanNullValues(value);
                if (cleanedValue is Dictionary<string, object> dict && !dict.Any())
                    continue;
            }
            else if (value is IList { Count: > 0 } list)
            {
                var cleanedList = new List<object>();
                foreach (var item in list)
                {
                    if (item == null) continue;
                    var cleanedItem = GraphQlFieldGenerator.IsComplexType(item.GetType()) ? CleanNullValues(item) : item;
                    if (cleanedItem != null)
                        cleanedList.Add(cleanedItem);
                }
                if (cleanedList.Count != 0)
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