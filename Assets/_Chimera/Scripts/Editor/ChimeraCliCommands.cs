#if CHIMERA_PIPELINE
using Unity.Pipeline.Commands;

/// <summary>
/// НАШИ ПУНКТЫ МЕНЮ, ДОСТУПНЫЕ ИЗ ТЕРМИНАЛА (Unity CLI + пакет com.unity.pipeline).
///
/// Зачем. Три команды из меню «Chimera» вызывались после каждой правки данных, и каждый вызов стоил
/// переключения человека в редактор: правка → «прогони, пожалуйста» → ожидание → ответ. Здесь они
/// становятся вызовом `unity command chimera-species` и делаются тем, кто правил, — сразу.
///
/// Почему обёртки, а не голый `eval` (он тоже умеет их звать):
///   • имя команды — контракт и переживает переименование класса, а `eval` намертво знает внутренние имена;
///   • `unity command --help` показывает список: команды находятся, а не вспоминаются;
///   • вызов короткий, поэтому им реально пользуются.
///
/// `#if CHIMERA_PIPELINE` — пакет ЭКСПЕРИМЕНТАЛЬНЫЙ (0.6.0-exp.1). Define приходит из `versionDefines`
/// в `Chimera.Editor.asmdef`: снимут пакет — файл просто выключится, а проект соберётся. Без этого
/// эксперимент Unity стал бы жёсткой зависимостью нашей сборки.
///
/// ЧТО ЭТИ КОМАНДЫ НЕ ЗАМЕНЯЮТ: взгляд человека. Они гоняют генераторы и детекторы, то есть отвечают
/// «сходятся ли числа». На вопрос «как это выглядит» по-прежнему отвечает только плейтест.
/// </summary>
public static class ChimeraCliCommands
{
    [CliCommand("chimera-species", "Пересоздать дефолтные виды (5 SpeciesSO в Data/). После правок SpeciesBootstrap")]
    public static string Species()
    {
        SpeciesBootstrap.CreateDefaults();
        return "виды пересозданы; сцену сохранить (Ctrl+S), если менялись привязки";
    }

    [CliCommand("chimera-map", "Выгрузить КАРТУ ТЕЛ — детектор: строит тела настоящим билдером и меряет стыки")]
    public static string Map()
    {
        BodyMap.Generate();
        return "Docs/Диаграммы/КАРТА_ТЕЛ.md обновлена";
    }

    [CliCommand("chimera-diagrams", "Выгрузить СХЕМЫ ТЕЛ — планы тела по видам из SpeciesSO")]
    public static string Diagrams()
    {
        BodyDiagram.Export();
        return "Docs/Диаграммы/<вид>.md обновлены";
    }

    /// <summary>Пересобрать оба отчёта разом: после правки данных сверять надо и план, и замер.</summary>
    [CliCommand("chimera-reports", "Выгрузить И карту тел, И схемы — обычный порядок сверки после правки данных")]
    public static string Reports()
    {
        BodyDiagram.Export();
        BodyMap.Generate();
        return "карта тел и схемы тел обновлены";
    }
}
#endif
