using UnityEngine;

/// <summary>КАТАЛОГ ФОРМ — библиотека ригблоков модельной линии: «имя → меш».
///
/// ЗАЧЕМ ИМЯ СТРОКОЙ, А НЕ ПЕРЕЧИСЛЕНИЕ. Данные видов живут в C# (`SpeciesBootstrap`), а меш — ассет:
/// сослаться на него из кода нечем. Имя разрывает эту зависимость — вид говорит «клин», каталог отвечает
/// мешем, и модельная линия пополняет библиотеку, не трогая ни строки кода механик.
///
/// ТРЕБОВАНИЯ К БЛОКУ (контракт `Docs/models/SPEC-model-contract.md` §7.4): габарит ровно 1×1×1, центр в
/// нуле, канонический кадр (+Z вдоль тела, +Y наружу), имя объекта в FBX = имя блока. Тогда блок подменяет
/// куб ОДИН В ОДИН: `scale` в долях `baseSize` продолжает означать то же самое, и компенсация по Y, нужная
/// капсуле с цилиндром, блоку не требуется.
///
/// ЧЕГО ЗДЕСЬ НЕТ — ТЕЛА. Телу форму даёт поле (`BoneMesher`), блоками делаются только ДЕТАЛИ: морда, уши,
/// когти, рога, зубы, иглы, глаза (`SPEC-konstruktor-formy.md` от 10.09: тело полем, детали блоками).</summary>
[CreateAssetMenu(fileName = "Формы", menuName = "Chimera/Каталог форм")]
public class ShapeCatalog : ScriptableObject
{
    /// <summary>Имя ассета в `Resources`, откуда каталог поднимается сам, если его никто не подал.
    /// Каталог — ДАННЫЕ, а не тюнинг: настраивать в инспекторе нечего, а морфология собирается статикой
    /// и из редактора, и в рантайме, и в тестах — общий вход дешевле ссылки в каждом носителе.</summary>
    public const string ResourceName = "Формы";

    /// <summary>ЗАПИСЬ БИБЛИОТЕКИ. Имя — контракт между линиями: его пишет вид (`OrganPart.block`), его же
    /// носит объект в поставленном FBX. Разойтись они могут только молча, поэтому промах имени не
    /// проглатывается, а попадает в `MorphBuilder.MissingBlocks`.</summary>
    [System.Serializable]
    public class Block
    {
        public string name;
        public Mesh mesh;
    }

    public Block[] blocks;

    /// <summary>Меш по имени; нет такого — null (чем заменить, решает носитель).</summary>
    public Mesh Find(string blockName)
    {
        if (blocks == null || string.IsNullOrEmpty(blockName)) return null;
        foreach (var b in blocks)
            if (b != null && b.mesh != null && b.name == blockName) return b.mesh;
        return null;
    }
}
