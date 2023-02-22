using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NandgameASM2MC.NgasmLine;

namespace NandgameASM2MC
{
    public class NgasmContext
    {
        [Flags]
        public enum PrintFormat { None, Binary = 1, Dec = 2, Hex = 4 };

        [Flags]
        public enum PrintMode { none = 0, line = 1, comment = 2, opcode = 4, source = 8, lineeach = 16, errors = 32, label_def = 64, full_source = 128,  normal = opcode | source | comment | lineeach | errors | label_def}
        PrintFormat format = PrintFormat.Hex;

        public static class Util
        {
            public static bool Has(dynamic bits, dynamic flag)
            {
                return (bits & flag) == flag;
            }

            public static bool GetEndianMode(char c)
            {
                switch (c)
                {
                    default:
                    case 'S':
                    case 's':
                        return BitConverter.IsLittleEndian;
                    case 'L':
                    case 'l':
                        return true;
                    case 'B':
                    case 'b':
                        return false;
                }
            }

            public static byte[] GetEndianBytes(byte[] data, bool isLittle)
            {
                if (isLittle)
                    return GetLittleEndian(data);
                return GetBigEndian(data);
            }

            public static byte[] GetBigEndian(byte[] data)
            {
                if (BitConverter.IsLittleEndian)
                    return data.Reverse().ToArray();
                return data;
            }

            public static byte[] GetLittleEndian(byte[] data)
            {
                if (!BitConverter.IsLittleEndian)
                    return data.Reverse().ToArray();
                return data;
            }

            public static PrintFormat formatFromChar(char c)
            { 
                switch (c)
                {
                    default:
                    case 'B':
                    case 'b':
                        return NgasmContext.PrintFormat.Binary;
                    case 'D':
                    case 'd':
                        return NgasmContext.PrintFormat.Dec;
                    case 'H':
                    case 'h':
                        return NgasmContext.PrintFormat.Hex;
                    case 'R':
                    case 'r':
                    case 'N':
                    case 'n':
                        return PrintFormat.None;
                }
            }
    }

        public Dictionary<string, string> defined_definitions = new();
        public Dictionary<string, ushort> defined_labels = new();

        public HashSet<string> definitions = new();
        public HashSet<string> labels = new();

        public List<NgasmLine> lines = new();
        public List<NgasmLine> late_lines = new();
        public List<string> errors = new();
        public string[] src_lines;

        public ushort instruction_address;

        public bool isLittleEndian;

        public NgasmContext(string code, bool isLittleEndian)
        {
            this.isLittleEndian = isLittleEndian;
            src_lines = code.Split("\r\n");
        }

        public string GetOutput(ushort code)
        {
            byte[] bytes = Util.GetEndianBytes(BitConverter.GetBytes(code), isLittleEndian);
            if (Util.Has(format, PrintFormat.Binary))
            {
                string bytesstr = "";
                foreach (var item in bytes)
                    bytesstr += Convert.ToString(item, 2).PadLeft(8, '0');
                return bytesstr.PadLeft(16, '0');
            }
            else if (Util.Has(format, PrintFormat.Hex))
            {
                return Convert.ToHexString(bytes);
            }
            else
            {
                return code.ToString();
            }
        }

        void Write(string str, ConsoleColor color, bool nl)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            if (nl)
                Console.WriteLine(str);
            else
                Console.Write(str);
            Console.ForegroundColor = previous;
        }

