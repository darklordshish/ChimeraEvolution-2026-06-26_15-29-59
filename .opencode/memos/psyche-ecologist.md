# Памятка лесника (психики) — 05.09.2026

**Психики:** `WolfPsyche` стая `PackCoordinator`, `SnakePsyche` засада/камуфляж/яд/обхват, `MoosePsyche` таран/рёв, `HedgehogPsyche` залп/Thorns, `Werewolf` босс, `ChimeraAlpha` фолбэк. Диспатч `PsycheDispatch` по `MostKin`, `Regard` по доминанте.

**Эволюция:** `убийство → CreditKiller +0.55/орган → TryChimerize родство/100 → Metamorph Medium 0.85` (`CreatureBody.Evolution`, `EvolutionConfig` в сцене). `AffinityCap` 100, ≥75 босс, ≥100 химерный слот. Шасси не меняется.

**Три оси:** статы (`Recompute` + `IBodyStatConsumer`), психика, модель (`MorphBuilder`, ось от головы, 81 кость волка). Детекторы `КАРТА_ТЕЛ`.

**Лес:** RPS ёж→змея→волк, `Satiety` Homogeneity, `Senses` 5 осей, `PackCoordinator`/`ForageSpawner`.
