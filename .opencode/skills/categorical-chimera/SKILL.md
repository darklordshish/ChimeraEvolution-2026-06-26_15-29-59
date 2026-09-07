---
name: categorical-chimera
description: Use when viewing CHIMERA architecture through category theory — functors, colimits, DSL, effect algebras. Proposes testable ideas, not refactors for beauty.
---

# Categorical Chimera

CHIMERA — уже категорна, просто не названа.

## Словарь проекта → категорный
| проект | категорно |
|---|---|
| `SpeciesSO` видов | объекты категории `Species` |
| `CreatureBody` сборка | функтор `Species → Assembly` |
| Графт `Organ.slot` | морфизм, `SpeciesSO.sockets` — копродукт |
| Клетка `M×N×3` (`SPEC-kletka`) | тензор, химера = ко-лимит (среднее таблиц) |
| `WindupAbility`+`HitEffect` | free monad + интерпретаторы |
| `PsycheDispatch` по доминанте | копродукт коалгебр, `Metamorph` — смена коалгебры |
| Три оси (`CLAUDE.md:64`) | три функтора из `Composition` |

## Правила
- Каждая идея — **спека-слайс**, не рефактор всего. Формат: проблема → категорная модель → C#-эскиз → цена/выгода → что мерить детектором.
- Уважай закрытое (`СТАТУС.md:164-172`): клетка-меш, ось от головы, смешение после чистых — не переоткрывать.
- Рисуй `categorical-diagrams` (палитра, ASCII-only, выноси SVG), ставь `source-anchors` (★ — свой синтез).
- Проходи YAGNI-фильтр `orchestrator`: есть ли 2 носителя? Чинит ли щель/дрейф? Сериализуется ли в Unity?

Связан: `spec-first` (оформление), `chimera-validation` (проверка), `docs-sync` (куда записать).
