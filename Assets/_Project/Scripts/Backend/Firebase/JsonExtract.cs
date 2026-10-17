// Pulls one value out of a piece of JSON text without parsing the rest. The server wraps its answer as
// {"result": {...}} and the answer can contain a whole saved game as one long string full of braces and quotes,
// so this walks the text and skips over strings properly.
public static class JsonExtract
{
    // The object or array text that follows "key": at the top level, or null
    public static string Value(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return null;

        int depth = 0;
        int i = 0;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"')
            {
                int end = EndOfString(json, i);
                if (end < 0) return null;
                // A key at depth 1 followed by a colon
                if (depth == 1 && json.Substring(i + 1, end - i - 1) == key)
                {
                    int colon = SkipSpaces(json, end + 1);
                    if (colon < json.Length && json[colon] == ':')
                    {
                        int start = SkipSpaces(json, colon + 1);
                        int stop = EndOfValue(json, start);
                        return stop < 0 ? null : json.Substring(start, stop - start);
                    }
                }
                i = end + 1;
                continue;
            }
            if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
            i++;
        }
        return null;
    }

    private static int SkipSpaces(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }

    // Index of the closing quote of the string starting at i
    private static int EndOfString(string s, int i)
    {
        for (int j = i + 1; j < s.Length; j++)
        {
            if (s[j] == '\\') j++;
            else if (s[j] == '"') return j;
        }
        return -1;
    }

    // Index just after the value that starts at i (an object, array, string or plain value)
    private static int EndOfValue(string s, int i)
    {
        if (i >= s.Length) return -1;
        char first = s[i];
        if (first == '"')
        {
            int end = EndOfString(s, i);
            return end < 0 ? -1 : end + 1;
        }
        if (first == '{' || first == '[')
        {
            int depth = 0;
            for (int j = i; j < s.Length; j++)
            {
                char c = s[j];
                if (c == '"') { j = EndOfString(s, j); if (j < 0) return -1; continue; }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') { depth--; if (depth == 0) return j + 1; }
            }
            return -1;
        }
        int k = i;
        while (k < s.Length && s[k] != ',' && s[k] != '}' && s[k] != ']') k++;
        return k;
    }
}
