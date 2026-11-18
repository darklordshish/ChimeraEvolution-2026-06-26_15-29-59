using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chimera.Laws;

namespace ChimeraSim
{
    static class Program
    {
        const string Version = "0.2 (Ф0, С9)";
        const string DataRelPath = "Docs/Диаграммы/Эволюция/данные.json";

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Console.WriteLine("ChimeraSim " + Version);
            var r = new XorShiftRandom(42);
            Console.Write("XorShiftRandom(42), первые 5 NextUInt:");
            for (int i = 0; i < 5; i++) Console.Write(" " + r.NextUInt());
            Console.WriteLine();

            string path = args.Length > 0 ? args[0] : FindData();
            if (path == null || !File.Exists(path))
            {
                Console.Error.WriteLine("нет " + DataRelPath + " — выгрузить командой Unity `chimera-sim-export` или передать путь аргументом");
                return 1;
            }

            string text = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SimData>(text, new JsonSerializerOptions { IncludeFields = true });
            if (data == null || data.schema != SimData.Schema)
            {
                Console.Error.WriteLine($"схема данных {data?.schema} не та, что у симулятора ({SimData.Schema}) — обновить симулятор или перевыгрузить");
                return 1;
            }

            // отпечаток — FNV по тексту файла с пустым полем; расходится — файл правили руками или он неполный
            string blank = Regex.Replace(text, "\"fingerprint\":\\s*\"[0-9a-f]*\"", "\"fingerprint\": \"\"");
            string actual = SimData.Fnv64(blank);
            Console.WriteLine($"данные: {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}");
            Console.WriteLine($"схема {data.schema}, отпечаток {data.fingerprint} {(actual == data.fingerprint ? "(сходится с содержимым)" : $"(НЕ СХОДИТСЯ: содержимое даёт {actual})")}");
            Console.WriteLine($"мир: {data.world.scene} Ø{data.world.mapDiameter:0} м, площадь купола {data.world.domeArea:0} м², сутки {data.world.dayMinutes:0.##} мин реального времени ({data.world.hoursPerSecond:0.####} игр. ч/с)");
            Console.WriteLine($"виды {data.species.Length}, логов {data.lairs.Length}; эволюция NPC {(data.evolution.evolveNpc ? "вкл" : "выкл")}, старт родства {data.evolution.startAffinity}, множитель химеризации {data.evolution.chimerizeMultiplier:0.##}");
            Console.WriteLine();
            Summary.PrintSpecies(data, Console.Out);
            Console.WriteLine();
            Summary.PrintDiet(data, Console.Out);
            Console.WriteLine();
            Summary.PrintLairs(data, Console.Out);
            return actual == data.fingerprint ? 0 : 2;
        }

        // ищем файл от папки сборки и от текущей вверх — запуск из корня репозитория и из bin/ равно работает
        static string FindData()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                {
                    string p = Path.Combine(d.FullName, DataRelPath);
                    if (File.Exists(p)) return p;
                }
            return null;
        }
    }
}
