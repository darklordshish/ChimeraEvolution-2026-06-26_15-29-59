using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>РОЗОВОЕ ДЕРЕВО — `data Tree a = Node a [Tree a]` (отчёт `Docs/reports/Тело как рекурсивный тип.md`).
///
/// Неизменяемое: любая правка строит новое дерево, а нетронутые поддеревья ДЕЛЯТСЯ со старым (`Replace` не копирует то,
/// куда не заходил). Операций ровно столько, сколько нужно телу, без теоретической надстройки:
/// `Map` — функтор (масштаб цепи), `Fold` — катаморфизм (длины, коробки), `Scan` — наследуемый атрибут сверху вниз
/// (поза: кадр родителя → кадр ребёнка), `Replace` — замена поддерева в фокусе (подстановка цепи), `Prune` — срез
/// поддеревьев по предикату, `Subtrees` — обход в прямом порядке (родитель раньше детей).</summary>
public sealed class Tree<T>
{
    public readonly T Value;
    public readonly IReadOnlyList<Tree<T>> Kids;

    static readonly Tree<T>[] None = new Tree<T>[0];

    public Tree(T value, IEnumerable<Tree<T>> kids = null)
    {
        Value = value;
        Kids = kids?.ToArray() ?? None;
    }

    /// <summary>fmap: то же дерево, другое значение в каждом узле.</summary>
    public Tree<U> Map<U>(Func<T, U> f) => new(f(Value), Kids.Select(k => k.Map(f)));

    /// <summary>Катаморфизм: значение снизу вверх — алгебра получает узел и уже свёрнутых детей.</summary>
    public R Fold<R>(Func<T, IReadOnlyList<R>, R> alg) => alg(Value, Kids.Select(k => k.Fold(alg)).ToArray());

    /// <summary>Наследуемый атрибут: атрибут узла = `step`(атрибут родителя, узел); у корня — от `seed`.</summary>
    public Tree<(T value, A attr)> Scan<A>(A seed, Func<A, T, A> step)
    {
        var a = step(seed, Value);
        return new((Value, a), Kids.Select(k => k.Scan(a, step)));
    }

    /// <summary>Все поддеревья в прямом порядке: родитель раньше детей, дети — в порядке записи.</summary>
    public IEnumerable<Tree<T>> Subtrees()
    {
        yield return this;
        foreach (var k in Kids)
            foreach (var t in k.Subtrees()) yield return t;
    }

    /// <summary>Поддеревья вместе с родителем (у корня — null).</summary>
    public IEnumerable<(Tree<T> node, Tree<T> parent)> WithParents(Tree<T> parent = null)
    {
        yield return (this, parent);
        foreach (var k in Kids)
            foreach (var p in k.WithParents(this)) yield return p;
    }

    /// <summary>Заменить ПЕРВОЕ сверху поддерево, где `at` истинно, на `with`(оно). Внутрь заменённого не заходит;
    /// ветви, где замены не было, возвращаются теми же объектами — дерево делит их со старым.</summary>
    public Tree<T> Replace(Func<Tree<T>, bool> at, Func<Tree<T>, Tree<T>> with)
    {
        bool done = false;
        return Go(this);

        Tree<T> Go(Tree<T> t)
        {
            if (done) return t;
            if (at(t)) { done = true; return with(t); }
            Tree<T>[] kids = null;
            for (int i = 0; i < t.Kids.Count; i++)
            {
                var k = Go(t.Kids[i]);
                if (!ReferenceEquals(k, t.Kids[i]))
                {
                    kids ??= t.Kids.ToArray();
                    kids[i] = k;
                }
            }
            return kids == null ? t : new Tree<T>(t.Value, kids);
        }
    }

    /// <summary>Срезать поддеревья, чей корень не проходит `keep` (вместе со всеми потомками).</summary>
    public Tree<T> Prune(Func<T, bool> keep)
    {
        var kids = Kids.Where(k => keep(k.Value)).Select(k => k.Prune(keep)).ToArray();
        bool same = kids.Length == Kids.Count && kids.Select((k, i) => ReferenceEquals(k, Kids[i])).All(x => x);
        return same ? this : new Tree<T>(Value, kids);
    }

    /// <summary>Тот же узел с другими детьми.</summary>
    public Tree<T> WithKids(IEnumerable<Tree<T>> kids) => new(Value, kids);
}
