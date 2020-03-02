using BenchmarkDotNet.Running;

namespace PerformanceTests
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<SumItemsWithLoop>();
            BenchmarkRunner.Run<StringToUppercase>();
        }
    }
}
