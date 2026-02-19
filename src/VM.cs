using Raylib_cs;
using System.Reflection;
using System.Numerics;

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

class Call(string name, Type returnType, Type[] parameters, Func<object[], object> func)
{
    public readonly string name = name;
    public readonly Type returnType = returnType;
    public readonly Type[] parameters = parameters;
    public readonly Func<object[], object> func = func;

    public object Invoke(object[] args)
    {
        return func(args);
    }

    public override string ToString()
    {
        return $"{returnType} {name}({string.Join(", ", parameters.Select(p=>p.Name).ToArray())})";
    }
}

class FunctionCalls
{
    readonly List<Call> calls = [];

    public void AddCSharpTypes(Type[] types)
    {
        foreach(var type in types)
        {
            var constructors = type.GetConstructors();
            foreach(var c in constructors)
            {
                calls.Add(new(
                    type.Name, 
                    type, 
                    [..c.GetParameters().Select(p=>p.ParameterType)],
                    c.Invoke));
            }

            var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach(var m in staticMethods)
            {
                if(m.ReturnType == typeof(CBool))
                {
                    calls.Add(new(
                        m.Name,
                        typeof(bool),
                        [..m.GetParameters().Select(p=>p.ParameterType)],
                        a => (bool)(CBool)m.Invoke(null, a)));
                }
                else
                {
                    calls.Add(new(
                        m.Name, 
                        m.ReturnType,
                        [.. m.GetParameters().Select(p=>p.ParameterType)], 
                        a => m.Invoke(null, a)));
                }
            }
        }
    }

    public void AddCall(string name, Type returnType, Type[] parameters, Func<object[], object> func)
    {
        calls.Add(new (name, returnType, parameters, func));
    }

    static bool IsAssignableToOrConvertableTo(Type a, Type b)
    {
        if (a.IsAssignableTo(b))
        {
            return true;
        }
        else if(a == typeof(int) && b == typeof(float))
        {
            return true;
        }
        return false;
    }

    static bool IsMatch(Type[] argTypes, Type[] paramTypes)
    {
        if(argTypes.Length == paramTypes.Length)
        {
            for(var i = 0; i < argTypes.Length; i++)
            {
                if(!IsAssignableToOrConvertableTo(argTypes[i], paramTypes[i]))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    public Call GetCall(string name, Type[] argTypes)
    {
        var call = calls.FirstOrDefault(c => c.name == name && IsMatch(argTypes, c.parameters));
        if(call == null)
        {
            var str = name+": ("+string.Join(", ", argTypes.Select(a=>a.Name).ToArray())+")";
            throw new Exception(str);
        }
        return call;
    }
}

static class VM
{
    readonly static Dictionary<string, Func<Tree, object>> trees = [];
    readonly static Dictionary<string, Func<Tree, Type>> treeTypes = [];
    readonly static Dictionary<string, object> globals = [];
    readonly static Stack<FunctionScopes> functionScopesStack = [];
    readonly static FunctionCalls functionCalls = new();

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

    public static void Init(Tree root)
    {
        //Globals
        globals.Add("BLUE", Color.Blue);
        globals.Add("WHITE", Color.White);
        globals.Add("RED", Color.Red);
        globals.Add("GREEN", Color.Green);

        //Trees
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
            var argTypes = args.children.Select(CalculateType).ToArray();
            var call = functionCalls.GetCall(name, argTypes);
            return call.Invoke([.. args.children.Select(a=>a.Run())]);
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
            var argTypes = args.children.Select(CalculateType).ToArray();
            var call = functionCalls.GetCall(name, argTypes);
            return call.returnType;
        });

        foreach(var c in root.children)
        {
            functionCalls.AddCall(c.GetField("Name").value, typeof(void), [], a =>
            {
                c.Run();
                return null;
            });
        }
        functionCalls.AddCSharpTypes([typeof(Raylib), typeof(Vector2), typeof(Console)]);
    }

    public static object Call(string name, object[] args)
    {
        var call = functionCalls.GetCall(name, [..args.Select(a=>a.GetType())]);
        call.Invoke(args);
        return call.returnType;
    }

    static object Run(this Tree tree)
    {
        return trees[tree.name](tree);
    }

    public static Type CalculateType(this Tree tree)
    {
        return treeTypes[tree.name](tree);
    }
}