# Памятка тестера — 05.09.2026

**Что крыть:** P0 `Cage SameTopology` (нет файлов), `BodyRules` ось, `Hit` 9 эффектов; P1 `Identity Σ=1`, `MostKin`, `Morph` тождественность; P2 `Telegraph`/`Evolution`/`CC`/`Destroy`.

**Врата (красный = не коммитить):** Test Runner EditMode+PlayMode зелёный (`Chimera.Tests.*.asmdef`), `КАРТА_ТЕЛ` 0 щелей (`Chimera → Выгрузить`), `audit.py` 0 замечаний (5/5 единый план), `check.py wolf`.

**Как гонять:** `Window → Test Runner → Run All` (EditMode отдельно), `C:\ProgramData\anaconda3\python.exe Anatomy/tools/audit.py` (Anaconda, не bare python). Тест рядом с фичей, `Data/*.asset` через `SpeciesBootstrap`.

**Сейчас:** 10+13 файлов в `Tests/`, P0-клетка дыра — завести `CageBlendTests`.
