using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NandgameASM2MC
{
    public class ArgumentsDefinitions
    {
        HashSet<int> usedIDs = new();

        HashSet<string> definedFields = new();
        Dictionary<int, string> idFieldMap = new();
        Dictionary<string, int> fieldIDMap = new();

        HashSet<string> definedFlags = new();
        Dictionary<int, string> idFlagMap = new();
        Dictionary<string, int> flagIDMap = new();

        public ArgumentsDefinitions()
        {
            usedIDs.Add(0);
        }

        public void DefineBuiltIn()
        {
            DefineFlag("arg-format", "arg-format");
            DefineFlag("h", "help");
        }

        public int DefineField(params string[] args)
        {
            bool inserted = false;
            int id = GetUniqueId();
            foreach (var item in args)
            {
                fieldIDMap.Add(item, id);
                if(!inserted)
                    idFieldMap.Add(id, item);
                definedFields.Add(item);
                inserted = true;
            }
            return id;
        }

        public int DefineFlag(params string[] args)
        {
            bool inserted = false;
            int id = GetUniqueId();
            foreach (var item in args)
            {
                flagIDMap.Add(item, id);
                if(!inserted)
                    idFlagMap.Add(id, item);
                definedFlags.Add(item);
                inserted = true;
            }
            return id;
        }

        public int GetUniqueId()
        {
            Random rand = new Random();
            int value = rand.Next();
            while (usedIDs.Contains(value))
                value = rand.Next();
            usedIDs.Add(value);
            return value;
        }

        public int GetID(string key)
        {
            if (IsField(key))
                return fieldIDMap[key];
            if (IsFlag(key))
                return flagIDMap[key];
            return 0;
        }

        public bool IsFlag(string str)
        {
            return definedFlags.Contains(str);
        }

        public bool IsField(string str)
        {
            return definedFields.Contains(str);
        }
    }

    /// <summary>
    /// Utility class from managing command line arguments
    /// 
    /// There are four types of arguments:
    /// --flag:   This is an argument that when specified, it changes the behaviour of the program just by it's existence.
    /// --field:  This is an argument that when specified, it changes the behaviour of the program depending on the next argument.
    /// "value":  This arguments comes after a field argument.
    /// "standalone": This argument is a random argument that is not apart of a field, usually used for filepaths.
    /// 
    /// The field and flag arguments must begin with - or -- so the parser knows the it's not a value argument, 
    /// otherwise it will be interpreted as a standlaone argument.
    /// 
    /// Arguments containing whitespace  must be double or single qouted, 
    /// otherwise they may be split across multiple arguments, becoming essentially useless.
    /// 
    /// Argument redefinitions overwrite previous definitions!
    /// </summary>
    public class Arguments
    {
        public string[] args;
        ArgumentsDefinitions definitions;

        List<string> standalone = new();

        Dictionary<int, string> foundFields = new();
        HashSet<int> foundFlags = new();

        public string help;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="help">The help string to print out when --help is passed.
        /// <br></br>Please include format {0} and {1} somewhere in your string.
        /// <br></br>{0} is for inserting the help strings for built-in arguments
        /// <br></br>{1} is for inserting the --arg-format help string
        /// </param>
        /// <param name="definitions"></param>
        public Arguments(string help, ArgumentsDefinitions definitions)
        {
            this.definitions = definitions;
            this.help = string.Format(help, BuiltInHelpString, CLIArgFormat);
        }

        /// <summary>
        /// Return true if initialise failed!
        /// It's highly recommended that you exit the program accordingly.
        /// This is nessecesary because if a built-in argument is than that argument may have unforseen consequences.
        /// </summary>
        /// <param name="runBuiltIns">If true you should not allow your program to continue if the return value is true</param>
        /// <returns></returns>
        public bool Initialise(string[] arguments, bool runBuiltIns)
        {
            this.args = arguments;
            string currentArg = "";
            bool isField = false;
            foreach (var arg in arguments)
            {
                if (isField)
                {
                    if (!foundFields.ContainsKey(definitions.GetID(currentArg)))
                        foundFields.Add(definitions.GetID(currentArg), arg);
                    else
                        foundFields[definitions.GetID(currentArg)] = arg;
                    isField = false;
                    continue;
                }
                int isFieldFlag = arg.StartsWith("--") ? 2 : arg.StartsWith("-") ? 1 : 0;
                if (isFieldFlag != 0)
                {
                    currentArg = arg.Substring(isFieldFlag);
                    if (definitions.IsFlag(currentArg))
                    { foundFlags.Add(definitions.GetID(currentArg)); continue; }
                    else if (definitions.IsField(currentArg))
                        isField = true;
                }
                else
                {
                    standalone.Add(arg);
                }
            }

            if (!runBuiltIns)
                return false;
            if (HasFlag("arg-format"))
            {
                Console.WriteLine(CLIArgFormat);
                return true;
            }
            if (HasFlag("help"))
            {
                Console.WriteLine(help);
                return true;
            }
            return false;
        }

        public string GetAloneFromBack(int index)
        {
            int i = Math.Max(0, (standalone.Count() - index) - 1);
            return standalone[i];
        }

        public string GetAloneFromFront(int index)
        {
            int i = Math.Min(Math.Max(0, index), standalone.Count() - 1);
            return standalone[i];
        }

        public bool TryGetValue<T>(string key, out T val)
        {
            val = default(T);
            return TryValue<T>(key, ref val);
        }

        public bool TryValues<T>(ref T val, params string[] keys)
        {
            foreach (var item in keys)
            {
                if (TryValue(item, ref val))
                    return true;
            }
            return false;
        }

        public bool TryValue<T>(string key, ref T val)
        {
            if (!HasField(key))
                return false;
            val = (T)Convert.ChangeType(foundFields[definitions.GetID(key)], typeof(T));
            return true;
        }

        public bool HasAny(params string[] keys)
        {
            return HasField(keys) || HasFlag(keys);
        }

        public bool HasFlag(params string[] keys)
        {
            foreach (var item in keys)
            {
                if (definitions.IsFlag(item) && foundFlags.Contains(definitions.GetID(item)))
                    return true;
            }
            return false;
        }

        public bool HasField(params string[] keys)
        {
            foreach (var item in keys)
            {
                if (definitions.IsField(item) && foundFields.ContainsKey(definitions.GetID(item)))
                    return true;
            }
            return false;
        }


        public static string BuiltInHelpString = 
@"-h help
        prints this help message.

    -arg-format
        prnts the formatting help for arguments
";

        public static string CLIArgFormat = @"
Argument format:

There are four types of arguments:
--flag:   This is an argument that when specified, it changes the behaviour of the program just by it's existence.
--field:  This is an argument that when specified, it changes the behaviour of the program depending on the next argument.
""value"":  This arguments comes after a field argument.
""standalone"": This argument is a random argument that is not apart of a field, usually used for filepaths.

The field and flag arguments must begin with - or -- so the parser knows the it's not a value argument, 
otherwise it will be interpreted as a standlaone argument.

Arguments containing whitespace  must be double or single qouted, 
otherwise they may be split across multiple arguments, becoming essentially useless.

Argument redefinitions overwrite previous definitions!
";
    }
}
