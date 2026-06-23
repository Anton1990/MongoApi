using System.Linq.Expressions;

namespace MongoApi.Filtering;

public class LogicalParser<T>
{
    private readonly List<string> _tokens;
    private int _pos;
    private readonly GeneralOperationType _operations;
    private readonly ParameterExpression _parameter;

    public LogicalParser(string query, GeneralOperationType operations)
    {
        _tokens = QueryTokenizer.TokenizeLogical(query);
        _operations = operations;
        _parameter = Expression.Parameter(typeof(T), "x");
        _pos = 0;
    }

    private string? Current => _pos < _tokens.Count ? _tokens[_pos] : null;
    private string Consume() => _tokens[_pos++];

    public Expression<Func<T, bool>> Parse()
    {
        var body = ParseOr();
        return Expression.Lambda<Func<T, bool>>(body, _parameter);
    }

    // Уровень OR (низший приоритет)
    private Expression ParseOr()
    {
        var left = ParseAnd();

        while (Current == "OR")
        {
            Consume();
            var right = ParseAnd();
            left = Expression.OrElse(left, right);
        }

        return left;
    }

    // Уровень AND (выше OR)
    private Expression ParseAnd()
    {
        var left = ParseFactor();

        while (Current == "AND")
        {
            Consume();
            var right = ParseFactor();
            left = Expression.AndAlso(left, right);
        }

        return left;
    }

    // Атом: скобки или одно условие
    private Expression ParseFactor()
    {
        if (Current == "(")
        {
            Consume(); // '('
            var inner = ParseOr();

            if (Current != ")")
                throw new InvalidOperationException("Missing closing parenthesis ')'");

            Consume(); // ')'
            return inner;
        }

        var token = Consume();
        return _operations.ParseQuery<T>(token, _parameter);
    }
}
