# Трек 7 (производство и инструменты): бесплатные средства Blender для «скелет → массы → кожа», лицензии, стоимость

Заметка собрана 30.10.2026 по ~15 вызовам поиска/чтения. Покрытие неравномерное: объёмы SDF и лицензии MakeHuman/Anny/Houdini подтверждены; мышечные аддоны, Surface Deform/Corrective Smooth в деталях, стоимость вида в человеко-днях — слабо (см. «Пробелы»). Всё, что не подкреплено ссылкой, вынесено в «Выводы» как моё рассуждение, а не факт.

## (a) Бесплатные способы построить тело из объёмов на костях в Blender; мышечные аддоны; растяжка масс между костями

### Takeaway
В Blender 5.x появился штатный набор SDF/volume-узлов в Geometry Nodes (сеточные volume grid, булевы операции, фильтры SDF), что даёт чистый открытый способ «объёмы → сплавленная водонепроницаемая оболочка». Готовой бесплатной открытой мышечной системы, подходящей под наш конвейер, найти не удалось; Tissue — GPL, но лицензионный статус на платформе расширений неоднозначен.

### Cited Findings
- В Blender 5.0 введён тип данных volume grid с отдельным сокетом; 5.0 добавляет SDF-моделирование и узлы для жидкостей: advection, булевы операции, векторное исчисление (curl, gradient), различные SDF-фильтры. — [Blender Code Blog, Volume Grids in Geometry Nodes](https://code.blender.org/2025/10/volu)
- Про новые узлы: 27 новых узлов в волюмной работе 5.0 (по CG Channel). — [CG Channel, Blender 5.0](https://www.cgchannel.com/2025/11/blender-5-0-is-out-check-out-its-5-key-features)
- В руководстве Blender раздел Volume Nodes (5.1/5.2/5.3) перечисляет Volume to Mesh, Grid to Mesh, Grid to Points, SDF Grid Boolean, SDF Fillet, SDF Grid Laplacian, SDF Grid Mean. Сглаживание SDF — SDF Grid Mean. — [Blender Manual 5.1, Volume Nodes](https://docs.blender.org/manual/en/5.1/modeling/geometry_nodes/volume/index.html); [Manual 5.2 (de)](https://docs.blender.org/manual/de/5.2/modeling/geometry_nodes/volume/index.html)
- Ограничения по официальному блогу: сложение двух сеток требует одинакового transform; не всё ещё открыто в узлах «из-за нехватки времени»; SDF как хранение детальных форм менее эффективен, чем меш. — [Blender Code Blog](https://code.blender.org/2025/10/volu)
- Узел «Mesh to SDF Grid» упомянут в боковом списке операций над мешем в руководстве Blender 5.2 (подтверждено только наличие в оглавлении, описание входов не прочитано). — [Blender Manual, Mesh to SDF Grid](https://docs.blender.org/manual/en/latest/modeling/geometry_nodes/mesh/operations/mesh_to_sdf_grid.html)
- BlenRig — GPL (Blender Studio: «BlenRig is GPL»); версия 5 публично выпущена в январе 2016 (автор Juan Pablo Bouza); сторонний агрегатор называет GPL-3.0. Текущей поддержки под Blender 4/5 не найдено. — [Blender Studio, BlenRig](https://studio.blender.org/training/blenrig/5cb14ebd808c0e07cdde1d03); [BlenderNation 2016](https://www.blendernation.com/2016/01/14/blenrig-public-release/); [kandi (агрегатор)](https://kandi.openweaver.com/python/jpbouza/BlenRig)
- Tissue: в Blender Extensions значится GPL-2.0-or-later (v0.3.70/0.3.71), автоодобрен как бывший штатный аддон 4.1, не рецензирован; платформа требует GPL-3.0-or-later для новых обновлений. — [extensions.blender.org/add-ons/tissue](https://extensions.blender.org/add-ons/tissue/); [Manual, Extension licenses](https://docs.blender.org/manual/ru/dev/advanced/extensions/licenses.html)
- Surface Deform: цель не должна содержать вогнутых граней, совпадающих вершин, коллинеарных рёбер; без bind эффекта нет; чем дальше меш от цели, тем больше артефактов; Sparse Bind требует перепривязки при добавлении вершин в группу; привязка в мировых координатах. — [Blender Manual, Surface Deform (es)](https://www.blender.org/manual/es/modeling/modifiers/deform/surface_deform.html)
- Баг-репорты: bind Surface Deform из Python давал «context incorrect» (позже решён); привязка тяжёлых мешей может надолго подвешивать UI. — [T50830](https://developer.blender.org/T50830); [#63252](https://projects.blender.org/blender/blender/issues/63252)
- Поиск открытого GPL-аддона «мышечной системы» для Blender 4.x ничего не дал (найден лишь платный GPL-аддон Shapekeys Extended, не мышцы). — [Shapekeys Extended, Gumroad](https://cascadian.gumroad.com/l/shapekeys-extended)

### Inferences
- (Моё рассуждение, не источник.) Минимальный конвейер в чистом Blender: (1) скелет-арматура с именованными костями из JSON; (2) на каждую из 12 масс — примитив/метабол/кривая-трубка, родитель — кость (или две кости: «начало» и «прикрепление»); растяжку держать ограничениями Stretch To / Damped Track + Copy Location между двумя empty-метками на костях, либо считать масштаб массы в Geometry Nodes из позиций двух костей (детерминированнее, чем драйверы); (3) в Geometry Nodes: Mesh to SDF Grid для каждой массы → SDF Grid Boolean (union, со сглаживанием) / SDF Fillet → Grid to Mesh с фиксированным шагом сетки (даёт водонепроницаемую поверхность); (4) децимация/гранение: Decimate (planar/collapse) или Voxel Remesh + Planar, затем триангуляция; (5) веса: Robust Weight Transfer с исходных масс или Automatic Weights/Data Transfer от масс по костям; (6) экспорт FBX. Узкие места, которые следует проверять: ≤4 веса на вершину в Unity, шаг сетки vs. бюджет 2–2,5 тыс. тр, потеря острых граней после SDF (нужно доведение), совместимость transform сеток.
- Тип «пружинящей» растяжки масс между origin и insertion можно получить без мышечных аддонов и драйверов: масса параметризуется двумя точками, и узел Geometry Nodes строит её вдоль отрезка; это дешевле и воспроизводимее, но подтверждающего источника у меня нет.
- Мышечные системы (BlenRig-мышцы, Ziva-подобные) для нашей задачи избыточны: анатомия нужна как способ авторинга, не рантайм-физика.

### Gaps
- Не подтверждены: точные имена и входы узла Mesh to SDF Grid, в какой версии он появился, не помечены ли volume-узлы экспериментальными (запросы вернули оглавление, не страницы узлов). Проверить в 5.2 руководстве и release notes Geometry Nodes 5.0.
- Не найден репозиторий BlenRig и его статус поддержки в 2026; не проверено, работает ли он в Blender 5.x.
- Не найдено ни одного открытого поддерживаемого мышечного аддона Blender (поиск был поверхностным; Muscle Tools, Metaball-мышцы, MuscleSimulator и т. п. не проверялись).
- Не исследованы: Metaballs в 5.x (поведение, headless-стабильность), Voxel Remesh, Corrective Smooth, Shrinkwrap — отдельных источников не прочитано.
- Не найдена информация о том, что работает ли Robust Weight Transfer / Pose Shape Keys детерминированно в headless-режиме.

## (b) Другие бесплатные средства: MakeHuman/MPFB, Anny, Houdini Apprentice, libigl/CGAL

### Takeaway
MPFB/MakeHuman и Anny дают человеческий параметрический базис с коммерчески свободной лицензией (кроме топологии smplx у Anny); для зверей они не подходят. Houdini Apprentice — только некоммерческий, в продукт не годится; Indie — платный.

### Cited Findings
- MakeHuman/MPFB: базовые ассеты (меш, targets, скины) под CC0; код под GPLv3; проект заявляет, что не претендует на экспортированные модели, рендеры, скриншоты; можно продавать созданные модели. — [MakeHuman license](https://static.makehumancommunity.org/about/license.html); [MPFB FAQ: can I sell models](https://static.makehumancommunity.org/mpfb/faq/can_i_sell_models.html)
- Оговорка: сторонние ассеты с другой лицензией — на ответственности пользователя; на форуме Unity высказана неоднозначность формулировки для 3D-выходных данных (мнение одного пользователя, не официальное). — [Unity Discussions, Makehuman license](https://discussions.unity.com/t/makehuman-license/639763)
- Anny (NAVER): код Apache-2.0 (copyright 2025); ассеты MakeHuman (из MPFB2) — CC0; топология SOMA — Apache-2.0; топология SMPL-X — ТОЛЬКО некоммерческое использование, скачивается отдельно; установка `anny[smpl,examples]` тянет несвободные зависимости. — [naver/anny](https://github.com/naver/anny); [arXiv 2511.03589](https://arxiv.org/html/2511.03589v2)
- Anny: риг по умолчанию 104 кости, полный MakeHuman — 163; параметры phenotype в [0,1], local_changes в [-1,1]; версии v0.1 (2025-11-05) … v0.6 (2026-08-06); мышц нет, описания экспорта в Blender нет (экспорт PLY через trimesh). — [naver/anny](https://github.com/naver/anny)
- Houdini Apprentice: только некоммерческие проекты; нельзя в одном конвейере с коммерческим Houdini; водяной знак на рендерах, сторонние рендереры запрещены; лицензия на 30 дней с автопродлением (лимит разрешения рендера — 1920×1080 по японской версии FAQ, 1280×720 по вторичному источнику, расхождение). — [SideFX Apprentice FAQ](https://sidefx.com/faq/apprentice); [SideFX Apprentice restrictions](https://www.sidefx.com/faq/question/435/)
- Houdini Indie: ограниченное коммерческое использование — годовой оборот ≤ 100 тыс. USD (по более старому посту форума), собственный формат файлов; платный. — [SideFX Indie FAQ](https://sidefx.com/faq/indie-new); [SideFX forum](https://www.sidefx.com/forum/post/155151/?page=2)

### Inferences
- Apprentice отпадает по жёсткому правилу проекта «без non-commercial»; Indie — платный, значит тоже отпадает. Houdini как идея (процедурный граф) уже покрывается Geometry Nodes.
- Человеческий базис (Anny/MPFB) полезен как источник пропорций и скелета человека, но не решает зверей и химер; вопрос нужен ли он — к геймдизайнеру.

### Gaps
- libigl и CGAL Python-bindings: лицензии (libigl — MPL-2.0, CGAL — GPL/коммерческая двойная лицензия по моей памяти) источниками не проверены; пригодность CGAL под GPL для инструмента авторинга, чьи выходные меши идут в игру, не уточнена.
- Не проверено, работает ли Anny в Blender (аддон) без torch и как его выход конвертируется в FBX.

## (c) Стоимость производства, автоматизация, детерминизм

### Takeaway
Чёткой цифры «человеко-дней на существо» из открытых источников не получено: данные — только порядки (часы…месяцы) и бюджеты треугольников. Детерминизм конвейера опирается на наши внутренние практики (headless Blender, JSON), внешних подтверждений собрано мало.

### Cited Findings
- По RuneScape (developer diary): маленький предмет — от часов до нескольких дней, полный комплект брони или малый NPC — недели, существо во много раз больше игрока — месяцы (от концепта до анимации); сложные риги вредят производительности, поэтому риги простые. — [RuneScape Dev Diary, Meet The Art Team Part 2](https://secure.runescape.com/m=news/developer-diary-meet-the-art-team-part-2)
- Ориентиры бюджетов полигонов (разрозненные источники): ~500–2k тр для мелкого окружающего существа, 2k–8k для боевого (стайл-гайд MMO); 30–40k тр для босса (одиночный проект); 1,5–3k полигонов (ретро-хоррор). — результаты поиска, см. [Kylar's Postmortem](https://grimsley7452.itch.io/workers-comp/devlog/915802/kylars-postmortem) и [Rookie Awards entries](https://www.therookies.co/entries/20103) (источники вторичные, разнородные)
- Личный челлендж «от концепта до внедрённого персонажа за неделю» — единичные данные, не норма. — результат поиска, см. [Creative Bloq, 18 tips](https://www.creativebloq.com/how-create-character-models-games-18-top-tips-9113050)

### Inferences
- Наш бюджет 2–2,5 тыс. тр лежит в нижнем диапазоне «мелкое/боевое существо»; значит основная стоимость у нас — не моделирование, а доводка формы по листу, риг и приёмка (ревью и критик), а не количество треугольников.
- Автоматизируемо (по логике конвейера, без внешнего подтверждения): сборка масс и кожи по JSON, веса, экспорт, измерение IoU по маскам, проверка бюджетов/имён костей/≤4 весов, регрессия кадров. Остаётся за человеком: выбор силуэта и вкус, подтверждение листа.
- Детерминизм: фиксировать версию Blender, запускать `blender -b` со скриптом без UI, версионировать JSON параметров, шаг сеток SDF и сиды задавать явно, сравнивать результат по хэшу вершин/метрикам (допуск), хранить один источник правды (урок проекта о двух источниках). Это практики, не подтверждённые найденными источниками.
- Масштаб 5→15 видов: растут пары «куски × органы × планы» и ключи — на это отдельных источников не искал.

### Gaps
- Нет замеров на r11, волке, человеке, лосе из внутренней истории (их надо брать из логов/коммитов проекта, не из внешнего поиска).
- Нет публичных постмортемов с человеко-днями на существо (GDC/Gamasutra не просматривались); значит cost model строить на внутренних замерах.
- Не проверены: детерминизм вывода Geometry Nodes/SDF между запусками и версиями Blender; лицензионные вопросы shipping-вывода Blender (по общеизвестной практике выход Blender — собственность автора, но источник здесь не получен).
- Лицензии Robust Weight Transfer и Pose Shape Keys не проверялись в этом заходе.
