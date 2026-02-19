using Raylib_cs;
using System.Reflection;

class FunctionScopes
{
    List<Dictionary<string, object>> scopes = [];

    public void PushScope()
    {
        scopes.Add([]);
    }

    public void PopScope()
    {
        scopes.RemoveAt(scopes.Count - 1);
    }

    Dictionary<string, object> Peek()
    {
        return scopes[^1];
    }

    public void CreateVar(string name, object value)
    {
        var peek = Peek();
        peek.Add(name, value);
    }

    public bool TryGetVar(string name, out object value)
    {
        for(var i = scopes.Count-1; i>=0; i--)
        {
            if(scopes[i].TryGetValue(name, out value))
            {
                return true;
            }
        }
        value = null;
        return false;
    }

    public bool TrySetVar(string name, object value)
    {
        for(var i = scopes.Count-1; i>=0; i--)
        {
            if(scopes[i].ContainsKey(name))
            {
                scopes[i][name] = value;
                return true;
            }
        }
        return false;
    }
}

static class VM
{
    readonly static Dictionary<string, Func<Tree, object>> trees = [];
    readonly static Dictionary<string, Func<Tree, Type>> treeTypes = [];
    readonly static Dictionary<string, object> globals = [];
    readonly static Stack<FunctionScopes> functionScopesStack = [];

    static bool IsMatch(MethodInfo method, Type[] types)
    {
        var parameters = method.GetParameters();
        var length = parameters.Length;
        if(length == types.Length)
        {
            for(var i = 0; i < length; i++)
            {
                if(parameters[i].ParameterType != types[i])
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    static object GetIdentifier(string name)
    {
        var functionScope = functionScopesStack.Peek();
        if(functionScope.TryGetVar(name, out var value))
        {
            return value;
        }
        else
        {
            return globals[name];
        }
    }

    public static void Init()
    {
        //Globals
        globals.Add("BLUE", Color.Blue);
        globals.Add("WHITE", Color.White);
        globals.Add("RED", Color.Red);
        globals.Add("GREEN", Color.Green);

        //Trees
        trees.Add("Root", t =>
        {
            return t.children.First(c=>c.GetField("Name").value == "Main").Run();
        });
        trees.Add("Function", t =>
        {
            functionScopesStack.Push(new());
            var returnValue = t.GetField("Statements").Run();
            functionScopesStack.Pop();
            return returnValue;
        });
        trees.Add("Statements", t =>
        {
            var functionScope = functionScopesStack.Peek();
            functionScope.PushScope();
            foreach(var c in t.children)
            {
                c.Run();
            }
            functionScope.PopScope();
            return null;
        });
        trees.Add("Identifier", t =>
        {
            return GetIdentifier(t.value);
        });
        trees.Add("Number", t =>
        {
            return int.Parse(t.value);
        });
        trees.Add("String", t =>
        {
            return t.value[1..^1];
        });
        trees.Add("+", t=>
        {
            return (int)t.children[0].Run() + (int)t.children[1].Run();
        });
        trees.Add("-", t=>
        {
            return (int)t.children[0].Run() - (int)t.children[1].Run();
        });
        trees.Add("<", t=>
        {
            return (int)t.children[0].Run() < (int)t.children[1].Run();
        });
        trees.Add(">", t=>
        {
            return (int)t.children[0].Run() > (int)t.children[1].Run();
        });
        trees.Add("!", t=>
        {
            return !(bool)t.children[0].Run();
        });
        trees.Add("WhileStmt", t =>
        {
            while ((bool)t.children[0].Run())
            {
                t.GetField("Statements").Run();
            }
            return null;
        });
        trees.Add("VarStmt", t =>
        {
            var name = t.GetField("Name").value;
            var value = t.children[1].Run();
            var functionScope = functionScopesStack.Peek();
            functionScope.CreateVar(name, value);
            return null;
        });
        trees.Add("AssignStmt", t =>
        {
            var name = t.GetField("Name").value;
            var value = t.children[1].Run();
            var functionScope = functionScopesStack.Peek();
            functionScope.TrySetVar(name, value);
            return null;
        });
        trees.Add("IfStmt", t =>
        {
            var condition = t.children[0].Run();
            if ((bool)condition)
            {
                t.GetField("Statements").Run();
            }
            return null;
        });
        trees.Add("Call", t =>
        {
            var name = t.GetField("Name").value;
            var args = t.GetField("Arguments");
            var argTypes = args.children.Select(a=>a.CalculateType()).ToArray();
            var argValues = args.children.Select(c=>c.Run()).ToArray();
            var methods = typeof(Raylib).GetMethods(BindingFlags.Public|BindingFlags.Static);
            var method = methods.First(m=>m.Name == name && IsMatch(m, argTypes));
            var returnValue = method.Invoke(null, argValues);
            if(method.ReturnType == typeof(CBool))
            {
                return (bool)(CBool)returnValue;
            }
            else
            {
                return returnValue;
            }
        });
        trees.Add("CallStmt", t =>
        {
            t.GetField("Call").Run();
            return null;
        });

        //TreeTypes
        treeTypes.Add("Identifier", t =>
        {
            return GetIdentifier(t.value).GetType();
        });
        treeTypes.Add("Number", t =>
        {
            return typeof(int);
        });
        treeTypes.Add("String", t =>
        {
            return typeof(string);
        });
        treeTypes.Add("+", t=>
        {
            return typeof(int);
        });
        treeTypes.Add("-", t=>
        {
            return typeof(int);
        });
        treeTypes.Add("<", t=>
        {
            return typeof(bool);
        });
        treeTypes.Add(">", t=>
        {
            return typeof(bool);
        });
        treeTypes.Add("!", t=>
        {
            return typeof(bool);
        });
        treeTypes.Add("Call", t =>
        {
            var name = t.GetField("Name").value;
            var args = t.GetField("Arguments");
            var argTypes = args.children.Select(a=>a.CalculateType()).ToArray();
            var methods = typeof(Raylib).GetMethods(BindingFlags.Public|BindingFlags.Static);
            var method = methods.First(m=>m.Name == name && IsMatch(m, argTypes));
            var returnType = method.ReturnType;
            if(returnType == typeof(CBool))
            {
                return typeof(bool);
            }
            else
            {
                return returnType;
            }
        });
    }

    public static object Run(this Tree tree)
    {
        return trees[tree.name](tree);
    }

    public static Type CalculateType(this Tree tree)
    {
        return treeTypes[tree.name](tree);
    }
}