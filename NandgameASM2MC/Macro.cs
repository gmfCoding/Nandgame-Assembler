using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NgAssmblCore
{
    public class MacroDefinition
	{
		public string shared_constants;
		public string[] placeholders;
		public string[] labels;
		public string code;
	}

	public class Macros
	{
		[JsonProperty("Constants")]
		public Dictionary<string, Dictionary<string, string>> constants;

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

		public bool ContainsConstant(string stack, string line)
		{
			if (!constants.ContainsKey(stack))
				return false;
			line = Preprocessing.FilteredWhitespaces(line);
			string[] values = line.Split(' ');
			foreach (var item in values)
				if (constants[stack].ContainsKey(item))
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

		public bool GetConstant(string stack, string linews, out string value)
		{
			value = null;
			if (!constants.ContainsKey(stack))
				return false;
			string[] values = linews.Split(' ');
			foreach (var item in values)
				if (constants[stack].TryGetValue(item, out value))
					return value != null;
			return false;
		}

		public Dictionary<string, string> GetConstants(string stack)
		{
			if (constants.ContainsKey(stack))
				return constants[stack];
			return null;
		}

		public void GenerateInstancers()
		{
			foreach (var item in macros)
			{
				instancers.Add(item.Key, new MacroInstancer(item.Value, this));
			}
		}
	}

    public class MacroInstancer
	{
		/// <summary> The assembly code implementation of the macro.</summary>
		public string[] code;

		public Dictionary<string, string> constants;
		public HashSet<string> labels;
		public static HashSet<string> usedSalts;
		public MacroInstancer(MacroDefinition definition, Macros macros)
		{
			constants = macros.GetConstants(definition.shared_constants);
			labels = new HashSet<string>(definition.labels);
			if (constants == null)
				constants = new Dictionary<string, string>();
		}

		private static Random random = new Random();

		public string GetUniqueSalt(int length = 5)
		{
			const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			string value = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
			usedSalts.Add(value);
			return value;
		}

		/// <summary>
		/// Returns code to which replaces a line with to with what to override 
		/// </summary>
		/// <param name="placeholders"></param>
		/// <returns>Code to exapand/replace the macro </returns>
		public string[] Generate(Dictionary<string, string> placeholders)
		{
			string unique = GetUniqueSalt();
			string[] output = new string[code.Length];

			string[] temp;
			for (int i = 0; i < this.code.Length; i++)
			{
				output[i] = Preprocessing.FilteredWhitespaces(code[i]);
				if (output[i].TrimStart().StartsWith("#"))
					continue;
				int labelMode = Preprocessing.GetLabelMode(output[i]);

				temp = output[i].Split(' ', '=');
				if (labelMode == 1 && temp.Length > 1)
					output[i] = output[i].Replace(temp[1], unique + temp[1]);
				else if (labelMode == 2)
					output[i] = output[i].Replace(output[i].Trim(), unique + output[i].Trim());
				/* Replaces appends unique to the remaing label values,
				 * so this macro's labels don't collide with any other labels. */
				foreach (var item in temp)
					if (labels.Contains(item))
						output[i] = output[i].Replace(item, unique + item);
				/* Replaces any placeholders tokens with the actual value */
				foreach (var item in placeholders)
					output[i] = output[i].Replace(item.Key, item.Value);
				/* Replaces any constant tokens with the actual value */
				foreach (var item in constants)
						output[i] = output[i].Replace(item.Key, item.Value);
			}
			return output;
		}
	}
}
