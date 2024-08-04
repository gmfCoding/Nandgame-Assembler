using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using NgAssmblCore;
using System.Reflection.Emit;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NgAssmblTests
{
    [TestClass]
    public class SmallProgram
    {

        [TestMethod("A small program")]

        public void Test_ASmallProgram()
        {

            // Program that counts to 10 (result in addr 42)
            string source = @"
                A = 42
                D, *A = *A+1
                A = 10
                D = A-D
                A = 0
                D;JLT
            ";
            Macros macro = Macros.GetDefault();

            NgasmContext context = new NgasmContext(source, new NgasmContextOptions() { littleEndian = true });
            if (!context.Parse())
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in context.errors)
                    sb.Append($"{item}\n");
                Assert.Fail($"Failed to parse source: \n {sb}");
            }
        }


        [TestMethod("Macros")]
        public void Test_Macros()
        {
            object[][] data = new object[][] {
                //new object[] { "INIT_STACK", new string[] { "A = 0x100", "D = A", "A = 0", "*A = D" } },
                //new object[] { "PUSH_D", new string[] { "A = 0", "*A = *A", "*A = *A+1", "A = *A-1", "*A = D" } },
                //new object[] { "PUSH_VALUE 42", new string[] { "A = 42", "D = A", "A = 0", "*A = *A", "*A = *A+1", "A = *A-1", "*A = D" } },
                //new object[] { "PUSH_VALUE 42", new string[] { "A = 42", "D = A", "A = 0", "*A = *A", "*A = *A+1", "A = *A-1", "*A = D" } },
                new object[] { "CALLX BOBBY", new string[] { " POP_D", "POP_A", "D = A-D", "PUSH_DA = 42" } },
                //new object[] { "SUB", new string[] { " POP_D", "POP_A", "D = A-D", "PUSH_DA = 42" } },
                //new object[] { "SUB", new string[] { " POP_D", "POP_A", "D = A-D", "PUSH_DA = 42" } },
                //new object[] { "SUB", new string[] { " POP_D", "POP_A", "D = A-D", "PUSH_DA = 42" } },
                //new object[] { "SUB", new string[] { " POP_D", "POP_A", "D = A-D", "PUSH_DA = 42" } },
            };

            List<string> source = data.Select(x => (string)x[0]).ToList();
            List<string> expected = data.SelectMany(x => (string[])x[1]).ToList();


            Macros macros = Macros.GetDefault();`
            macros.GenerateInstancers();
            Preprocessor processor = new Preprocessor(macros, false);

            if (!processor.ProcessSource(source, out List<Line> output, out string error))
                Assert.Fail($"An error occured: {error}");

            for (int i = 0; i < expected.Count() && i < output.Count(); i++)
            {
                if (expected[i] != output[i].line)
                    Assert.Fail($"Preprocessor output doesn't match expected:\nExpected:\n{expected[i]}\nGenerated:\n{output[i]}");
            }
            if (expected.Count() != output.Count())
                Assert.Fail($"Preprocessor output doesn't match expected.");
        }
    }
}
