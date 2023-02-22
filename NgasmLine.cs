using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NandgameASM2MC
{
    public class NgasmLine
    {
        public enum OPType { None, Math, Bitwise }

        [Flags]
        public enum OPResult { None = 0, Finished = 1, Error = 2, NotUsed = 4, BadFormat = 8, Postprocess = 16 };
        public enum ParseStage { Begin, Assignment, Expression, Jump };

        public int lineNumber = 0;

        public string line = "";
        public string linews = "";
        public string linenws = "";
        public ushort code;
        public bool comment;
        public bool jump;

        public bool assignment;
        public bool assignValue;

        public bool isDefinition;
        public bool isLabel;
        public bool isInstruction;
        public bool isValid;

        public OPType opType;
        public OPResult error;
        public IEnumerator <OPResult> executor;
        public ParseStage stage;

        public bool rhs_ptr;
        public string[] split;

        public string rhs = "";
        public string lhs = "";
        public string jmp = "";

        public IEnumerator<OPResult> ParseLine(NgasmContext context, int index, string[] lines)
        {
            bool Assert(string s, int i, bool c, bool cu = false) => context.Assert(s, i, c, cu);
            bool Attempt(ref bool attempt, bool cond)
            {
                if (!attempt && cond)
                {
                    attempt = true;
                    return true;
                }
                return false;
            }

            string[] line_data;
            int data_count;

            lineNumber = index + 1;
            line = lines[index];
            linews = Preprocessing.FilteredWhitespaces(line);
            linenws = Preprocessing.RemoveWhitespaces(line);
            stage = ParseStage.Begin;

            if (line.Contains('#'))
            { isValid = comment = true; yield return OPResult.None; }
            int labelMode = Preprocessing.GetLabelMode(linews);
            if (line.StartsWith("DEFINE"))
            {
                isDefinition = true;
                split = linews.Split(' ');
                if (Assert("Definition doesn't specify a name and value.", lineNumber, split.Length == 1))
                    yield return OPResult.Error;
                if (Assert($"Invalid Definition format of {split[1]}.", lineNumber, split.Length == 2))
                    yield return OPResult.Error;
                if (Assert($"Redefinition of {split[1]}.", lineNumber, context.defined_definitions.ContainsKey(split[1])))
                    yield return OPResult.Error;
                isValid = true;
                context.defined_definitions.Add(split[1], split[2]);
                yield return OPResult.Finished;
            }
            else if (labelMode > 0)
            {
                isLabel = true;
                Preprocessing.GetLabelInfo(linews, out string label, out split);
                if (Assert("Label doesn't specify a name", lineNumber, string.IsNullOrEmpty(label)))
                    yield return OPResult.Error;
                if (Assert($"Redefinition of Label {split[1]}.", lineNumber, context.defined_labels.ContainsKey(label)))
                    yield return OPResult.Error;
                context.defined_labels.Add(label, context.instruction_address);
                isValid = true;
                yield return OPResult.Finished;
            }

            line_data = linenws.Split('=', ';'); 
            data_count = line_data.Count();
            if (data_count == 0)
                yield return OPResult.NotUsed;
            lhs = line_data[0];

            if (line.Contains('='))
            {
                isInstruction = true;
                stage = ParseStage.Assignment;
                if (data_count < 2)
                    yield return OPResult.BadFormat;
                assignment = true;
                rhs = line_data[1];

                string[] destinations = lhs.Split(',', ' ').Where(x => !string.IsNullOrEmpty(x) && !string.IsNullOrWhiteSpace(x)).ToArray();
                foreach (var item in destinations)
                {
                    if (Assert($"Assignment to unknown Register ({item})", lineNumber, !Tokens.destByteMap.ContainsKey(item)))
                        yield return OPResult.BadFormat | OPResult.Error;
                    code |= Tokens.destByteMap[item];
                }

                stage = ParseStage.Expression;

                if (destinations.Length == 1 && destinations[0] == "A")
                {
                    bool attempt = ushort.TryParse(rhs, out ushort number);
                    if (!attempt && rhs.Contains('x'))
                        attempt = ushort.TryParse(rhs.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out number);

                    if (Attempt(ref attempt, context.labels.Contains(rhs)))
                        yield return OPResult.Postprocess;
                    if (Attempt(ref attempt, context.defined_definitions.ContainsKey(rhs)))
                    {
                        attempt = ushort.TryParse(context.defined_definitions[rhs], out number);
                        if (!attempt)
                            attempt = ushort.TryParse(context.defined_definitions[rhs].Substring(2), System.Globalization.NumberStyles.HexNumber, null, out number);
                    }

                    if (Attempt(ref attempt, context.defined_labels.ContainsKey(rhs)))
                        number = context.defined_labels[rhs];

                    if (attempt)
                    {
                        code = number;
                        assignValue = true;
                        isValid = true;
                        yield return OPResult.Finished;
                    }
                }

                // The assignment check for a single value did not pass, therefore we must certaintly be an full expression instruction;
                code |= Tokens.INSTRUCTION_CODE;
                rhs_ptr = rhs.Contains('*');
                if (rhs_ptr)
                    code |= Tokens.A_PTR_EXP_CODE;

                string frhs = new string(line_data[1].ToCharArray().Where(c => c != '*').ToArray()).Trim();
                if (Assert($"Unknown expression {line_data[1]}", lineNumber, !Tokens.byteMap.ContainsKey(frhs)))
                    yield return OPResult.BadFormat;
                code |= Tokens.byteMap[frhs];
            }
            stage = ParseStage.Jump;
            if (line.Contains(';'))
            {
                jump = isInstruction = true;
                if (data_count < 2)
                    yield return OPResult.BadFormat;
                if (data_count == 3)
                { jmp = line_data[2]; rhs = line_data[1]; }
                else
                { rhs = line_data[0]; jmp = line_data[1]; }

                if (Tokens.byteMap.ContainsKey(rhs))
                    code |= Tokens.byteMap[rhs];
                if (Tokens.byteMap.ContainsKey(jmp))
                    code |= Tokens.byteMap[jmp];
                else
                    yield return OPResult.BadFormat;
            }
            else if (line.Trim() == "JMP")
            { jump = isInstruction = true; code |= Tokens.JUMP_CODE; }
            isValid = true;
            yield return OPResult.Finished;
        }
    }
}
