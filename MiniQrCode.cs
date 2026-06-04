/*
 * MiniQrCode.cs - 最小二维码生成器 (单文件,纯 C#,无外部依赖)
 *
 * 用途:替代 ZXing.dll 的 BarcodeWriterGeneric,只生成不解码,只支持 byte 模式 (UTF-8)。
 *       适用于支付二维码、URL 二维码等任意文本/字节串编码场景。
 *
 * 用法:
 *     bool[,] m = MiniQrCode.Encode("https://...", MiniQrCode.Ecc.M);
 *     int size = m.GetLength(0);  // = m.GetLength(1),正方形
 *     bool dark = m[y, x];        // y=row, x=col (与 ZXing.BitMatrix[x,y] 注意 xy 顺序)
 *
 * 来源 (MIT 许可证,见下方完整 license):
 *   - QR Code generator library (.NET) by Manuel Bleichenbacher
 *     https://github.com/manuelbl/QrCodeGenerator
 *   - QR Code generator library by Project Nayuki
 *     https://github.com/nayuki/QR-Code-generator
 *     https://www.nayuki.io/page/qr-code-generator-library
 *
 * 本文件功能精简版本,改造点:
 *   - 仅 byte 模式 (UTF-8),去掉 numeric / alphanumeric / kanji 优化
 *   - 去掉 SVG / BMP / PNG / Graphics 输出,只保留 bool[,] 矩阵接口
 *   - 去掉 Structured Append、ECI、QrSegmentAdvanced
 *   - 内联到单文件,改 internal 静态类避免外部污染
 *
 * MIT License:
 *   Copyright (c) Manuel Bleichenbacher
 *   Copyright (c) Project Nayuki
 *
 *   Permission is hereby granted, free of charge, to any person obtaining a copy
 *   of this software and associated documentation files (the "Software"), to deal
 *   in the Software without restriction, including without limitation the rights
 *   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *   copies of the Software, and to permit persons to whom the Software is
 *   furnished to do so, subject to the following conditions:
 *
 *   The above copyright notice and this permission notice shall be included in
 *   all copies or substantial portions of the Software.
 *
 *   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 *   FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 *   IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace VR180Recorder
{
    /// <summary>
    /// 最小二维码生成器。线程不安全,但每次调用都是独立的,可在不同线程并发调用。
    /// </summary>
    internal static class MiniQrCode
    {
        /// <summary>纠错等级。L≈7%、M≈15%、Q≈25%、H≈30%。支付二维码推荐 M。</summary>
        public enum Ecc { L = 0, M = 1, Q = 2, H = 3 }

        // ECC level → format-info 中的 2-bit code: L=01, M=00, Q=11, H=10
        private static readonly int[] EccFormatBits = { 1, 0, 3, 2 };

        // ============================================================================
        //  ISO/IEC 18004 Model 2 静态查表 (来自 Nayuki QR-Code-generator c/qrcodegen.c)
        // ============================================================================

        // [ECC level, version 0..40] → 每块的纠错码字数
        // version=0 是占位 (-1),实际从 1 开始
        private static readonly sbyte[,] EccCodewordsPerBlock = new sbyte[4, 41]
        {
            //v: 0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18  19  20  21  22  23  24  25  26  27  28  29  30  31  32  33  34  35  36  37  38  39  40
            { -1,  7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }, // L
            { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 }, // M
            { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }, // Q
            { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }, // H
        };

        // [ECC level, version 0..40] → 纠错块数
        private static readonly sbyte[,] NumErrorCorrectionBlocks = new sbyte[4, 41]
        {
            //v: 0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40
            { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9,10,12,12,12,13,14,15,16,17,18,19,19,20,21,22,24,25 }, // L
            { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9,10,10,11,13,14,16,17,17,18,20,21,23,25,26,28,29,31,33,35,37,38,40,43,45,47,49 }, // M
            { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8,10,12,16,12,17,16,18,21,20,23,23,25,27,29,34,34,35,38,40,43,45,48,51,53,56,59,62,65,68 }, // Q
            { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8,11,11,16,16,18,16,19,21,25,25,25,34,30,32,35,37,40,42,45,48,51,54,57,60,63,66,70,74,77,81 }, // H
        };

        // 自动 mask 选择时的 penalty 权重 (规范定义)
        private const int PenaltyN1 = 3;   // 连续 5+ 同色像素
        private const int PenaltyN2 = 3;   // 2x2 同色块
        private const int PenaltyN3 = 40;  // finder-like 1:1:3:1:1 pattern
        private const int PenaltyN4 = 10;  // 黑色比例偏离 50% (每 5%)

        // ============================================================================
        //  Public API
        // ============================================================================

        /// <summary>
        /// 编码字符串 (UTF-8 字节串) 为二维码矩阵。失败抛 ArgumentException。
        /// </summary>
        /// <param name="text">要编码的文本,可含任意 Unicode 字符 (UTF-8 编码后按 byte 模式处理)。</param>
        /// <param name="ecc">最低纠错等级。在不增大版本号的前提下会自动尽可能提升。</param>
        /// <returns>bool[size, size] 矩阵,索引 [y, x],true=黑,false=白。</returns>
        public static bool[,] Encode(string text, Ecc ecc = Ecc.M)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? string.Empty);
            return EncodeBytes(data, ecc);
        }

        /// <summary>
        /// 直接以 byte 模式编码任意字节串。
        /// </summary>
        public static bool[,] EncodeBytes(byte[] data, Ecc ecc = Ecc.M)
        {
            return EncodeBytes(data, ecc, boostEcc: true);
        }

        /// <summary>
        /// EncodeBytes 的可选 boost 重载。boostEcc=true (默认) 在不增大版本号的前提下尽量
        /// 提升 ECC level (规范鼓励)。设为 false 时严格使用传入的 ECC level。
        /// </summary>
        public static bool[,] EncodeBytes(byte[] data, Ecc ecc, bool boostEcc)
        {
            if (data == null) data = new byte[0];

            int version;
            int dataBitsUsed;
            ChooseVersion(data.Length, ecc, out version, out dataBitsUsed);

            // 在不增大版本号的前提下尽量提升 ECC level (规范鼓励)
            if (boostEcc)
            {
                for (int e = (int)ecc + 1; e <= 3; e++)
                {
                    if (dataBitsUsed <= GetNumDataCodewords(version, (Ecc)e) * 8)
                        ecc = (Ecc)e;
                }
            }

            // Step 3: 构建 bit stream
            int totalBits = GetNumDataCodewords(version, ecc) * 8;
            var bits = new List<bool>(totalBits);

            AppendBits(bits, 0x4, 4);                                  // mode indicator: byte = 0100
            AppendBits(bits, data.Length, (version < 10) ? 8 : 16);    // 字符计数
            foreach (byte b in data) AppendBits(bits, b, 8);           // 数据字节

            // 终止符 (最多 4 个 0,但不超过容量)
            int termLen = Math.Min(4, totalBits - bits.Count);
            AppendBits(bits, 0, termLen);
            // 补 0 到字节边界
            AppendBits(bits, 0, (8 - bits.Count % 8) % 8);
            // 用 0xEC / 0x11 交替填满
            for (uint pad = 0xEC; bits.Count < totalBits; pad ^= 0xEC ^ 0x11)
                AppendBits(bits, (int)pad, 8);

            // Step 4: 打包成字节
            byte[] dataCodewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i]) dataCodewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));

            // Step 5: 加纠错码 + 交错排列
            byte[] allCodewords = AddEccAndInterleave(version, ecc, dataCodewords);

            // Step 6: 绘制矩阵 + 自动选择最佳 mask
            return BuildMatrix(version, ecc, allCodewords);
        }

        // 选择最小可容纳的版本号 (不 boost)
        private static void ChooseVersion(int byteLen, Ecc ecc, out int version, out int dataBitsUsed)
        {
            version = -1;
            dataBitsUsed = -1;
            for (int v = 1; v <= 40; v++)
            {
                int charCountBits = (v < 10) ? 8 : 16;
                if (byteLen >= (1 << charCountBits)) continue;
                int needBits = 4 + charCountBits + byteLen * 8;
                int capBits = GetNumDataCodewords(v, ecc) * 8;
                if (needBits <= capBits) { version = v; dataBitsUsed = needBits; return; }
            }
            throw new ArgumentException("Data too long for QR code (max version 40, max " +
                                        (GetNumDataCodewords(40, Ecc.L) - 3) + " bytes at L)");
        }

        // ============================================================================
        //  矩阵构建主流程
        // ============================================================================

        private static bool[,] BuildMatrix(int version, Ecc ecc, byte[] allCodewords)
        {
            int size = version * 4 + 17;
            bool[,] modules = new bool[size, size];     // [y, x],true=dark
            bool[,] isFunction = new bool[size, size];  // 标记 function module (不参与 mask)

            // 绘制 function patterns (finder/alignment/timing/format-placeholder/version)
            DrawFunctionPatterns(version, modules, isFunction);

            // 绘制数据 (zigzag scan)
            DrawCodewords(version, modules, isFunction, allCodewords);

            // 自动选最佳 mask: 8 个都试一遍,选 penalty 最低的
            int bestMask = -1;
            int minPenalty = int.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                ApplyMask(modules, isFunction, m);
                DrawFormatBits(modules, isFunction, ecc, m);
                int p = GetPenaltyScore(modules);
                if (p < minPenalty) { minPenalty = p; bestMask = m; }
                ApplyMask(modules, isFunction, m);  // XOR 二次还原
            }
            ApplyMask(modules, isFunction, bestMask);
            DrawFormatBits(modules, isFunction, ecc, bestMask);

            return modules;
        }

        // ============================================================================
        //  Function pattern 绘制
        // ============================================================================

        private static void DrawFunctionPatterns(int version, bool[,] modules, bool[,] isFunction)
        {
            int size = modules.GetLength(0);

            // Timing patterns (第 6 行 / 第 6 列的交替黑白)
            for (int i = 0; i < size; i++)
            {
                SetFunc(modules, isFunction, 6, i, i % 2 == 0);
                SetFunc(modules, isFunction, i, 6, i % 2 == 0);
            }

            // 三个 Finder pattern (左上、右上、左下),9x9 含分隔线
            DrawFinderPattern(modules, isFunction, 3, 3);
            DrawFinderPattern(modules, isFunction, size - 4, 3);
            DrawFinderPattern(modules, isFunction, 3, size - 4);

            // Alignment patterns (v2+ 才有)
            int[] alignPos = GetAlignmentPatternPositions(version);
            int n = alignPos.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)))
                        DrawAlignmentPattern(modules, isFunction, alignPos[i], alignPos[j]);

            // Format-info 占位 (mask=0,实际 mask 选定后会 overwrite)
            DrawFormatBits(modules, isFunction, Ecc.L, 0);
            // Version-info (v7+ 才有)
            DrawVersion(version, modules, isFunction);
        }

        private static void DrawFinderPattern(bool[,] modules, bool[,] isFunction, int x, int y)
        {
            int size = modules.GetLength(0);
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));   // Chebyshev dist
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                        SetFunc(modules, isFunction, xx, yy, dist != 2 && dist != 4);
                }
        }

        private static void DrawAlignmentPattern(bool[,] modules, bool[,] isFunction, int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    SetFunc(modules, isFunction, x + dx, y + dy,
                            Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        // 计算 alignment pattern 中心坐标列表 (Nayuki 推导的等距算法,等价于规范附录 E 的硬编码表)
        private static int[] GetAlignmentPatternPositions(int version)
        {
            if (version == 1) return new int[0];
            int numAlign = version / 7 + 2;
            int step = (version * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4) * 2;
            int[] result = new int[numAlign];
            result[0] = 6;
            int size = version * 4 + 17;
            for (int i = result.Length - 1, pos = size - 7; i >= 1; i--, pos -= step)
                result[i] = pos;
            return result;
        }

        // 绘制 format-info (15 bits BCH(15,5),含 mask + ecc level)
        private static void DrawFormatBits(bool[,] modules, bool[,] isFunction, Ecc ecc, int mask)
        {
            int size = modules.GetLength(0);
            uint data = (uint)((EccFormatBits[(int)ecc] << 3) | mask);   // 5 bits
            uint rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            uint bits = ((data << 10) | rem) ^ 0x5412;  // BCH 15-bit code

            // 第一份 (左上区域)
            for (int i = 0; i <= 5; i++) SetFunc(modules, isFunction, 8, i, GetBit(bits, i));
            SetFunc(modules, isFunction, 8, 7, GetBit(bits, 6));
            SetFunc(modules, isFunction, 8, 8, GetBit(bits, 7));
            SetFunc(modules, isFunction, 7, 8, GetBit(bits, 8));
            for (int i = 9; i < 15; i++) SetFunc(modules, isFunction, 14 - i, 8, GetBit(bits, i));

            // 第二份 (右上 + 左下,含右下永远黑的"dark module")
            for (int i = 0; i < 8; i++) SetFunc(modules, isFunction, size - 1 - i, 8, GetBit(bits, i));
            for (int i = 8; i < 15; i++) SetFunc(modules, isFunction, 8, size - 15 + i, GetBit(bits, i));
            SetFunc(modules, isFunction, 8, size - 8, true);
        }

        // 绘制 version-info (v7+ 才有,18 bits)
        private static void DrawVersion(int version, bool[,] modules, bool[,] isFunction)
        {
            if (version < 7) return;
            int size = modules.GetLength(0);
            uint rem = (uint)version;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            uint bits = ((uint)version << 12) | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = GetBit(bits, i);
                int a = size - 11 + i % 3;
                int b = i / 3;
                SetFunc(modules, isFunction, a, b, bit);
                SetFunc(modules, isFunction, b, a, bit);
            }
        }

        // ============================================================================
        //  Codeword 绘制 (zigzag scan) + Mask
        // ============================================================================

        private static void DrawCodewords(int version, bool[,] modules, bool[,] isFunction, byte[] data)
        {
            int size = modules.GetLength(0);
            int i = 0;
            // 从右下往左上,2 列一组,垂直方向 zigzag 上下交替
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;   // timing 列在 x=6,跳过
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!isFunction[y, x] && i < data.Length * 8)
                        {
                            modules[y, x] = GetBit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
                }
            }
        }

        // 8 种 mask 表达式 (规范定义)。XOR 应用于非 function 模块,二次调用可还原。
        private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask)
        {
            int size = modules.GetLength(0);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (isFunction[y, x]) continue;
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        case 7: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                        default: throw new ArgumentOutOfRangeException("mask");
                    }
                    if (invert) modules[y, x] ^= true;
                }
        }

        // 计算当前 mask 的 penalty score (越低越好)。规范定义的 4 项之和。
        private static int GetPenaltyScore(bool[,] modules)
        {
            int size = modules.GetLength(0);
            int result = 0;

            // 规则 1+3: 行方向连续同色 + finder-like pattern
            for (int y = 0; y < size; y++)
            {
                bool runColor = false; int runX = 0;
                int[] hist = new int[7];
                for (int x = 0; x < size; x++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runX++;
                        if (runX == 5) result += PenaltyN1;
                        else if (runX > 5) result++;
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runX, hist, size);
                        if (!runColor) result += FinderPenaltyCountPatterns(hist, size) * PenaltyN3;
                        runColor = modules[y, x];
                        runX = 1;
                    }
                }
                result += FinderPenaltyTerminateAndCount(runColor, runX, hist, size) * PenaltyN3;
            }
            // 列方向同上
            for (int x = 0; x < size; x++)
            {
                bool runColor = false; int runY = 0;
                int[] hist = new int[7];
                for (int y = 0; y < size; y++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runY++;
                        if (runY == 5) result += PenaltyN1;
                        else if (runY > 5) result++;
                    }
                    else
                    {
                        FinderPenaltyAddHistory(runY, hist, size);
                        if (!runColor) result += FinderPenaltyCountPatterns(hist, size) * PenaltyN3;
                        runColor = modules[y, x];
                        runY = 1;
                    }
                }
                result += FinderPenaltyTerminateAndCount(runColor, runY, hist, size) * PenaltyN3;
            }
            // 规则 2: 2x2 同色块
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                {
                    bool c = modules[y, x];
                    if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1])
                        result += PenaltyN2;
                }
            // 规则 4: 黑色比例偏离 50%
            int dark = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (modules[y, x]) dark++;
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += k * PenaltyN4;

            return result;
        }

        // history[0..6]: 最近 7 个 run 长度 (只在 finder pattern 检测时使用,反序存储)
        private static void FinderPenaltyAddHistory(int currentRunLength, int[] history, int size)
        {
            if (history[0] == 0) currentRunLength += size;   // 行/列起点处的隐式 light run (规范 §8.5.2)
            for (int i = history.Length - 1; i >= 1; i--) history[i] = history[i - 1];
            history[0] = currentRunLength;
        }

        // 检测 1:1:3:1:1:4 finder-like pattern (前后任一侧有 ≥4 倍长度的 light run)
        private static int FinderPenaltyCountPatterns(int[] history, int size)
        {
            int n = history[1];
            bool core = n > 0 && history[2] == n && history[3] == n * 3 && history[4] == n && history[5] == n;
            return (core && history[0] >= n * 4 && history[6] >= n ? 1 : 0)
                 + (core && history[6] >= n * 4 && history[0] >= n ? 1 : 0);
        }

        // 行/列扫描结束时,补一个虚拟 light run,然后再统计一次
        private static int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, int[] history, int size)
        {
            if (currentRunColor) { FinderPenaltyAddHistory(currentRunLength, history, size); currentRunLength = 0; }
            currentRunLength += size;   // 末尾隐式 light run
            FinderPenaltyAddHistory(currentRunLength, history, size);
            return FinderPenaltyCountPatterns(history, size);
        }

        // ============================================================================
        //  Reed-Solomon (GF(256), 0x11D) + 块交错
        // ============================================================================

        private static byte[] AddEccAndInterleave(int version, Ecc ecc, byte[] data)
        {
            int numBlocks = NumErrorCorrectionBlocks[(int)ecc, version];
            int blockEccLen = EccCodewordsPerBlock[(int)ecc, version];
            int rawCodewords = GetNumRawDataModules(version) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;

            byte[][] blocks = new byte[numBlocks][];
            byte[] rsDivisor = ReedSolomonComputeDivisor(blockEccLen);

            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                byte[] dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;

                byte[] block = new byte[shortBlockLen + 1];
                Array.Copy(dat, 0, block, 0, dat.Length);
                byte[] ecc2 = ReedSolomonComputeRemainder(dat, rsDivisor);
                Array.Copy(ecc2, 0, block, block.Length - blockEccLen, ecc2.Length);
                blocks[i] = block;
            }

            // 交错: 先按列扫数据部分,再按列扫纠错部分,跳过 short block 的 padding 槽
            byte[] result = new byte[rawCodewords];
            int outIdx = 0;
            for (int i = 0; i < blocks[0].Length; i++)
            {
                for (int j = 0; j < blocks.Length; j++)
                {
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                        result[outIdx++] = blocks[j][i];
                }
            }
            return result;
        }

        // 算 Reed-Solomon 生成多项式: (x - r^0)(x - r^1)...(x - r^{degree-1}) over GF(256)
        // 系数按降幂存储,首项 (x^degree 系数 = 1) 略去
        private static byte[] ReedSolomonComputeDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;   // 起始 monomial x^0
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = ReedSolomonMultiply(result[j], (byte)root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = ReedSolomonMultiply((byte)root, 0x02);
            }
            return result;
        }

        // 算 data % divisor (LFSR 实现)
        private static byte[] ReedSolomonComputeRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ result[0]);
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++)
                    result[i] ^= ReedSolomonMultiply(divisor[i], factor);
            }
            return result;
        }

        // GF(256) 乘法,modulus = 0x11D (x^8 + x^4 + x^3 + x^2 + 1)
        private static byte ReedSolomonMultiply(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ (((z >> 7) & 1) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        // ============================================================================
        //  小工具
        // ============================================================================

        // QR 数据区可容纳的 bit 数 / 8 (无功能模块) 推导公式 (Nayuki)
        private static int GetNumRawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7) result -= 36;
            }
            return result;
        }

        // 实际可用数据字节数 (扣除纠错码字)
        private static int GetNumDataCodewords(int ver, Ecc ecc)
        {
            return GetNumRawDataModules(ver) / 8
                 - EccCodewordsPerBlock[(int)ecc, ver] * NumErrorCorrectionBlocks[(int)ecc, ver];
        }

        private static void AppendBits(List<bool> bits, int value, int len)
        {
            if (len < 0 || len > 31) throw new ArgumentOutOfRangeException("len");
            for (int i = len - 1; i >= 0; i--)
                bits.Add(((value >> i) & 1) != 0);
        }

        private static bool GetBit(uint x, int i) { return ((x >> i) & 1) != 0; }
        private static bool GetBit(int x, int i)  { return ((x >> i) & 1) != 0; }

        private static void SetFunc(bool[,] modules, bool[,] isFunction, int x, int y, bool isDark)
        {
            modules[y, x] = isDark;
            isFunction[y, x] = true;
        }
    }
}
