using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

public class Hoge
{
    int @if = 5;
    float ff = 0.5f;
    string sf = "hog";

    int ip { get; set; }
    float fp { get; set; }
    string sp { get; set; }

    int m(int i)
    {
        return i * i;
    }
}

class Program
{
    static void Main(string[] args)
    {
        var script = @"
PushString:MAKI
PushInt:114514
PushFloat:99.99
PushNull:

PushNew:Hoge
PeekToGlobal:this
PushFromGlobal:this
PushFloat:2.70
PopField:Hoge.ff


PushFromGlobal:this
PushInt:8
Call:Hoge.m

// PushField:
// SetProp:
// Call:
        ";

        var lastStack = CsInterpreter.Eval(script);
        Console.WriteLine(string.Join("\n", lastStack));
    }
}


public class CsInterpreter
{
    public Dictionary<string, object> global = new Dictionary<string, object>();
    public static Stack<object> Eval(string script)
    {
        if(string.IsNullOrEmpty(script))
        {
            throw new InvalidScriptException("Script is empty");
        }

        var interpreter = new CsInterpreter();
        var lineNo = 0;
        var lines = script.Split("\n");
        try
        {
            for (; lineNo < lines.Length; lineNo++)
            {
                if(string.IsNullOrEmpty(lines[lineNo]) || string.IsNullOrWhiteSpace(lines[lineNo]))
                {
                    continue;
                }
                interpreter.EvalLine(lines[lineNo].Trim());
            }
        }
        catch(Exception e)
        {
            throw new InvalidScriptException(string.Format("Error in #{0} \n{1}", lineNo, e));
        }

        return interpreter.stack;
    }

    public class InvalidScriptException : Exception
    {
        public InvalidScriptException(string message) : base(message) {}
    }

    Stack<object> stack = new Stack<object>();
    List<string> scriptLines = new List<string>();

    CsInterpreter() {}

    void EvalLine(string line)
    {
        if(line.StartsWith("//"))
        {
            return;
        }

        var split = line.Split(":");
        var command = split[0];
        var commandArg = split.Length > 0 ? split[1] : null;
        switch(command)
        {
            case "PushString":
            {
                stack.Push(commandArg);
                break;
            }
            case "PushInt":
            {
                stack.Push(int.Parse(commandArg));
                break;
            }
            case "PushFloat":
            {
                stack.Push(float.Parse(commandArg));
                break;
            }
            case "PushChar":
            {
                stack.Push(char.Parse(commandArg));
                break;
            }
            case "PushBool":
            {
                stack.Push(bool.Parse(commandArg));
                break;
            }
            case "PushNull":
            {
                stack.Push(null);
                break;
            }
            case "PushNew":
            {
                var tokens = SplitToTokens(commandArg, true);
                var ctors = Type.GetType(tokens[0]).GetConstructors();
                var paramTypes = tokens.Skip(2).Select(name => Type.GetType(name));
                foreach (var ctor in ctors)
                {
                    var _paramTypes = ctor.GetParameters().Select(inf => inf.ParameterType);
                    if(_paramTypes.SequenceEqual(paramTypes))
                    {
                        var @params = ctor.GetParameters().Select(_ => stack.Pop());
                        var instance = ctor.Invoke(@params.ToArray());
                        stack.Push(instance);
                        goto succsess;
                    }
                }
                goto default;
                succsess:;
                break;
            }
            case "PushField":
            {
                var field = GetMember(commandArg) as FieldInfo;
                var target = stack.Pop();
                var @return = field.GetValue(target);
                stack.Push(@return);
                break;
            }
            case "PopField":
            {
                var value = stack.Pop();
                var target = stack.Pop();
                var field = GetMember(commandArg) as FieldInfo;
                field.SetValue(target, value);
                break;
            }
            case "GetProp":
            {
                var prop = GetMember(commandArg) as PropertyInfo;
                var @params = prop.GetIndexParameters().Select(_ => stack.Pop()).ToArray();
                var target = stack.Pop();
                var @return = prop.GetValue(target);
                stack.Push(@return);
                break;
            }
            case "SetProp":
            {
                var value = stack.Pop();
                var target = stack.Pop();
                var prop = GetMember(commandArg) as FieldInfo;
                prop.SetValue(target, value);
                break;
            }
            case "Call":
            {
                var method = GetMember(commandArg) as MethodInfo;
                var @params = method.GetParameters().Select(_ => stack.Pop()).ToArray();
                var target = stack.Pop();
                var @return = method.Invoke(target, @params);
                stack.Push(@return);
                break;
            }
            case "Drop":
            {
                stack.Pop();
                break;
            }
            case "PeekToGlobal":
            {
                global[commandArg] = stack.Pop();
                break;
            }
            case "PushFromGlobal":
            {
                stack.Push(global[commandArg]);
                break;
            }
            default :
            {
                throw new ArgumentException("Invalid Command");
            }
        }
    }

    static MemberInfo GetMember(string memberFullName)
    {
        var tokens = SplitToTokens(memberFullName, false);
        var typeName = tokens[0];
        var memberName = tokens[1];
        var option = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.CreateInstance;
        var members = Type.GetType(string.Join(".", typeName)).GetMember(memberName, option);
        
        if(tokens.Count <= 2)
        {
            return members.First();
        }
        else
        {
            var paramTypes = tokens.Skip(2).Select(name => Type.GetType(name));
            foreach (var member in members)
            {
                var method = member as MethodInfo;
                var _paramTypes = method.GetParameters().Select(inf => inf.ParameterType);
                if(_paramTypes.SequenceEqual(paramTypes))
                {
                    return member;
                }
            }
        }

        throw new ArgumentException("Invalid command arg");
    }

    static List<string> SplitToTokens(string memberFullName, bool isConstructor)
    {
        var separater = memberFullName.Contains("(") ? '(' : '[';

        var tokens = new List<string>();
        var split = memberFullName.Split(separater);

        if(isConstructor)
        {
            tokens.Add(split[0]);
            tokens.Add(".ctor");
        }
        else
        {
            tokens.Add(string.Join(",", split[0].Split(".").SkipLast(1)));
            tokens.Add(split[0].Split(".").Last());
        }

        if(split.Length > 1)
        {
            split[1].Replace(")", "").Replace("]", "");
            tokens.AddRange(split[1].Split(","));
        }
        return tokens;
    }
}
