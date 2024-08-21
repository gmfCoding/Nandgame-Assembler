using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NgAssmblCore
{
    public class Line
    {
        public string line;
        public int depth;
        public int number;
        public bool processMacro;
        public bool constants;

        public bool label;

        public List<Line> children;

        public Line(string line, int number, int depth, Macros macros)
        {
            this.depth = depth;
            this.line = line;
            this.number = number;
            processMacro = macros.ContainsMacro(line);
            constants = macros.ContainsConstant(line);
        }
    };

    public class Preprocessor
    {
        Macros macros;

        public class Options
        {
            public readonly bool expandSharedConstants = true;
            public readonly bool errorOnMax = true;
            public readonly int maxDepth = Preprocessor.recommendedMaxDepth;
        }

        public const int recommendedMaxDepth = 10;

        Options opt = new Options();

        public Preprocessor(Macros macros, Options options = null)
        {
            this.macros = macros;
        }

        public bool ContainsMacros(List<string> source)
        {
            bool contains = false;
            Parallel.For(0, source.Count(), (i, s) =>
            {
                if (!macros.ContainsMacro(source[i]))
                    return;
                contains = true;
                s.Stop();
            });
            return contains;
        }

        /// <summary>
        /// Expands all macros
        /// <br></br>
        /// Mostly for internal work, you probably want to use ProcessSource.
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="output">All macros expanded, first item is first line</param>
        /// <param name="consts">List of lines that need to have constants evaluated</param>
        /// <returns></returns>
        private bool ProcessMacros(Stack<Line> lines, ref Stack<Line> output, ref string error)
        {
            while (lines.Count() > 0)
            {
                Line item = lines.Pop();

                if (item.processMacro)
                {
                    if (!ProcessLine(item, out item.children, out error))
                        return false;
                    foreach (var child in item.children)
                    {
                        if (child.depth >= opt.maxDepth)
                        {
                            if (opt.errorOnMax)
                            {
                                error = $"Exceeded set maximum macro recusion depth of {opt.maxDepth}";
                                return false;
                            }
                            child.processMacro = false;
                        }
                        lines.Push(child);
                    }
                }
                else
                    output.Push(item);
            }
            return true;
        }


        //public bool ProcessSymbols(List<string> source, out List<string> labels, out List<string> definitions, out string error)
        //{
        //    labels = new List<string>();
        //    definitions = new List<string>();

        //    foreach (var src_line in source)
        //    {
        //        string line = Preprocessing.FilteredWhitespaces(src_line);

        //        if (line.Contains('#'))
        //            continue;
        //        if (line.StartsWith("DEFINE"))
        //        {
        //            string[] split = line.Split(' ');
        //            if (split.Length != 3)
        //            {
        //                error = $"DEFINE has too many arguments expected 2 got {split.Length}";
        //                return false;
        //            }
        //            definitions.Add(split[1]);
        //        }
        //        else if (line.StartsWith("LABEL") || line.Contains(':'))
        //        {
        //            Preprocessing.GetLabelInfo(line, out string label, out string[] split);
        //            labels.Add(label);
        //        }
        //    }
        //}

        /// <summary>
        /// Expands all macros within source
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="output">true: The final output</param>
        /// <param name="error">false: The error string </param>
        /// <returns>Returns true if it was sucessful or false if there was an error.</returns>
        public bool ProcessSource(List<string> source, out List<Line> output, out string error)
        {
            output = null;
            error = null;

            Stack<Line> leafs = new Stack<Line>();
            Stack<Line> stack = new Stack<Line>();

            for (int i = 0; i < source.Count(); i++)
            {
                Line line = new Line(source[i], i, 0, macros);
                stack.Push(line);
            }

            if (!ProcessMacros(stack, ref leafs, ref error))
                return false;

            output = leafs.Select(x => x).ToList();
            return true;
        }

        public bool ProcessLine(Line line, out List<Line> output, out string error)
        {
            output = new List<Line>();
            error = null;

            if (!macros.ContainsMacro(line.line))
                return true;

            MacroInstancer instancer = null;
            if (macros.GetMacroInstancer(line.line, out instancer))
            {
                string linews = Preprocessing.FilteredWhitespaces(line.line);
                List<string> stack = new List<string>();
                string[] arguments = linews.Split(' ');
                for (int i = 1; i < arguments.Length; i++)
                    stack.Add(arguments[i]);
                if (!instancer.Generate(stack, line, out List<Line> children, /*out _,*/ out error))
                    return false;
                output.AddRange(children);
            }
            return true;
        }
    }
}
