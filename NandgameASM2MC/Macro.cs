using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace NgAssmblCore
{
    public class MacroDefinition
	{
		public string[] placeholders;
		public string[] labels;
		public string code;

		/// <summary>
		/// Does the `code` contain any more macros?
		/// True: Code contains no more macros
		/// False: Code contains more macros
		/// </summary>
		public bool leaf;
		public bool constants;
	}

	public class Macros
	{
		[JsonProperty("Constants")]
		public Dictionary<string, string> constants;

		[JsonProperty("Macros")]
		public Dictionary<string, MacroDefinition> macros;

		public Dictionary<string, MacroInstancer> instancers = new Dictionary<string, MacroInstancer>();

		public bool ContainsMacro(string line)
		{
			line = Preprocessing.FilteredWhitespaces(line);
			string[] values = line.Split(' ');
			if (values.Length > 0)
				return macros.ContainsKey(values[0]);
			return false;
		}
	
		public bool ContainsConstant(string line)
		{
			line = Preprocessing.FilteredWhitespaces(line);
			string[] values = line.Split(' ');
			foreach (var item in values)
				if (constants.ContainsKey(item))
					return true;
			return false;
		}

		public bool GetMacroInstancer(string line, out MacroInstancer macro)
		{
			macro = null;
			line = Preprocessing.FilteredWhitespaces(line);
			string[] values = line.Split(' ');
			if (values.Length > 0)
				if (instancers.TryGetValue(values[0], out macro))
					return macro != null;
			return false;
		}

		public bool GetConstant(string linews, out string value)
		{
			value = null;
			string[] values = linews.Split(' ');
			foreach (var item in values)
				if (constants.TryGetValue(item, out value))
					return value != null;
			return false;
		}

		public void GenerateInstancers()
		{
			foreach (var item in macros)
			{
				instancers.Add(item.Key, new MacroInstancer(item.Value, this));
			}
		}

		public static Macros GetDefault()
		{
            Stream macro_stream = Assembly.GetAssembly(typeof(Macros)).GetManifestResourceStream("NgAssmblCore.macros.json");
			Macros macros;

            if (macro_stream == null)
            {
                Console.WriteLine("Cannot load macro definitions");
                return null;
            }

            using (StreamReader sr = new StreamReader(macro_stream))
            {
                using (JsonTextReader jtr = new JsonTextReader(sr))
                {
                    macros = JsonSerializer.Create().Deserialize<Macros>(jtr);
                }
            }
            macro_stream.Dispose();
			return macros;
        }
	}

    public class MacroInstancer
	{
		/// <summary> The assembly code implementation of the macro.</summary>
		public string[] code;

		public List<string> placeholders_order;

		Dictionary<string, string> constants;

        public HashSet<string> labels;
		public static HashSet<string> usedSalts = new HashSet<string>();

		MacroDefinition definition;
		Macros macros;


		public class Options
		{
			public string uniquePrefix = null;
			public bool expandConstants = true;
		}

		public MacroInstancer(MacroDefinition definition, Macros macros)
		{
			this.definition = definition;
			this.macros = macros;

			constants = macros.constants;
            if (definition.labels != null)
				labels = new HashSet<string>(definition.labels);
			if (definition.placeholders != null)
				placeholders_order = new List<string>(definition.placeholders);
			//string preLabel = definition.code;
			//if (labels != null)
			//	foreach (var item in labels)
			//	{
			//		preLabel = preLabel.Replace(item, GetUniqueSalt());
			//	}
			code = Preprocessing.SplitNewLines(definition.code);
            if (placeholders_order == null)
                placeholders_order = new List<string>();
        }

		private static Random random = new Random();

		public string GetUniqueSalt(int length = 5)
		{
			const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			string value = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
			usedSalts.Add(value);
			return "SP"+value;
		}

		/// <summary>
		/// Returns code to which replaces a line with to with what to override 
		/// </summary>
		/// <param name="placeholders"></param>
		/// <returns>Code to exapand/replace the macro </returns>
		/// 

		public bool Generate(List<string> arguments, Line parent, out List<Line> output, /*out Dictionary<string, List<Line>> labelLines, // 08*/ out string errors, Options opt)
		{
			Dictionary<string, string> placeholders = new Dictionary<string, string>();
            string unique = GetUniqueSalt();

			//labelLines = new Dictionary<string, List<Line>>(); // 08
			output = new List<Line>(code.Length);
            errors = null;

            for (int i = 0; i < placeholders_order.Count() && i < arguments.Count(); i++)
				placeholders.Add(placeholders_order[i], arguments[i]);
			if (placeholders.Count() != placeholders_order.Count())
			{
				errors = $"{definition.code} Incorrect Amount of arguments expected: {placeholders_order.Count()} got {arguments.Count()} ";
                return false;
			}
			string[] temp;
			for (int i = 0; i < this.code.Length; i++)
			{
				Line line = new Line(Preprocessing.FilteredWhitespaces(code[i].Trim()), parent.number, parent.depth + 1, macros);
				if (line.line.StartsWith("#"))
					continue;
				output.Add(line);
				int labelMode = Preprocessing.GetLabelMode(line.line);
				
				temp = line.line.Split(' ', '=', ':');
				if (labelMode == 1 && temp.Length > 1)
					line.line = line.line.Replace(temp[1], unique + temp[1]);
				else if (labelMode == 2)
					line.line = line.line.Replace(line.line.Trim(), unique + line.line.Trim());
				else
				{
					/* Replaces appends unique to the remaing label values,
				 * so this macro's labels don't collide with any other labels. */
					foreach (var item in temp)
						if (labels.Contains(item))
						{
							string label = unique + item;
							//if (!labelLines.ContainsKey(label))  // 08
       //                         labelLines.Add(label, new List<Line>());  // 08
       //                     labelLines[label].Add(line); // 08
                            line.line = line.line.Replace(item, label);
						}

                    /* Replaces any placeholders tokens with the actual value */
                    foreach (var item in placeholders)
                        if (temp.Contains(item.Key))
                            line.line = line.line.Replace(item.Key, item.Value);

					if (opt.expandConstants)
					{
						/* Replaces any constant tokens with the actual value */
						foreach (var item in constants)
							if (temp.Contains(item.Key))
								line.line = line.line.Replace(item.Key, item.Value);
					}
                }
			}
			return true;
		}
	}
}
