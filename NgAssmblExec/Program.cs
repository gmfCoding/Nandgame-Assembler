using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using static NgAssmblCore.NgasmContext;
using NgAssmblCore;
using System.Xml.Linq;

namespace NgAssmblExec
{
    class Program
    {

        public static string version =
@"Assembler version 0.1
Assembles: Nandgame-Assembly 202202 (YYYYMM)";

        public static bool TryHandleMacros(Arguments args, out Macros macros)
        {
            string macro_path = "";
            macros = null;

            if (args.TryValues(ref macro_path, "mf", "macro-file"))
            {
                if (File.Exists(macro_path))
                {
                    Console.Write($"Cannot find custom macro json configuration {macro_path}");
                    return false;
                }

                using (Stream macro_stream = File.OpenRead(macro_path))
                {
                    using (StreamReader sr = new StreamReader(macro_stream))
                    {
                        using (JsonTextReader jtr = new JsonTextReader(sr))
                        {
                            macros = JsonSerializer.Create().Deserialize<Macros>(jtr);
                        }
                    }
                }
            }
            else
                macros = Macros.GetDefault();
            return macros != null;
        }

        public static bool TryHandleSource(Arguments args, out string program)
        {
            string source = "";

            if (!args.TryValues(ref source, "c", "code"))
            {
                if (string.IsNullOrEmpty(args.GetAloneFromFront(0)))
                {
                    Console.WriteLine("No source loaded, please load source from a file or from -c");
                    program = null;
                    return false;
                }
                program = File.ReadAllText(args.GetAloneFromFront(0));
            }
            else
                program = source;
            return true;
        }

        static int Main(string[] arguments)
        {
            string program;
            ArgumentsDefinitions definitions = new ArgumentsDefinitions();
            definitions.DefineField("o", "output");
            definitions.DefineField("f", "format");
            definitions.DefineField("c", "code");
            definitions.DefineField("m", "mode");
            definitions.DefineField("t", "terminal");
            definitions.DefineField("e", "endian");
            definitions.DefineField("p", "print");
            definitions.DefineFlag("h", "help");
            definitions.DefineFlag("v", "version");
            definitions.DefineField("mf", "macro-file");
            definitions.DefineField("cmp", "comment-prefix");
            definitions.DefineField("sep", "separator");

            Arguments args = new Arguments(arguments_help, definitions);

            if (args.Initialise(arguments, true))
                return -1;

            if (arguments.Length == 0)
            {
                Console.WriteLine(arguments_help);
                return -1;
            }

            if (args.HasFlag("v"))
            {
                Console.WriteLine(version);
                return 0;
            }

            string outputFile = "ng_bin.txt";
            string outputFormat = "b";
            string printMode = "normal";
            string fullPrintFormats = "";
            string terminalFormat = "b";
            string source = "";
            string endianness = "b";
            string commentPrefix = "#";
            string separator = "";
            string _ = "";

            bool o_spec = args.TryValues(ref outputFile, "o", "output");
            bool f_spec = args.TryValues(ref outputFormat, "f", "format");
            bool m_spec = args.TryValues(ref printMode, "m", "mode");
            bool p_spec = args.TryValues(ref fullPrintFormats, "p", "print");
            bool t_spec = args.TryValues(ref terminalFormat, "t", "terminal");
            bool c_spec = args.TryValues(ref source, "c", "code");
            bool e_spec = args.TryValues(ref endianness, "e", "endian");
            bool cm_spec = args.TryValues(ref commentPrefix, "cmp", "comment-prefix");
            bool mf_spec = args.TryValues(ref _, "mf", "macro-file");
            bool sep_spec = args.TryValues(ref separator, "sep", "separator");

            if (c_spec && !m_spec)
                printMode = "none";

            NgasmContext.PrintMode printModes = NgasmContext.PrintMode.none;

            string[] printModesStr = printMode.Split(' ', ',');
            foreach (var item in printModesStr)
            {
                if (Enum.TryParse(item, out PrintMode printModeValue))
                    printModes |= ((PrintMode?)printModeValue).GetValueOrDefault();
                else
                { Console.WriteLine($"Unknown print mode: {item}"); Console.WriteLine(arguments_help); return -1; }
            }

            PrintFormat finalTerminalFormat = PrintFormat.None;
            if (!string.IsNullOrEmpty(terminalFormat))
                finalTerminalFormat |= Util.formatFromChar(terminalFormat[0]);

            PrintFormat finalPrintFormat = PrintFormat.None;
            if (!string.IsNullOrEmpty(fullPrintFormats))
                finalPrintFormat |= Util.formatFromChar(fullPrintFormats[0]);

            if (!TryHandleSource(args, out program))
                return -1;

            if (!TryHandleMacros(args, out Macros macros))
                return -1;

            NgasmContext context = new NgasmContext(program, new NgasmContextOptions()
            {
                littleEndian = Util.GetEndianMode(endianness[0]),
                commentPrefix = commentPrefix,
                separator = separator
            });

            context.Parse();
            context.Print(printModes, finalTerminalFormat);

            if (!c_spec || o_spec)
                context.Save(outputFile, finalPrintFormat);
            
            if (p_spec)
            {
                void AttemptPrint(string list, PrintFormat targetFormat, char type)
                {
                    if (!list.Contains(type) && !list.Contains(char.ToUpper(type)))
                        return;
                    if (list.Contains(char.ToUpper(type)))
                        targetFormat = targetFormat | PrintFormat.Prefix;
                    if (list.Contains("m"))
                            Console.WriteLine("Machine Code (binary)");
                    else
                        Console.WriteLine();
                    context.Print(PrintMode.opcode, targetFormat);
                }

                AttemptPrint(fullPrintFormats, PrintFormat.Binary, 'b');
                AttemptPrint(fullPrintFormats, PrintFormat.Hex, 'h');
                AttemptPrint(fullPrintFormats, PrintFormat.Dec, 'd');
            }
            return 1;
        }

