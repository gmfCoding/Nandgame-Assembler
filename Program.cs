using System;
using System.Linq;
using System.IO;

namespace NandgameASM2MC
{
    class Program
    {

        public static string version = 
@"Assembler version 0.1
Assembles: Nandgame-Assembly 202202 (YYYYMM)";
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

            bool o_spec = args.TryValues(ref outputFile      , "o", "output");
            bool f_spec = args.TryValues(ref outputFormat    , "f", "format");
            bool m_spec = args.TryValues(ref printMode       , "m", "mode");
            bool p_spec = args.TryValues(ref fullPrintFormats, "p", "print");
            bool t_spec = args.TryValues(ref terminalFormat  , "t", "terminal");
            bool c_spec = args.TryValues(ref source          , "c", "code");
            bool e_spec = args.TryValues(ref endianness      , "e", "endian");
            if (c_spec && !m_spec)
                printMode = "none";

            NgasmContext.PrintMode printModes = NgasmContext.PrintMode.none;

            string[] printModesStr = printMode.Split(' ', ',');
            foreach (var item in printModesStr)
            {
                if (Enum.TryParse(typeof(NgasmContext.PrintMode), item, out object? printModeValue))
                    printModes |= ((NgasmContext.PrintMode?)printModeValue).GetValueOrDefault();
                else
                { Console.WriteLine($"Unknown print mode: {item}"); Console.WriteLine(arguments_help); return -1; }
            }
            NgasmContext.PrintFormat t_printFormat = NgasmContext.PrintFormat.None;

            foreach (char item in terminalFormat)
                t_printFormat |= NgasmContext.Util.formatFromChar(terminalFormat[0]);

            NgasmContext.PrintFormat o_PrintFormat = NgasmContext.PrintFormat.None;
            foreach (char item in outputFormat)
                o_PrintFormat |= NgasmContext.Util.formatFromChar(terminalFormat[0]);

            if (!c_spec)
            {
                if (string.IsNullOrEmpty(args.GetAloneFromFront(0)))
                {
                    Console.WriteLine("No source loaded, please load source from a file or from -c");
                    return -1;
                }
                program = File.ReadAllText(args.GetAloneFromFront(0));
            }
            else
                program = source;

            NgasmContext context = new NgasmContext(program, NgasmContext.Util.GetEndianMode(endianness[0]));
            context.Parse();
            context.Print(printModes, t_printFormat);

            if (!c_spec || o_spec)
                context.Save(outputFile, o_PrintFormat);

            if (p_spec)
            {
                if (fullPrintFormats.Contains("b"))
                {
                    if (fullPrintFormats.Contains("m"))
                        Console.WriteLine("Machine Code (binary)");
                    else
                        Console.WriteLine();
                    context.Print(NgasmContext.PrintMode.opcode, NgasmContext.PrintFormat.Binary);
                }
                if (fullPrintFormats.Contains("h"))
                {
                    if (fullPrintFormats.Contains("m"))
                        Console.WriteLine("Machine Code (Hex):");
                    else
                        Console.WriteLine();
                    context.Print(NgasmContext.PrintMode.opcode, NgasmContext.PrintFormat.Hex);
                }
                if (fullPrintFormats.Contains("d"))
                {
                    if (fullPrintFormats.Contains("m"))
                        Console.WriteLine("Machine Code (decimal)");
                    else
                        Console.WriteLine();
                    context.Print(NgasmContext.PrintMode.opcode, NgasmContext.PrintFormat.Dec);
                }
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
        none, line, comment, opcode, source, lineeach, errors, label_def, full_source, normal
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
        d:ascii decimal
        h:ascii hex
        m:Print ""Machine Code(type)"" before the opcodes

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