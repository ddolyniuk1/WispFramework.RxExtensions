using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WispFramework.RxExtensions.Tests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var t = new Tests.ReactiveExtensionsTests();
            t.QueueLatestWhileBusy_WithObservable_ShouldRespectShortCircuit();
            Console.Read();
        }
    }
}
