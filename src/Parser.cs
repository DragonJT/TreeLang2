

interface IParse;

class Obj(string code) : IParse
{
    public string code = code;
}

class Arr(string code) : IParse
{
    public string code = code;
}

class SplitArr(string code, string deliminator) : IParse
{
    public string code = code;
    public string deliminator = deliminator;
}

class Expression(string code) : IParse
{
    public string code = code;
}

class Identifier : IParse{}

class Number : IParse{}

class String : IParse{}

class BinaryOperators(string[] opGroups, string parse) : IParse
{
    public string[] opGroups = opGroups;
    public string parse = parse;
}

class UnaryOperators(string ops, string parse) : IParse
{
    public string ops = ops;
    public string parse = parse;
}

struct TokenReader(List<Token> tokens, int index = 0)
{
    readonly List<Token> tokens = tokens;
    int index = index;

    public bool IsIdentifier(out string value)
    {
        if(index < tokens.Count && tokens[index].kind == TokenKind.Identifier)
        {
            value = tokens[index].value;
            index++;
            return true;
        }
        value = null;
        return false;
    }

    public bool IsNumber(out string value)
    {
        if(index < tokens.Count && tokens[index].kind == TokenKind.Number)
        {
            value = tokens[index].value;
            index++;
            return true;
        }
        value = null;
        return false;
    }

    public bool IsString(out string value)
    {
        if(index < tokens.Count && tokens[index].kind == TokenKind.String)
        {
            value = tokens[index].value;
            index++;
            return true;
        }
        value = null;
        return false;
    }

    public bool IsKeyword(string keyword)
    {
        if(index < tokens.Count && tokens[index].kind == TokenKind.Keyword && tokens[index].value == keyword)
        {
            index++;
            return true;
        }
        return false;
    }

    public bool IsOperator(string op)
    {
        if(index < tokens.Count && tokens[index].kind == TokenKind.Operator && tokens[index].value == op)
        {
            index++;
            return true;
        }
        return false;
    }

    public bool IsOther(string other)
    {
        if(index < tokens.Count && tokens[index].kind == TokenKind.Other && tokens[index].value == other)
        {
            index++;
            return true;
        }
        return false;
    }

    public List<Token> GetExpressionTokens()
    {
        var start = index;
        var depth = 0;
        while (true)
        {
            if(index >= tokens.Count)
            {
                return tokens[start..];
            }
            var t = tokens[index];
            if(t.kind == TokenKind.Other)
            {
                if(t.value == "(")
                {
                    depth++;
                }
                else if(t.value == ")")
                {
                    depth--;
                    if(depth < 0)
                    {
                        return tokens[start..index];
                    }
                }
                else if(t.value == ";" && depth == 0)
                {
                    return tokens[start..index];
                }
                else if(t.value == "," && depth == 0)
                {
                    return tokens[start..index];
                }
                else if(t.value == "{" && depth == 0)
                {
                    return tokens[start..index];
                }
            }
            index++;
        }
    }

    public bool SplitTokensWithOperators(string opGroup, out string op, out List<Token> left, out List<Token> right)
    {
        var ops = opGroup.Split(' ');
        var depth = 0;
        for(var i = tokens.Count - 1; i >= 0; i--)
        {
            var t = tokens[i];
            if(t.kind == TokenKind.Other)
            {
                if(t.value == ")")
                {
                    depth++;   
                }
                else if(t.value == "(")
                {
                    depth--;   
                }
            }
            else if(t.kind == TokenKind.Operator)
            {
                if(depth == 0)
                {
                    if (ops.Contains(t.value))
                    {
                        left = tokens[..i];
                        right = tokens[(i+1)..];
                        op = t.value;
                        return true;
                    }
                }
            }
        }
        left = null;
        right = null;
        op = null;
        return false;
    }

    public bool UnaryOperators(string op, out List<Token> result)
    {
        if(tokens.Count >= 2 && tokens[0].kind == TokenKind.Operator && tokens[0].value == op)
        {
            result = tokens[1..];
            return true;
        }
        result = null;
        return false;
    }

    public TokenReader End()
    {
        return new TokenReader(tokens, tokens.Count);
    }
}

struct ParsedResult
{
    public bool valid;
    public TokenReader reader;
    public Tree tree;

    public ParsedResult(bool valid, TokenReader reader, Tree tree)
    {
        this.valid = valid;
        this.reader = reader;
        this.tree = tree;
    }
}

class Tree(string name, string value)
{
    public string name = name;
    public string value = value;
    public List<Tree> children = [];

    public override string ToString()
    {
        if(children.Count > 0)
        {
            var str = string.Join(", ", children.Select(c=>c.ToString()));
            return $"{name} {value} [{str}]";
        }
        else
        {
            return $"{name} {value}";
        }
    }
    
    public Tree GetField(string name)
    {
        var field = children.FirstOrDefault(c => c.name == name)
            ?? throw new Exception(name+": "+string.Join(", ", children.Select(c => c.name)));
        return field;
    }
}

static class Parser
{
    static Dictionary<string, IParse> parsers = [];

