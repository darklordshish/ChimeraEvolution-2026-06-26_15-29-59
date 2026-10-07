using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chimera.Tests.EditMode
{
    /// <summary>КОПИЯ МЕСТА НЕ ТЕРЯЕТ ПОЛЕЙ (07.10): смешанный план копирует места шасси, и ручная копия забыла
    /// `chainForward` — цепь шеи змеи у любой её химеры раскладывалась назад, в 3 м от головы. Сторож: копия каждого места
    /// каждого вида совпадает с оригиналом во всех сериализуемых полях.</summary>
    public class BlendPlanCloneTests
    {
        static readonly string[] Species = { "Волк", "Лось", "Ёж", "Змея", "Человек" };

        [Test]
        public void CloneSocket_KeepsEveryField_OnEverySpecies()
        {
            foreach (var n in Species)
            {
                var sp = AssetDatabase.LoadAssetAtPath<SpeciesSO>($"Assets/_Chimera/Data/{n}.asset");
                foreach (var s in sp.sockets)
                    Assert.AreEqual(JsonUtility.ToJson(s), JsonUtility.ToJson(CreatureBody.CloneSocket(s)), $"{n}: место «{s.name}» скопировано с потерей");
            }
        }
    }
}
