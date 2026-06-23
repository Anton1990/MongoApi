namespace MongoApi.Filtering;

public static class QueryTokenizer
{
    public static List<string> TokenizeLogical(string query)
    {
        var tokens = new List<string>();
        var current = string.Empty;

        void Flush()
        {
            if (!string.IsNullOrWhiteSpace(current))
            {
                tokens.Add(current.Trim());
                current = string.Empty;
            }
        }

        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];

            if (c == '(' || c == ')')
            {
                Flush();
                tokens.Add(c.ToString());
                continue;
            }

            // Фикс оригинала: проверяем границы слова (пробелы вокруг AND/OR)
            // чтобы "Bandwidth" не разбивалось на "B" AND "width"
            if (i + 4 < query.Length && query.Substring(i, 5).ToUpper() == " AND ")
            {
                Flush();
                tokens.Add("AND");
                i += 4;
                continue;
            }

            if (i + 3 < query.Length && query.Substring(i, 4).ToUpper() == " OR ")
            {
                Flush();
                tokens.Add("OR");
                i += 3;
                continue;
            }

            current += c;
        }

        Flush();
        return tokens;
    }
}
