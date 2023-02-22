using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NandgameASM2MC
{
    public class Tokens
    {
        public readonly static Dictionary<string, ushort> destByteMap = new() { { "A", 0x20 }, { "D", 0x10 }, { "*A", 0x8 } };

        public readonly static Dictionary<string, ushort> byteMap = new()
        {
            {"D+A", 0x400},
            {"D+1", 0x500},
            {"A+1", 0x540},
            {"D-A", 0x600},
            {"A-D", 0x640},
            {"D-1", 0x700},
            {"A-1", 0x740},
            {"A",   0x480},
            {"D",   0x4c0},
            {"-A",  0x680},
            {"-D",  0x6c0},
            {"1",   0x580},
            {"-1", 0x780},
            // BIT WISE
            {"D&A", 0x000},
            {"D|A", 0x100},
            {"D^A", 0x200},
            {"~D",  0x300},
            {"~A",  0x340},
            // Jumps
            {"JGT", 0x8001},
            {"JEQ", 0x8002},
            {"JGE", 0x8003},
            {"JLT", 0x8004},
            {"JNE", 0x8005},
            {"JLE", 0x8006},
            {"JMP", JUMP_CODE}
        };

        public const ushort A_PTR_EXP_CODE = 0x1000;
        public const ushort JUMP_CODE = 0x8007;
        public const ushort INSTRUCTION_CODE = 0x8000;
        public readonly static char[] bit_ops = { '&', '|', '^', '~' };
        public readonly static char[] arith_ops = { '+', '-' };
    }
}
