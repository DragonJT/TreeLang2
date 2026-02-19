
enum TokenKind
{
    Identifier,
    Keyword,
    Number,
    Operator,
    String,
    Other,
}

class Token(TokenKind kind, string value)
{
    public readonly TokenKind kind = kind;
    public readonly string value = value;
}

static class Tokenizer
{
    static readonly HashSet<string> operators = ["!", "+", "-", "&", "|", "=", "<", ">", 
        "<=", ">=", "&&", "||", "==", "++", "--"];
    static readonly HashSet<string> keywords = ["struct", "true", "false", "while", "var", "if"];

    static bool IsDigit(char c)
    {
        return c>='0' && c<='9';
    }

    static bool IsCharacter(char c)
    {
        return (c>='a' && c<='z') || (c>='A' && c<='Z') || c=='_';
    }

    static bool IsAlphaNumeric(char c)
    {
        return IsCharacter(c) || IsDigit(c);
    }

    public static List<Token> Tokenize(string code)
    {
        List<Token> tokens = [];
        int index = 0;
        while (true)
        {
            if(index >= code.Length)
            {
                return tokens;
            }
            var c = code[index];
            if (IsCharacter(c))
            {
                List<char> chars = [c];
                index++;
                while (true)
                {
                    if(index >= code.Length)
                    {
                        break;
                    }
                    c = code[index];
                    if (IsAlphaNumeric(c))
                    {
                        chars.Add(c);
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
                var identifier = new string([..chars]);
                if (keywords.Contains(identifier))
                {
                    tokens.Add(new(TokenKind.Keyword, identifier));
                }
                else
                {
                    tokens.Add(new (TokenKind.Identifier, identifier));
                }
            }
            else if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
            {
                index++;
            }
            else if(IsDigit(c))
            {
                List<char> chars = [c];
                index++;
                while (true)
                {
                    if(index >= code.Length)
                    {
                        break;
                    }
                    c = code[index];
                    if(IsDigit(c) || c=='.')
                    {
                        chars.Add(c);
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
                tokens.Add(new (TokenKind.Number, new string([..chars])));
            }
            else if(c == '"')
            {
                List<char> chars = [c];
                index++;
                while (true)
                {
                    if(index >= code.Length)
                    {
                        break;
                    }
                    c = code[index];
                    if(c == '"' && code[index-1] != '\\')
                    {
                        chars.Add(c);
                        index++;
                        break;
                    }
                    else
                    {
                        chars.Add(c);
                        index++;
                    }
                }
                tokens.Add(new(TokenKind.String, new string([..chars])));
            }
            else if(operators.Contains(c.ToString()))
            {
                List<char> chars = [c];
                index++;
                while (true)
                {
                    if(index >= code.Length)
                    {
                        break;
                    }
                    c = code[index];
                    if(operators.Contains(new string([..chars, c])))
                    {
                        chars.Add(c);
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
                tokens.Add(new (TokenKind.Operator, new string([..chars])));
            }
            else
            {
                tokens.Add(new(TokenKind.Other, c.ToString()));
                index++;
            }
        }
    }

}