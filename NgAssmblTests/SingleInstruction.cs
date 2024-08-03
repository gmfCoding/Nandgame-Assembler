using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using NgAssmblCore;
using System.Reflection.Emit;
using System.Linq;

namespace NgAssmblTests
{
    [TestClass]
    public class SingleInstruction
    {

        public static IEnumerable<object[]> RegisterALiteralAssignments
        {
            get
            {
                return new[]
                {
                    new object[] { "A = 1", 1, true},
                    new object[] { "A = -1", 0x87a0, true},
                    new object[] { "A = 2", 2, true},
                    new object[] { "A = 0", 0, true},
                    new object[] { "A = 32767", 32767, true },
                    new object[] { "A = 0x7FFF", 0x7FFF, true },
                    new object[] { "A = 0x84", 0x84, true },
                    new object[] { "A = 0xFF", 0xFF, true },
                    new object[] { "A = 0x0", 0x0, true },
                    new object[] { "A = 0x00", 0x00, true },
                    new object[] { "A = 0xF0", 0xF0, true },
                    new object[] { "A = 0x04", 0x04, true },

                    // TODO: Update parser for binary literals
                    //new object[] { "A = 0b010111", 0b010111, true },
                    //new object[] { "A = 0b_0101_1100", 0b_0101_1100, true },
                    //new object[] { "A = 0b_000_10_00", 0b_000_10_00, true },
                    
                    // The test run with this row fails
                    // Negative not allowed  (other than -1)
                    new object[] { "A = -2", 0, false},
                    new object[] { "A = 32768", 0, false},
                    new object[] { "A = 0x8000", 0, false},
                    new object[] { "A = 0xFFFF", 0, false},
                    new object[] { "A = -32768", 0, false},
                };
            }
        }

        [TestMethod("A Literal Assignments")]
        [TestCategory("test")]
        [DynamicData(nameof(RegisterALiteralAssignments))]

        public void TestARegisterAssignments(string code, int opcode, bool expected_result)
        {
            ExecuteGeneralTest(code, opcode, expected_result);
        }

        public static IEnumerable<object[]> CommonInstructions
        {
            get
            {
                return new[]
                {
                    // Tests marked with *SW are supposed to have SW set lexically but as a simplifiction it is not because the operation is commutative (a+b = b+a)
                    // SW = 0 DST = D op A
                    // SW = 1 DST = A op D

                    // Although yes, through a ci op code you can get A = 1, A = -1, A = 0, the current assembly tries to use literal assignment
                    //new object[] { "A = 1", 1, true}, 
                    //new object[] { "A = 0", 0x80a0, true},
                    new object[] { "A", 0x8480, true},
                    new object[] { "*A", 0x9480 , true},
                    new object[] { "D", 0x84c0, true},
                    new object[] { "A+1", 0x8540, true},
                    new object[] { "A-1", 0x8740, true},
                    new object[] { "-A", 0x8680, true},
                    new object[] { "-D", 0x86c0, true},
                    new object[] { "D^A", 0x8200, true},
                    new object[] { "D|A", 0x8100, true}, 
                    //new object[] {= A|D", 0x8140, true}, // *SW (0x8160) // Operation is not supported in the direction
                    new object[] { "D&A", 0x8000, true},
                    //new object[] {= A&D", 0x8040, true}, // *SW (0x8060)  // Operation is not supported in the direction
                    new object[] { "A-D", 0x8640, true},
                    new object[] { "D-A", 0x8600, true},
                    new object[] { "D+A", 0x8400, true},
                    new object[] { "D+1", 0x8500, true},
                    new object[] { "A+1", 0x8540, true},
                    new object[] { "D-1", 0x8700, true},
                    new object[] { "A-1", 0x8740, true},
                    new object[] { "~A", 0x8340, true},
                    new object[] { "~D", 0x8300, true},
                };
            }
        }

        public static IEnumerable<object[]> TransformInstructionTestcasePre(IEnumerable<object[]> objects, string dest, int code)
        {
            return TransformInstructionTestcasePrePost(objects, dest, "", code);
        }

