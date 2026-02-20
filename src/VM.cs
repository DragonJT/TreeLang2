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

class Call(string name, Type returnType, Type[] paramTypes, Func<object[], object> func)
{
    public readonly string name = name;
    public readonly Type returnType = returnType;
    public readonly Type[] paramTypes = paramTypes;
    public readonly Func<object[], object> func = func;

    public object Invoke(object[] args)
    {
        return func(args);
    }

    public override string ToString()
    {
        return $"{returnType} {name}({string.Join(", ", paramTypes.Select(p=>p.Name).ToArray())})";
    }
}

class FunctionCalls
{
    readonly List<Call> calls = [];

    public void AddCSharpType(Type type)
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

    public void AddCall1<T,TR>(string name, Func<T,TR> func)
    {
        calls.Add(new (name, typeof(TR), [typeof(T)], a=>func((T)a[0])));
    }

    public void AddCall2<T1,T2,TR>(string name, Func<T1,T2,TR> func)
    {
        calls.Add(new (name, typeof(TR), [typeof(T1), typeof(T2)], a=>func((T1)a[0], (T2)a[1])));
    }

    public void AddCall(string name, Type returnType, Type[] parameters, Func<object[], object> func)
    {
        calls.Add(new (name, returnType, parameters, func));
    }

    static bool IsMatch(Type[] argTypes, Type[] paramTypes)
    {
        if(argTypes.Length == paramTypes.Length)
        {
            for(var i = 0; i < argTypes.Length; i++)
            {
                if(argTypes[i] != paramTypes[i])
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
        var call = calls.FirstOrDefault(c => c.name == name && IsMatch(argTypes, c.paramTypes));
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
    readonly static Dictionary<string, Type> types = [];

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
        string[] assignOps = ["+", "-", "*", "/"];
        string[] binaryOps = ["+", "-", "*", "/", ">", "<"];
        string[] unaryOps = ["!"];

        //Globals
        globals.Add("BLUE", Color.Blue);
        globals.Add("WHITE", Color.White);
        globals.Add("BLACK", Color.Black);
        globals.Add("RED", Color.Red);
        globals.Add("GREEN", Color.Green);

        globals.Add("LEFT", KeyboardKey.Left);
        globals.Add("RIGHT", KeyboardKey.Right);
        globals.Add("PERSPECTIVE", CameraProjection.Perspective);

        //Trees
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
            if (t.value.Contains('.'))
            {
                return float.Parse(t.value);
            }
            return int.Parse(t.value);
        });
        trees.Add("String", t =>
        {
            return t.value[1..^1];
        });

        foreach(var op in binaryOps)
        {
            trees.Add(op, t=>
            {
                var call = functionCalls.GetCall(op, [t.children[0].CalculateType(), t.children[1].CalculateType()]);
                return call.Invoke([t.children[0].Run(), t.children[1].Run()]);
            });
        }
        foreach(var op in unaryOps)
        {
            trees.Add(op, t=>
            {
                var call = functionCalls.GetCall(op, [t.children[0].CalculateType()]);
                return call.Invoke([t.children[0].Run()]);
            });
        }
        
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
        trees.Add("=", t =>
        {
            var name = t.children[0].value;
            var value = t.children[1].Run();
            var functionScope = functionScopesStack.Peek();
            functionScope.TrySetVar(name, value);
            return null;
        });

        foreach(var assignOp in assignOps)
        {
            trees.Add(assignOp+"=", t =>
            {
                var name = t.children[0].value;
                var b = t.children[1].Run();
                var btype = t.children[1].CalculateType();
                var functionScope = functionScopesStack.Peek();
                if (functionScope.TryGetVar(name, out var a))
                {
                    var call = functionCalls.GetCall(assignOp, [a.GetType(), btype]);
                    functionScope.TrySetVar(name, call.Invoke([a,b]));
                }
                else
                {
                    throw new Exception($"Cant find variable: {name}");
                }
                return null;
            });
        }
        
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
        trees.Add("ExprStmt", t =>
        {
            t.children[0].Run();
            return null;
        });

        //TreeTypes
        treeTypes.Add("Identifier", t =>
        {
            return GetIdentifier(t.value).GetType();
        });
        treeTypes.Add("Number", t =>
        {
            if(t.value.Contains('.'))
            {
                return typeof(float);
            }
            return typeof(int);
        });
        treeTypes.Add("String", t =>
        {
            return typeof(string);
        });
        
        foreach(var op in binaryOps)
        {
            treeTypes.Add(op, t=>
            {
                var call = functionCalls.GetCall(op, [t.children[0].CalculateType(), t.children[1].CalculateType()]);
                return call.returnType;
            });
        }
        foreach(var op in unaryOps)
        {
            treeTypes.Add(op, t=>
            {
                var call = functionCalls.GetCall(op, [t.children[0].CalculateType()]);
                return call.returnType;
            });
        }
        
        treeTypes.Add("Call", t =>
        {
            var name = t.GetField("Name").value;
            var args = t.GetField("Arguments");
            var argTypes = args.children.Select(CalculateType).ToArray();
            var call = functionCalls.GetCall(name, argTypes);
            return call.returnType;
        });


        types.Add("Raylib", typeof(Raylib));
        types.Add("Console", typeof(Console));
        types.Add("int", typeof(int));
        types.Add("float", typeof(float));
        types.Add("void", typeof(void));
        types.Add("Vector2", typeof(Vector2));
        types.Add("Matrix3x2", typeof(Matrix3x2));
        types.Add("Color", typeof(Color));
        types.Add("Matrix4x4", typeof(Matrix4x4));
        types.Add("Vector3", typeof(Vector3));
        types.Add("Camera3D", typeof(Camera3D));

        foreach(var c in root.children)
        {
            var parameters = c.GetField("Parameters").children;
            var parameterTypes = parameters
                .Select(p=>types[p.GetField("Type").value])
                .ToArray();
            var parameterNames = parameters.Select(p => p.GetField("Name").value).ToArray();
            var returnType = types[c.GetField("Type").value];

            functionCalls.AddCall(c.GetField("Name").value, returnType, parameterTypes, a =>
            {
                var functionScopes = new FunctionScopes();
                functionScopesStack.Push(functionScopes);
                functionScopes.PushScope();
                
                for(var i = 0; i < a.Length; i++)
                {
                    functionScopes.CreateVar(parameterNames[i], a[i]);
                } 
                var returnValue = c.GetField("Statements").Run();
                functionScopesStack.Pop();
                return returnValue;
            });
        }

        functionCalls.AddCall1<bool,bool>("!", a=>!a);
        functionCalls.AddCall2<float,float,float>("+", (a, b)=>a + b);
        functionCalls.AddCall2<int,int,int>("+", (a, b)=>a + b);
        functionCalls.AddCall2<float,float,float>("-", (a, b)=>a - b);
        functionCalls.AddCall2<int,int,int>("-", (a, b)=>a - b);
        functionCalls.AddCall2<float,float,float>("*", (a,b)=>a*b);
        functionCalls.AddCall2<int,int,int>("*", (a,b)=>a*b);
        functionCalls.AddCall2<Matrix3x2,Matrix3x2,Matrix3x2>("*", (a,b)=>a*b);
        functionCalls.AddCall2<Matrix4x4,Matrix4x4,Matrix4x4>("*", (a,b)=>a*b);
        functionCalls.AddCall2<float,float,bool>(">", (a,b)=>a>b);
        functionCalls.AddCall2<int,int,bool>(">", (a,b)=>a>b);
        functionCalls.AddCall2<float,float,bool>("<", (a,b)=>a<b);
        functionCalls.AddCall2<int,int,bool>("<", (a,b)=>a<b);

        foreach(var t in types.Values)
        {
            functionCalls.AddCSharpType(t);
        }
    }

    public static object Call(string name, object[] args)
    {
        var call = functionCalls.GetCall(name, [..args.Select(a=>a.GetType())]);
        return call.Invoke(args);
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