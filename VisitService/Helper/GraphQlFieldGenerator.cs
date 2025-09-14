using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace VisitService.Helper;

public class LimitSkipObject
{
    public string Name { get; set; }
    public Dictionary<string, int> Properties { get; set; }
}

public class GraphQlFieldGenerator(List<LimitSkipObject>? limitSkipObjects = null)
{
    private readonly Dictionary<Type, string> _fieldCache = new();
    private readonly HashSet<Type> _visitedTypes = new();

    public string GetGraphQlFieldsFromDataMembers<T>(int maxDepth = 2)
    {
        var type = typeof(T);
        
        if (_fieldCache.TryGetValue(type, out var cachedFields))
            return cachedFields;

        _visitedTypes.Clear();
        var fields = GenerateFieldsRecursively(type, 0, maxDepth);
        _fieldCache[type] = fields;
        
        return fields;
    }

    private string GenerateFieldsRecursively(Type type, int currentDepth, int maxDepth)
    {
        if (currentDepth >= maxDepth || !_visitedTypes.Add(type))
            return "";

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();
        var indent = new string(' ', currentDepth * 4);

        foreach (var prop in properties)
        {
            var dataMember = prop.GetCustomAttribute<DataMemberAttribute>();
            if (dataMember == null || string.IsNullOrEmpty(dataMember.Name))
                continue;

            var fieldName = dataMember.Name;

            if (IsSimpleType(prop.PropertyType))
            {
                sb.AppendLine($"{indent}{fieldName}");
            }
            else if (IsListType(prop.PropertyType))
            {
                var elementType = GetListElementType(prop.PropertyType);
                if (elementType != null && !IsSimpleType(elementType))
                {
                    var nestedFields = GenerateFieldsRecursively(elementType, currentDepth + 1, maxDepth);
                    if (!string.IsNullOrWhiteSpace(nestedFields))
                    {
                        var parametersString = GetFieldParameters(fieldName);
                        sb.AppendLine($"{indent}{fieldName}{parametersString} {{");
                        sb.Append(nestedFields);
                        sb.AppendLine($"{indent}}}");
                    }
                }
                else
                {
                    sb.AppendLine($"{indent}{fieldName}");
                }
            }
            else if (IsNullableType(prop.PropertyType))
            {
                var underlyingType = GetUnderlyingType(prop.PropertyType);
                if (!IsSimpleType(underlyingType))
                {
                    var nestedFields = GenerateFieldsRecursively(underlyingType, currentDepth + 1, maxDepth);
                    if (!string.IsNullOrWhiteSpace(nestedFields))
                    {
                        var parametersString = GetFieldParameters(fieldName);
                        sb.AppendLine($"{indent}{fieldName}{parametersString} {{");
                        sb.Append(nestedFields);
                        sb.AppendLine($"{indent}}}");
                    }
                }
                else
                {
                    sb.AppendLine($"{indent}{fieldName}");
                }
            }
            else
            {
                // Complex object 
                var nestedFields = GenerateFieldsRecursively(prop.PropertyType, currentDepth + 1, maxDepth);
                if (!string.IsNullOrWhiteSpace(nestedFields))
                {
                    var parametersString = GetFieldParameters(fieldName);
                    sb.AppendLine($"{indent}{fieldName}{parametersString} {{");
                    sb.Append(nestedFields);
                    sb.AppendLine($"{indent}}}");
                }
            }
        }

        _visitedTypes.Remove(type);
        return sb.ToString();
    }

    private static bool IsSimpleType(Type type)
    {
        var checkType = Nullable.GetUnderlyingType(type) ?? type;
        
        return checkType.IsPrimitive || 
               checkType.IsEnum ||
               checkType == typeof(string) ||
               checkType == typeof(DateTime) ||
               checkType == typeof(DateTimeOffset) ||
               checkType == typeof(decimal) ||
               checkType == typeof(float) ||
               checkType == typeof(double) ||
               checkType == typeof(Guid) ||
               checkType == typeof(TimeSpan);
    }

    private static bool IsListType(Type type)
    {
        return type.IsGenericType && 
               (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    public static bool IsComplexType(Type type)
    {
        var checkType = Nullable.GetUnderlyingType(type) ?? type;
    
        return !IsSimpleType(type) &&
               !checkType.IsArray &&
               !IsListType(checkType);
    }
    
    private static bool IsNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null || 
               (!type.IsValueType && type != typeof(string));
    }

    private static Type? GetListElementType(Type listType)
    {
        return listType.IsGenericType ? listType.GetGenericArguments()[0] : null;
    }

    private static Type GetUnderlyingType(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }
    
    private string GetFieldParameters(string fieldName)
    {
        var foundObject = limitSkipObjects?.FirstOrDefault(obj => obj.Name == fieldName);
        if (foundObject == null) return "";

        var parameters = foundObject.Properties
            .Select(kvp => $"{kvp.Key}: {kvp.Value}")
            .ToArray();

        return parameters.Length > 0 ? $"({string.Join(", ", parameters)})" : "";
    }
}