        public void Parse()
        {
            Preprocessor();

            for (int i = 0; i < src_lines.Length; i++)
            {
                string line = src_lines[i];
                if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                    continue;
                var ngl = new NgasmLine();
                ngl.executor = ngl.ParseLine(this, i, src_lines);
                ngl.executor.MoveNext();
                ngl.error = ngl.executor.Current;
                lines.Add(ngl);
                if (ngl.isInstruction)
                    instruction_address++;
            }

            foreach (var item in lines)
            {
                if (item.error == OPResult.Postprocess)
                    item.executor.MoveNext();
            }
        }
        public void Preprocessor()
        {
            foreach (var srcline in src_lines)
            {
                string line = Preprocessing.FilteredWhitespaces(srcline);

                if (line.Contains('#'))
                    continue;
                if (line.StartsWith("DEFINE"))
                {
                    string[] split = line.Split(' ');
                    if (split.Length == 1)
                        return;
                    if (split.Length == 2)
                        return;
                    definitions.Add(split[1]);
                }
                else if (line.StartsWith("LABEL") || line.Contains(':'))
                {
                    Preprocessing.GetLabelInfo(line, out string label, out string[] split);
                    labels.Add(label);
                }
            }
        }

        public void Print(PrintMode printMode, PrintFormat? opFormat = null)
        {
            PrintFormat previous = this.format;
            this.format = opFormat.GetValueOrDefault(this.format);

            if(Util.Has(printMode, PrintMode.full_source))
            {
                foreach (var item in src_lines)
                {
                    Write($"{item}", ConsoleColor.Blue, true);
                }
            }

            if (Util.Has(printMode, PrintMode.label_def))
            {  
                Write($"Constant Definitions:", ConsoleColor.Magenta, true);
                foreach (var define in definitions)
                {
                    if (defined_definitions.ContainsKey(define))
                        Write($"{define} = {defined_definitions[define]}", ConsoleColor.Cyan, true);
                    else
                        Write($"{define} = NULL", ConsoleColor.Red, true);
                }

                Write($"Label Definitions:", ConsoleColor.Magenta, true);
                foreach (var label in labels)
                {
                    if (defined_labels.ContainsKey(label))
                        Write($"{label} = {defined_labels[label]}", ConsoleColor.Cyan, true);
                    else
                        Write($"{label} = NULL", ConsoleColor.Red, true);
                }
            }

            foreach (var line in lines)
            {
                if (Util.Has(printMode, PrintMode.line))
                    Write($"{line.lineNumber} :", ConsoleColor.Yellow, true);
                if (Util.Has(printMode, PrintMode.source))
                    Write($"{(!line.comment ? "# " : "#")}{line.linews}", ConsoleColor.Green, true);
                if (line.isInstruction && line.isValid && Util.Has(printMode, PrintMode.opcode))
                    Write(GetOutput(line.code), ConsoleColor.White, true);
                else if (line.isInstruction && Util.Has(printMode, PrintMode.errors))
                    Write($"{GetOutput(line.code)} : {line.linews}", ConsoleColor.Red, true);
            }

            if (Util.Has(printMode, PrintMode.errors))
            { 
                foreach (var error in errors)
                    Write(error, ConsoleColor.Red, true);
            }
            format = previous;
        }


        public void Save(string filepath, PrintFormat opFormat)
        {
            PrintFormat previous = this.format;
            this.format = opFormat;

            List<ushort> codes = new();
            foreach (var line in lines)
            {
                if (!line.isInstruction && !line.isValid)
                { Write("Could not save to file because of compilation error!", ConsoleColor.Red, true); return; }
                codes.Add(line.code);
            }
            if (opFormat == PrintFormat.None)
            {
                BinaryWriter bw = new BinaryWriter(File.Open(filepath, FileMode.Create, FileAccess.Write));
                foreach (var data in codes)
                    bw.Write(Util.GetEndianBytes(BitConverter.GetBytes(data), isLittleEndian));
                bw.Dispose();
            }
            else
            {
                TextWriter tw = new StreamWriter(File.Open(filepath, FileMode.Create, FileAccess.Write));
                foreach (var data in codes)
                    tw.WriteLine(GetOutput(data));
                tw.Dispose();
            }
            format = previous;
        }

        public void Error(string str, int line, bool custom = false)
        {
            string customstr = custom ? string.Empty : ": " + src_lines[line - 1];
            errors.Add($"Error at {line}: {str} {customstr}");
        }

        public bool Assert(string str, int line, bool condition, bool custom = false)
        {
            if (!condition)
                return condition;
            Error(str, line, custom);
            return true;
        }
    }
}
