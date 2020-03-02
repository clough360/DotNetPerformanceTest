using System;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace PerformanceTests
{
    [Orderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Declared)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [RPlotExporter, RankColumn]

    public class StringToUppercase
    {
        private string _baseString;
        private string _resultString;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var sb = new StringBuilder();
            var r = new Random(1);
            for (var i = 0; i < 1000; i++)
            {
                // random upper or lowercase char with some others, <65 = space
                var nextChar = r.Next(50, 122);
                if (nextChar < 65)
                {
                    // random space
                    nextChar = 32;
                }

                sb.Append((char)nextChar);
            }

            _baseString = sb.ToString();
            _resultString = _baseString.ToUpper();
            Console.WriteLine(sb.ToString());
        }

        public void CheckResult(string compare)
        {
            if (compare != _resultString)
            {
                if (compare.Length != _resultString.Length)
                {
                    throw new Exception($"string is not correct: (different lengths)\r\n{compare}");
                }
                for (var i = 0; i < _resultString.Length; i++)
                {
                    if (_resultString[i] != compare[i])
                    {
                        throw new Exception($"string is not correct: (differ at position {i}: {compare[i]}!={_resultString[i]})\r\n{compare}");
                    }
                }
            }

        }

        [Benchmark(Baseline = true)]
        public void ToUpper()
        {
            CheckResult(_baseString.ToUpper());
        }

        [Benchmark]
        public void StringBuilderAscii()
        {
            var sb = new StringBuilder(_baseString.Length);
            foreach (var c in _baseString)
            {
                if (c >= 97 && c <= 122)
                {
                    sb.Append((char)(c - 32));
                }
                else
                {
                    sb.Append(c);
                }
            }
            CheckResult(sb.ToString());
        }

        [Benchmark]
        public void MemoryExtensionsToUpper()
        {
            var _resultString = new Span<char>();
            MemoryExtensions.ToUpper(_baseString.AsSpan(), _resultString, CultureInfo.InvariantCulture);
            CheckResult(this._resultString);
        }


        [Benchmark]
        public unsafe void UnsafeCharArrayAsciiFixed()
        {
            var newString = _baseString;

            fixed (char* c = newString)
            {
                for (var i = 0; i < _baseString.Length; i++)
                {
                    if (c[i] >= 97 && c[i] <= 122)
                    {
                        c[i] = (char) (c[i] - 32);
                    }
                }

            }

            CheckResult(newString);
        }


        [Benchmark]
        public unsafe void UnsafeCharArrayFixedPointer()
        {
            var newString = _baseString;
            
            fixed (char* c = newString)
            {
                var c2 = c;

                for (var i = 0; i < _baseString.Length; i++)
                {
                    if (*c2 >= 97 && *c2 <= 122)
                    {
                        *c2 = (char)(*c2 - 32);
                    }

                    c2++;
                }
            }

            CheckResult(newString);
        }
    }
}