        public static string arguments_help =
@"
Usage:

    ngassmbl.exe <arguments> <input file>

Format:

    Don't use braces in command.
    [...] means select one, the first option is default, ie [xyz] x is default.
    (...) means select multiple, the first option is default.
    <...> Generic argument, read description, use double quotes if whitespaces.
    <list> list items can be separated by ',' and or 'spaces' if args is quoted.

Help:

    {0}
    -o output <output file> 
        The machine code output destination.
        defaults to ng_bin.txt

    -f format [bdhr] 
        output file format
        b:ascii binary
        d:ascii decimal
        h:ascii hex
        r:raw data

    -c code <Nandgame assembly code>
        Load Nandgame assembly from command line.
        Make use of -p to select a format, default is binary.
        NOTE: Not outputed to file unless directly specified with -o.

    -m mode <list> 
        Specify the printing mode/s:
        This applies to terminal output preview and not file output.
        list values:
        none, line, comment, opcode, source, comma, lineeach, errors, label_def, full_source, normal
        normal = (opcode,source,comment,lineeach,errors,label_def)

    -t terminal [bdh]
        Specifies the format options for in-terminal machine code preview.
        b:ascii binary (default)
        d:ascii decimal
        h:ascii hex

    -e endian [bsl]
        Specifies the output endianness
        b:big endian (default)
        s:system endianness
        l:little endian

    -p print (bdhm)
        Print the all the opcode
        b:ascii binary (default)
        B:ascii binary (default)
        d:ascii decimal
        h:ascii hex
        H:ascii hex

        m:Print ""Machine Code(type)"" before the opcodes

    -cmp comment-prefix <string>
        The prefix string to use for comment lines (used with -m source)

    -cma comma <string>
        Output with commas

    -mf macro-file <path>
        Supplies a custom macro expansion json config file (stack operations)

    -v version
        Print assembler the version.

Examples:

    ngassmbl.exe -o output.bin -f r example.ngasm
        Loads the source code from example.ngasm and outputs machine code to output.bin in a raw format (non-human readable).

    ngassmbl.exe -f h example.ngasm
        Loads the source code from example.ngasm and outputs machine code to ng_bin.txt in a ascii hex format (human 'readable').

    ngassmbl.exe -f b example.ngasm
        Loads the source code from example.ngasm and outputs machine code to ng_bin.txt in a ascii hex format (human 'readable').

    ngassmbl.exe -c ""A = 1""
        Compiles code passed to -c and outputs machine code to terminal in a ascii hex format (human 'readable').

";
    }
}