    public static void Create()
    {
        parsers.Add("Identifier", new Identifier());
        parsers.Add("Name", new Identifier());
        parsers.Add("Type", new Identifier());
        parsers.Add("Number", new Number());
        parsers.Add("String", new String());
        parsers.Add("UOps", new UnaryOperators("!", "Expr"));
        parsers.Add("Ops", new BinaryOperators(["< >", "+ -"], "Expr"));
        parsers.Add("Call", new Obj("Name(Arguments)"));
        parsers.Add("Expr", new Expression("Ops UOps Call Number String Identifier"));
        parsers.Add("Arguments", new SplitArr("Expr", ","));
        parsers.Add("CallStmt", new Obj("Call;"));
        parsers.Add("VarStmt", new Obj("var Name = Expr;"));
        parsers.Add("AssignStmt", new Obj("Name = Expr;"));
        parsers.Add("WhileStmt", new Obj("while Expr {Statements}"));
        parsers.Add("IfStmt", new Obj("if Expr {Statements}"));
        parsers.Add("Statements", new Arr("VarStmt WhileStmt IfStmt CallStmt AssignStmt"));
        parsers.Add("Field", new Obj("Type Name;"));
        parsers.Add("Fields", new Arr("Field"));
        parsers.Add("Parameter", new Obj("Type Name"));
        parsers.Add("Parameters", new SplitArr("Parameter", ","));
        parsers.Add("Struct", new Obj("struct Name{Fields}"));
        parsers.Add("Function", new Obj("Type Name(Parameters){Statements}"));
        parsers.Add("Root", new Arr("Struct Function"));
    }

    static ParsedResult ParseBranches(string[] branches, TokenReader reader)
    {
        foreach(var b in branches)
        {
            var result = Parse(b, reader);
            if (result.valid)
            {
                return result;
            }
        }
        return new ParsedResult(false, reader, null);
    }

    static Tree GetTree(this ParsedResult parsedResult)
    {
        if (parsedResult.valid)
        {
            return parsedResult.tree;   
        }
        else
        {
            return new Tree("Error", "...");
        }
    }

    public static ParsedResult Parse(string name, TokenReader reader)
    {
        var parser = parsers[name];
        if(parser is Obj obj)
        {
            var ureader = reader;
            var tree = new Tree(name, null);
            var tokens = Tokenizer.Tokenize(obj.code);
            for(var i = 0;i<tokens.Count;i++)
            {
                var t = tokens[i];
                if(t.kind == TokenKind.Keyword)
                {
                    if (!ureader.IsKeyword(t.value))
                    {
                        return new ParsedResult(false, reader, null);
                    }
                }
                else if(t.kind == TokenKind.Identifier)
                {
                    var result = Parse(t.value, ureader);
                    if (result.valid)
                    {
                        ureader = result.reader;
                        tree.children.Add(result.tree);
                    }
                    else
                    {
                        return new ParsedResult(false, reader, null);
                    }
                }
                else if(t.kind == TokenKind.Other)
                {
                    if (!ureader.IsOther(t.value))
                    {
                        return new ParsedResult(false, reader, null);
                    }
                }
                else if(t.kind == TokenKind.Operator)
                {
                    if (!ureader.IsOperator(t.value))
                    {
                        return new ParsedResult(false, reader, null);
                    }
                }
                else
                {
                    throw new Exception();
                }
            }
            return new ParsedResult(true, ureader, tree);
        }
        else if(parser is Identifier)
        {
            if(reader.IsIdentifier(out string value))
            {
                return new ParsedResult(true, reader, new Tree(name, value));
            }
            return new ParsedResult(false, reader, null);
        }
        else if(parser is Number)
        {
            if(reader.IsNumber(out string value))
            {
                return new ParsedResult(true, reader, new Tree(name, value));
            }
            return new ParsedResult(false, reader, null);
        }
        else if(parser is String)
        {
            if(reader.IsString(out string value))
            {
                return new ParsedResult(true, reader, new Tree(name, value));
            }
            return new ParsedResult(false, reader, null);
        }
        else if(parser is Arr arr)
        {
            Tree tree = new(name, null);
            var branches = arr.code.Split(' ');
            while(true)
            {
                var result = ParseBranches(branches, reader);
                if (result.valid)
                {
                    reader = result.reader;
                    tree.children.Add(result.tree);
                }
                else
                {
                    return new ParsedResult(true, reader, tree);
                }
            }
        }
        else if(parser is SplitArr splitArr)
        {
            Tree tree = new(name, null);
            var branches = splitArr.code.Split(' ');
            while(true)
            {
                var result = ParseBranches(branches, reader);
                if (result.valid)
                {
                    reader = result.reader;
                    tree.children.Add(result.tree);
                }
                else
                {
                    return new ParsedResult(true, reader, tree);
                }
                if (!reader.IsOther(splitArr.deliminator))
                {
                    return new ParsedResult(true, reader, tree);
                }
            }
        }
        else if(parser is BinaryOperators binaryOperators)
        {
            foreach(var og in binaryOperators.opGroups)
            {
                if(reader.SplitTokensWithOperators(og, out string op, out var left, out var right))
                {
                    var tree = new Tree(op, null);
                    tree.children.Add(Parse(binaryOperators.parse, new TokenReader(left)).GetTree());
                    tree.children.Add(Parse(binaryOperators.parse, new TokenReader(right)).GetTree());
                    return new ParsedResult(true, reader.End(), tree);
                }
            }
            return new ParsedResult(false, reader, null);
        }
        else if(parser is UnaryOperators unaryOperators)
        {
            foreach(var uo in unaryOperators.ops.Split(' '))
            {
                if(reader.UnaryOperators(uo, out var tokens))
                {
                    var tree = new Tree(uo, null);
                    tree.children.Add(Parse(unaryOperators.parse, new TokenReader(tokens)).GetTree());
                    return new ParsedResult(true, reader.End(), tree);
                }
            }
            return new ParsedResult(false, reader, null);
        }
        else if(parser is Expression expression)
        {
            var tokens = reader.GetExpressionTokens();
            var newReader = new TokenReader(tokens);
            var branches = expression.code.Split(' ');
            var result = ParseBranches(branches, newReader);
            if (result.valid)
            {
                return new ParsedResult(true, reader, result.tree);
            }
            else
            {
                return new ParsedResult(false, reader, null);
            }
        }
        else
        {
            throw new Exception();
        }
    }
}
