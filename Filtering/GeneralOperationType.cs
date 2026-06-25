using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Filtering;

public class GeneralOperationType
{
    private readonly HashSet<string> _allowedFields;

    public Dictionary<string, Func<Expression, Expression, Expression>> Arguments { get; }

    public GeneralOperationType(HashSet<string> allowedFields)
    {
        _allowedFields = allowedFields;

        Arguments = new Dictionary<string, Func<Expression, Expression, Expression>>
        {
            { "==",         Expression.Equal },
            { "!=",         Expression.NotEqual },
            { "<",          Expression.LessThan },
            { ">",          Expression.GreaterThan },
            { "<=",         Expression.LessThanOrEqual },
            { ">=",         Expression.GreaterThanOrEqual },
            { "Contains",   (l, r) => Expression.Call(l, typeof(string).GetMethod("Contains",   [typeof(string)])!, r) },
            { "StartsWith", (l, r) => Expression.Call(l, typeof(string).GetMethod("StartsWith", [typeof(string)])!, r) },
            { "EndsWith",   (l, r) => Expression.Call(l, typeof(string).GetMethod("EndsWith",   [typeof(string)])!, r) },
        };
    }

    public Expression ParseQuery<T>(string query, ParameterExpression parameter)
    {
        // Находим оператор в строке (от длинных к коротким чтобы >= не путать с >)
        var op = Arguments.Keys
            .OrderByDescending(k => k.Length)
            .FirstOrDefault(query.Contains)
            ?? throw new ArgumentException($"No valid operator found in: '{query}'");

        var parts = query.Split(new[] { op }, 2, StringSplitOptions.None);
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid query format: '{query}'");

        var fieldName = parts[0].Trim();
        var rawValue  = parts[1].Trim();

        // Whitelist — только разрешённые поля
        if (!_allowedFields.Contains(fieldName))
            throw new ArgumentException($"Field '{fieldName}' is not allowed for filtering.");

        // Поддержка вложенных свойств: Address.City
        PropertyInfo? propertyInfo = null;
        Type currentType = typeof(T);
        Expression propertyExpression = parameter;

        foreach (var part in fieldName.Split('.'))
        {
            propertyInfo = currentType.GetProperty(part)
                ?? throw new ArgumentException($"Property '{part}' not found on '{currentType.Name}'.");

            propertyExpression = Expression.Property(propertyExpression, propertyInfo);
            currentType = propertyInfo.PropertyType;
        }

        var targetType = propertyInfo!.PropertyType;
        object parsedValue = targetType.IsEnum
            ? Enum.Parse(targetType, rawValue, ignoreCase: true)
            : Convert.ChangeType(rawValue, targetType);
        var right = Expression.Constant(parsedValue, targetType);

        return Arguments[op](propertyExpression, right);
    }
}
