using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NandgameASM2MC
{
    public class Preprocessing
    {
        /// <summary>
        /// Returns the formating type of the Labels       
        /// </summary>
        /// <param name="linews">The contents of the line, spaces as whitespace only!</param>
        /// <returns>
        /// <br></br> 1 : "LABEL name"
        /// <br></br> 2 : "name:"
        /// <br></br> 3 : "LABEL name:"
        /// </returns>
        public static int GetLabelMode(string linews)
        { 
            return (linews.StartsWith("LABEL ") ? 1 : 0) + (linews.Contains(':') ? 2 : 0);
        }

        public static void GetLabelInfo(string linews, out string label, out string[] split)
        {
            label = "";
            split = linews.Split(' ', ':');
            if (split.Length >= 2)
            {
                foreach (var item in split)
                {
                    if (string.IsNullOrWhiteSpace(item) || string.IsNullOrEmpty(item) || item == "LABEL")
                        continue;
                    label = item;
                    break;
                }
            }
        }

        /// <summary>
        /// All occurances of whitespace are guarenteed to be spaces
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FilteredWhitespaces(string str)
        { 
            return new string(str.ToCharArray().Select(c => Char.IsWhiteSpace(c) ? ' ' : c).ToArray());
        }

        /// <summary>
        /// All occurances of whitespace are guarenteed to be spaces
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveWhitespaces(string str)
        {
            return new string(str.ToCharArray().Where(c => !Char.IsWhiteSpace(c)).ToArray());
        }
    }
}
