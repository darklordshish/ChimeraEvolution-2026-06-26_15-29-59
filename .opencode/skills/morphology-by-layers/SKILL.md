---
name: morphology-by-layers
description: Use when building or fixing CHIMERA creature bodies — SpeciesSO sockets, bones, muscles, covers, cage M×N. Enforces layer workflow and tandem rule.
---

# Morphology by Layers (CHIMERA)

Процесс рождён после 55 коммитов за 2 недели и 4 переделок волка за день (`УКАЗАТЕЛЬ.md:5.5`).

## Слои (спека 2026-08-21, CLAUDE.md:35)
1. **Кости** — `SpeciesSO.bones` → `SkeletonBuilder` → `Bone`, 81 кость у волка, ось от черепа. Источник — `Anatomy/species/<вид>/etalon/`.
2. **Мышцы** — поверх костей, `SpeciesSO.buildLayers=2`.
3. **Покровы** — `buildLayers=3`.

Каждый слой принимается ОТДЕЛЬНО скрином строго в профиль крупно, один зверь (`CLAUDE.md:30`). Дефект — «где и насколько» («голова ниже шеи на полголовы»), не «оторвана».

## Правила
- Эталон СНИМАЕТСЯ с референса-картинки, не выдумывается (был зеркальный эталон — «схождение 2.1 см» с выдумкой).
- Тело правится только из `Anatomy/` (`Anatomy/README.md`), не подгонкой чисел в `SpeciesBootstrap` (`CLAUDE.md:12`). После правок — `Chimera → Создать дефолтные виды`.
- Части раздаются парам агентов по слотам, собирает и проверяет швы ревизор (`morphologist`).
- Если после 2 итераций причина не найдена — честно «со скриншота не вижу, нужен ракурс X», не гадать.
- Длинная ось по `baseSize` — запас ≥5% (`CLAUDE.md:49`), масштаб родителя =1 иначе плющит (`CLAUDE.md:51`), торец капсулы — шар (`CLAUDE.md:52`).

## Инструменты
`Anatomy/tools/{skel,speciesdata,render,grid,audit}.py` (Anaconda, `C:\ProgramData\anaconda3\python.exe`), `Tools/Blender/` (клетка), `MorphBuilder.axisOf`.
