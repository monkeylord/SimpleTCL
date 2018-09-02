//#define DEBUGLOG
using System;
using System.Collections.Generic;
using System.Linq;

class TCLite
{
    const bool Debug = false;
    const string versionstr = "Monkeylord's C# TCL interpreter v0.1 alpha";
    public Context context;
    public TCLite()
    {
        context = new Context();
        context.Buildins.Add("puts", puts);
        context.Buildins.Add("version", version);
        context.Buildins.Add("sum", sum);
        context.Buildins.Add("lindex", lindex);
        context.Buildins.Add("set", set);
        context.Buildins.Add("proc", proc);
#if DEBUGLOG
            Console.WriteLine(Evaluate("set version [version]\nputs $version"));
#endif
    }
    public bool Buildin(string name, Func<int, List<string>, Context, string> func)
    {
        if (context.Buildins.ContainsKey(name)) return false;
        context.Buildins.Add("set", func);
        return true;
    }
    public bool Buildin(string name, Func<int, List<string>, string> func)
    {
        return Buildin("set", (argc, argv, context) => func(argc, argv));
    }
    public class Context
    {
        public Dictionary<string, Func<int, List<string>, Context, string>> Buildins;
        public Dictionary<string, function> Functions;
        public Dictionary<string, string> Varibles;
        public Context()
        {
            Buildins = new Dictionary<string, Func<int, List<string>, Context, string>>();
            Varibles = new Dictionary<string, string>();
            Functions = new Dictionary<string, function>();
        }
        public string ErrorMsg = "";
        public int ErrorLevel = 0;
    };
    public struct function
    {
        public string name;
        public string param;
        public string body;
    }

    public string Evaluate(string command)
    {
        string rest = preParse(command);
        if (rest.Equals("")) return "";
#if DEBUGLOG
            Console.WriteLine("[Debug]Evaluating->"+rest);
#endif
        List<String> tokens = new List<string>();
        while (hasMoreToken(rest))
        {
            tokens.Add(getToken(rest.TrimStart(), out rest));
        }
        string result = Call(tokens);
        if (context.ErrorMsg.Equals(""))
        {
            return result + Evaluate(rest);
        }
        return context.ErrorMsg;
    }
    string preParse(string command)
    {
        //去除前空格和注释
        string rest = command.TrimStart();
        if (rest.Length > 0 && rest.First() == '#')
        {
            int eol = rest.IndexOf("\n");
            if (eol > 0)
            {
                rest = rest.Substring(eol);
            }
            else rest = "";
        }
        return rest;
    }
    bool hasMoreToken(string command)
    {
        if (command.TrimStart().Length == 0) return false;
#if DEBUGLOG
            Console.WriteLine("[Debug]hasMoreToken->"+command.TrimStart(' ').First());
#endif
        //遇到换行或注释则语句结束
        if (command.TrimStart(' ').IndexOfAny("\r\n#".ToCharArray()) == 0) return false;
        return true;
    }
    string getToken(string command, out string rest)
    {
        string token = "";
        switch (command.First())
        {
            case '[':
                token = Evaluate(getInside(command, '[', ']', out rest));
                break;
            case '{':
                token = getInside(command, '{', '}', out rest);
                break;
            case '\"':
                rest = command.Substring(command.IndexOf("\"", 1) + 1);
                token = TranslateVarible(command.Substring(1, command.IndexOf("\"", 1) - 2));
                break;
            default:
                if (command.IndexOfAny(" \0\r\n#".ToCharArray()) < 0)
                {
                    rest = "";
                    token = TranslateVarible(command);
                }
                else
                {
                    rest = command.Substring(command.IndexOfAny(" \0\r\n#".ToCharArray()));
                    token = TranslateVarible(command.Substring(0, command.IndexOfAny(" \0\r\n#".ToCharArray())));
                }
                break;
        }
#if DEBUGLOG
            Console.WriteLine("[Debug]Token->"+token);
#endif
        return token;
    }
    string getInside(string command, char start, char end, out string rest)
    {
        char[] chars = command.ToCharArray();
        int ptr = 1;
        int level = 1;
        rest = "";
        while (level > 0 && ptr < chars.Length)
        {
            if (chars[ptr] == start) level++;
            else if (chars[ptr] == end) level--;
            else if (chars[ptr] == '\0') return null;
            ptr++;
        }
        if (level > 0)
        {
            context.ErrorMsg = "parse error";
            return "";
        };
        rest = command.Substring(ptr);
        return command.Substring(1, ptr - 2);
    }
    string TranslateVarible(string token)
    {
        if (token.IndexOf("$") == -1) return token;
        string result = token;
#if DEBUGLOG
            Console.WriteLine("[Debug]Varibles:"+string.Join(",",context.Varibles.Keys));
#endif
        foreach (var varible in context.Varibles)
        {
            result = result.Replace("$" + varible.Key, varible.Value);
        }
        return result;
    }
    string Call(List<string> tokens)
    {
        if (tokens.Count == 0) return "";
        string command = tokens[0];
        tokens.RemoveAt(0);
#if DEBUGLOG
            Console.WriteLine("[Debug]Calling "+command+" Args:{"+String.Join(",",tokens)+"}");
#endif
        if (context.Buildins.ContainsKey(command)) return context.Buildins[command](tokens.Count, tokens, context);
        else if (context.Functions.ContainsKey(command))
        {
            //新建context并添加参数
            function func = context.Functions[command];
            string[] parms = func.param.Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (parms.Length > tokens.Count)
            {
                context.ErrorMsg = command + " takes " + parms.Length + " parameters";
                return "";
            }
            for (int i = 0; i < parms.Length; i++)
            {
                ScopeIn(parms[i]);
                context.Varibles.Add(parms[i], tokens[i]);
            }
            string result = Evaluate(func.body);
            foreach (var parm in parms)
            {
                ScopeOut(parm);
            }
            return result;
        }
        else
        {
            context.ErrorMsg = "No such function:" + command;
            return "";
        }
    }

