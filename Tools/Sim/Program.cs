using System;
using Chimera.Laws;

namespace ChimeraSim
{
    static class Program
    {
        const string Version = "0.1 (Ф0, С1)";

        static void Main()
        {
            Console.WriteLine("ChimeraSim " + Version);
            var r = new XorShiftRandom(42);
            Console.WriteLine("XorShiftRandom(42), первые 5 NextUInt:");
            for (int i = 0; i < 5; i++) Console.WriteLine(r.NextUInt());
        }
    }
}