        public static IEnumerable<object[]> TransformInstructionTestcasePrePost(IEnumerable<object[]> objects, string pre, string post, int code)
        {
            foreach (var item in objects)
            {
                item[0] = pre + ((string)item[0]) + post;
                item[1] = (int)item[1] | code;
            }
            return objects;
        }

        public static IEnumerable<object[]> RegisterAstarInstructions
        {
            get
            {
                return TransformInstructionTestcasePre(CommonInstructions, "*A = ", 0x8);
            }
        }

        public static IEnumerable<object[]> RegisterDInstructions
        {
            get
            {
                return TransformInstructionTestcasePre(CommonInstructions, "D = ", 0x10);
            }
        }

        public static IEnumerable<object[]> RegisterAInstructions
        {
            get
            {
                return TransformInstructionTestcasePre(CommonInstructions, "A = ", 0x20);
            }
        }

        public static IEnumerable<object[]> RegisterManyInstructions
        {
            get
            {
                return TransformInstructionTestcasePre(CommonInstructions, "*A, D, A = ", 0x20 | 0x10 | 0x8 );
            }
        }

        public static IEnumerable<object[]> RegisterManyInstructionsJump
        {
            get
            {
                Dictionary<string, int> variations = new Dictionary<string, int>()
                {
                    { "JGT", 1 },
                    { "JEQ", 2 },
                    { "JLT", 4 },

                    { "JGE", 1 | 2 },
                    { "JNE", 1 | 4 },
                    { "JLE", 2 | 4 },

                    { "JMP", 1 | 2 | 4 },
                };
                IEnumerable<object[]> all = null;
                foreach (var item in variations)
                {
                    var transformed = TransformInstructionTestcasePrePost(RegisterManyInstructions, "", ";" + item.Key, item.Value);
                    if (all != null)
                        all = Enumerable.Concat(all, transformed);
                    else
                        all = transformed;
                }
                return all;
            }
        }

        [TestMethod("Resgister Many Instructions With Jumps")]
        [DynamicData(nameof(RegisterManyInstructionsJump))]

        public void TestRegisterManyInstructionsJump(string code, int opcode, bool expected_result)
        {
            ExecuteGeneralTest(code, opcode, expected_result);
        }

        [TestMethod("Resgister Many Instructions")]
        [DynamicData(nameof(RegisterManyInstructions))]

        public void TestRegisterManyInstructions(string code, int opcode, bool expected_result)
        {
            ExecuteGeneralTest(code, opcode, expected_result);
        }

        [TestMethod("Register Astar Instructions")]
        [DynamicData(nameof(RegisterAstarInstructions))]

        public void TestRegisterAstarInstructions(string code, int opcode, bool expected_result)
        {
            ExecuteGeneralTest(code, opcode, expected_result);
        }

        [TestMethod("Register D Instructions")]
        [DynamicData(nameof(RegisterDInstructions))]

        public void TestRegisterDInstructions(string code, int opcode, bool expected_result)
        {
            ExecuteGeneralTest(code, opcode, expected_result);
        }

        [TestMethod ("Register A Instructions")]
        [DynamicData(nameof(RegisterAInstructions))]

        public void TestRegisterAInstructions(string code, int opcode, bool expected_result)
        {
            ExecuteGeneralTest(code, opcode, expected_result);
        }

        public void ExecuteGeneralTest(string code, int opcode, bool expected_result)
        {
            NgasmContext context = new NgasmContext(code, new NgasmContextOptions() { littleEndian = true });
            bool success = context.Parse();
            if (expected_result == false)
            {
                if (success == true)
                    Assert.Fail($"Suceeded when failure expected on: {code}");
                return;
            }
            if (!success)
                Assert.Fail($"Parser Failed: {code}");
            if (context.lines[0].code != opcode)
                Assert.Fail($"Parser failed on {code}: opcode (0x{context.lines[0].code.ToString("Register X")}) doesn't match the expected opcode (0x{opcode.ToString("X")})");
        }
    }
}