    void ScopeIn(string varible)
    {
        if (context.Varibles.Keys.Contains(varible))
        {
            ScopeIn("_super." + varible);
            context.Varibles.Add("_super." + varible, context.Varibles[varible]);
            context.Varibles.Remove(varible);
        }
    }
    void ScopeOut(string varible)
    {
        context.Varibles.Remove(varible);
        if (context.Varibles.Keys.Contains("_super." + varible))
        {
            context.Varibles.Add(varible, context.Varibles["_super." + varible]);
            ScopeOut("_super." + varible);
        }
    }
    //Buildins Definations
    static string puts(int argc, List<String> argv, Context context)
    {
        if (argc > 0) Console.WriteLine(String.Join(" ", argv));
        else context.ErrorMsg = "puts <args>";
        return "";
    }
    static string version(int argc, List<String> argv, Context context)
    {
        return versionstr;
    }
    static string set(int argc, List<String> argv, Context context)
    {
        if (argc >= 2)
        {
            if (context.Varibles.ContainsKey(argv[0])) context.Varibles[argv[0]] = argv[1];
            else context.Varibles.Add(argv[0], argv[1]);
        }
        else context.ErrorMsg = "set name value";
        return "";
    }
    static string sum(int argc, List<String> argv, Context context)
    {
        if (argc > 0)
        {
            double sum = 0;
            foreach (var number in argv)
            {
                sum += Convert.ToDouble(number);
            }
            return sum.ToString();
        }
        else context.ErrorMsg = "sum <numbers>";
        return "";
    }
    static string lindex(int argc, List<String> argv, Context context)
    {
        if (argc >= 2)
        {
            return argv[0].Split(' ')[Convert.ToInt32(argv[1])];
        }
        else context.ErrorMsg = "lindex <List> index";
        return "";
    }
    static string proc(int argc, List<String> argv, Context context)
    {
        if (argc >= 3)
        {
            function func = new function();
            func.name = argv[0];
            func.param = argv[1];
            func.body = argv[2];
            context.Functions.Add(argv[0], func);
        }
        else context.ErrorMsg = "proc name args body";
        return "";
    }
}
