# Ловушки и читаемость процедурной анимации существ

Статус: первичный ресёрч, выполнен в одного агента (~20 вызовов поиска и чтения). Отдельные числа из руководств Monster Hunter, Dark Souls и For Honor в открытых источниках не найдены: помечено в «Gaps». Фреймы ниже — как в источниках; перевод в миллисекунды — арифметика (60 fps = 16,7 мс/кадр, 30 fps = 33,3 мс/кадр).

Важно для чтения: часть источников — форумы, wiki и маркетинговые страницы. Они помечены как вторичные.

---

## 1. Читаемость атаки: замах → удар → восстановление

### Takeaway
Публично доступных таблиц frame data по монстрам Monster Hunter, Dark Souls и For Honor в ходе поиска получить не удалось. Единственная проверяемая структура — фазы startup / active / recovery из файтингов, которую wiki Monster Hunter переносит на монстров. Принцип «замах читается, если видна фаза до удара» подтверждён косвенно; конкретных миллисекунд для action-игр нет.

### Cited Findings
- Monster Hunter Wiki определяет startup монстра как время от начала атаки до её начала, и называет его «wind-up»; recovery — время, в течение которого монстр не может начать другое действие после конца active-фреймов — [Monster Hunter Wiki, Template:MonsterDataAttacks](https://monsterhunterwiki.org/wiki/Template:MonsterDataAttacks)
- Поимённые данные атак монстров (startup/recovery по каждой атаке) лежат на страницах вида «<монстр>/Data» — [пример: Wind Serpent Ibushi (MHRS)/Data](https://www.monsterhunterwiki.org/wiki/Wind_Serpent_Ibushi_(MHRS)/Data). Сами числа в поиске не получены, поэтому конкретных значений здесь нет.
- Фазы startup / active / recovery описаны как общая терминология файтингов; у Street Fighter V recovery — окно после active-фреймов, пока персонаж открыт для удара — [Prima Games, SFV reading frame data](https://primagames.com/eguides/street-fighter-v-eguide/primer/reading-frame-data) (ссылка из выдачи поиска; текст страницы не открывался).
- У Dark Souls 2 параду измеряли по трём секциям (startup, active, recovery) и суммировали; автор отмечает отсутствие PvP-тестов — [Fextralife, Parry](https://darksouls2.wiki.fextralife.com/Parry) (вторичный источник, фан-wiki). Для атак врагов в Dark Souls числа не найдены.
- Для Dark Souls III в обсуждении Steam указано, что публичная таблица показывает только startup и active для парирования; таблица «атак в секунду» не даёт фазы — [Steam discussion](https://steamcommunity.com/app/374320/discussions/0/1290690926869297769) (вторичный).
- Соглашения о подсчёте расходятся: часть источников относит первый active-фрейм к startup, часть нет. Сравнивать числа из разных источников без проверки конвенции нельзя — [Frame Data Definitions, SvC Chaos glossary](https://srk.shib.live/w/SvC_Chaos:_SNK_Vs_Capcom/Glossary) (из выдачи поиска).
- Ubisoft (апрель 2018, пост о задержке ввода в For Honor): «стойко-ориентированные атаки и блоки должны быть максимально ясными и читаемыми» названо давним приоритетом дизайна; в тексте упомянуты 30 fps как базовая частота — [Ubisoft, Input delay in For Honor](https://www.ubisoft.com/en-us/game/for-honor/news-updates/4qgcoZf3m61lpWD8GlN4JO/input-delay-in-for-honor) (первичный источник заявления разработчика, но не исследование).
- Ubisoft на GDC 2016 (Simon Clavet, «Motion Matching and Road To Next-Gen Animation») описывает анимационную систему For Honor; по выдаче поиска, цель — точность геймплея и убедительная анимация, которые тянут в разные стороны. Прямой ссылки на сессию в GDC Vault поиск не дал; из выдачи есть японская заметка [Game Watch](https://game.watch.impress.co.jp/docs/news/749136.html) и вариант nucl.ai 2016 «Motion Matching For Realistic Animation in For Honor» (не открывался).
- Монстры Monster Hunter Wilds: разработчик назвал частый уход монстров в бете багом, а не дизайном — [VGC](https://www.videogameschronicle.com/news/monster-hunter-wilds-betas-frequently-moving-monsters-were-a-bug-director-says/). Прямой связи с читаемостью замаха это не даёт, но это пример того, что поведенческие «теллы» в игре ломаются от поведенческих багов.
- Разработчики Wilds описывают процесс от роли монстра в игре к его виду и поведению (Kaname Fujioka) — [Automaton](https://automaton-media.com/en/news/monster-hunter-wilds-new-monster-lala-barina-is-designed-to-encourage-players-to-vary-their-roles-in-multiplayer/) (косвенно; про телеграф не говорится).
- Hit stop в SSBWiki hitlag (он же hitstop, freeze frames) — заморозка атакующего и жертвы на несколько кадров при попадании; формула зависит от урона и серии игры. Пример из страницы: удар 15% урона даёт 10 кадров hitlag в Smash 64, 8 в Melee, 10 в Brawl/Smash 4, 15 в Ultimate — [SSBWiki, Hitlag](https://ssbwiki.com/Hitlag) (фан-wiki, вторичный; формулы в сниппете обрезаны).
- Hit-stop 1–3 кадра на сильных ударах упомянут как общая рекомендация в гайде, без первичного источника — [Bugnet, how to design enemy attack telegraphs](https://bugnet.io/blog/how-to-design-enemy-attack-telegraphs) (низкое качество, использовать только как пример формулировки).
- Vlambeer / Jonasson & Purho: «Juice It or Lose It» (Martin Jonasson, Petri Purho) — показ приёмов «сочности» (Nordic Game Jam 2012; по выдаче — GDC Europe 2012) — [GDC Vault, Juice It or Lose It](https://gdcvault.com/play/1016487/Juice-It-or-Lose). Talk Яна-Виллема Нейман «The Art of Screenshake» (Vlambeer, около 2013) — про screenshake: камера отбрасывается примерно на 6 px от точки прицела, тряска гаснет быстро — по сниппету выдачи, сама лекция не открыта. Про hit stop эти материалы в выдаче не подтверждены.

### Inferences
- Для процедурного тела читаемость замаха почти полностью живёт в фазе wind-up: это единственное время, когда игрок может отреагировать. Значит, процедурные атаки должны иметь ЯВНУЮ фазу замаха (отдельный параметр «длительность замаха», а не скорость удара). Это следует из определения startup в источниках выше, но конкретные значения для action-игр не получены.
- Hit-stop в кадрах плохо переносится на другие частоты: при 60 fps 10 кадров = 167 мс, при 30 fps = 333 мс. Поэтому хранить его надо в секундах (или в тиках фиксированного шага), а не в кадрах. Это вывод из арифметики и совпадает с замечанием из обсуждения, что кадровый hitstop ломается на высоких частотах (источник — Reddit, слабый).
- Экранная тряска как «сочность» дешевле, чем анимация реакции, и не мешает читаемости, если не маскирует замах. Вывод умозрительный.

### Gaps
- Конкретные фреймы startup / active / recovery для монстров Monster Hunter (MHWilds, MHRise) — в поиске не получены. Нужен прямой разбор страниц `/Data` вики или community frame-data таблица.
- Тайминги атак врагов Dark Souls и Hollow Knight — не найдены.
- Официальные GDC-лекции про телеграфы врагов (Death's Gambit readability, For Honor readability) — лекции не открыты, стенограммы не получены. Упомянутый «GDC-талк Death's Gambit» подтверждён только пересказом в Medium, а не записью.
- Правила процедурной читаемости (как процедурный замах сохраняет силуэт) — отдельных источников не нашлось. Общая рекомендация «замах должен быть в силуэте, а не только в таймлайне» — неподтверждена.

---

## 2. Силуэт и преувеличение

### Takeaway
Прямых источников по силуэтному преувеличению для процедурных существ не найдено. Есть один сильный косвенный пример: Team Ico / Shadow of the Colossus — размер воспринимается через время, эффекты, окружение и угол камеры, а не только через скорость движения.

### Cited Findings
- Разработчики SotC: «не было достаточно, чтобы они были медленными — нужно было выразить их размер» — [Siliconera, How Shadow of the Colossus Colossi came to life](https://www.siliconera.com/how-shadow-of-the-colossus-colossi-came-to-life/) (пересказ интервью, вторичный).
- Ощущение размера зависело от эффектов, декораций и ракурса камеры: «даже смена угла камеры могла влиять на восприятие» — там же.
- Колоссы сначала «смотрели в даль» и не реагировали на Wander; позже, в определённый момент, поворачивали шеи к нему — и именно тогда начинали казаться живыми — там же.
- Интервью Tanaka (genDESIGN): процедурная анимация использовалась для колоссов, и они намеренно были «жёсткими и без эмоций» — [genDESIGN, interview](https://gendesign.co.jp/qa_01/interview04en.html) (вторичная выдача поиска, текст не открывался полностью).
- Редактор Glitterberri: Agro-бег «переделывали бесчисленное число раз» — [Glitterberri, The Making of the Colossi](https://glitterberri.com/?p=3510) (вторичный).

### Inferences
- Для читаемости силуэта важнее амплитуда и медленность, но размер можно «продать» камерой и окружением. Это требует проверки в игре, а не в анимации.
- Жёсткость как характер (SotC) — приём, который экономит процедурные ресурсы: меньше степеней свободы — меньше шума. Вывод умозрительный.

### Gaps
- Нет источника про «hit-stop + силуэт» для процедурных существ.
- Нет количественных правил (например, сколько процентов амплитуды нужно для читаемости). Не найдено.

---

## 3. Реакции на попадание: flinch, stagger, knockback дёшево процедурно

### Takeaway
Подтверждённый дешёвый путь: аддитивный импульс + пружина (spring-damper) поверх keyframe-анимации, применяемый на LateUpdate, с параметрами stiffness / damping / per-bone limits. Конкретные цифры — из туториалов, их стоит перепроверить.

### Cited Findings
- Плагин Easy Procedural Hit Reactions (Unreal, UE 5.8, Control Rig) применяет попадания как аддитивные пружинные движения поверх анимации без отдельных hit-анимаций; параметры — damping, stiffness, mass, per-bone rotation limits; вход — точка удара, направление, кость, сила — [Epic forums, Kettunen](https://forums.unrealengine.com/t/kettunen-easy-procedural-hit-reactions/2741781) (сторонний плагин, Unreal, не Unity).
- Opsive: stiffness задаёт «жёсткость» пружины, damping отводит скорость при возврате в покой; слишком большой damping даёт быстрый покой, мало — дёрганье — [Opsive, Springs](https://opsive.com/support/documentation/ultimate-character-controller/animation/springs/) (вендорская документация, Unity-пакет).
- Туториал: для вторичного движения персонажей брать damping ratio около 0,5–0,8 и снижать от критического, пока не станет «живым» — выдача поиска, конкретный URL в выдаче: [gamejuice, procedural animation springs ik](https://gamejuice.pages.dev/articles/procedural-animation-springs-ik) (низкое качество, не проверено; число — только как стартовая точка).
- Рекомендация Unity-гайда: держать keyframe для продуманного движения, а процедурные пружины — слоем поверх; обновлять в LateUpdate, чтобы реагировать на финальную позу — тот же туториал (вторичный).
- Ragdoll-вспышка после удара с возвратом в keyframe — один из вариантов, описан в туториале без цифр.

### Inferences
- Для химерных тел (набор деталей) пружина на корне детали + импульс на удар даёт реакцию без единой hit-анимации на вид — это соответствует идее «тело = данные, поведение = слой». Вывод умозрительный, проверяется в игре.
- Пружина, применённая к корню тела, и пружина на каждой детали дают разные читаемости. Вывод: начинать с одной пружины на корне и добавлять по детали только там, где игрок должен видеть реакцию (голова, хвост).

### Gaps
- Не найдено опубликованных Unity-примеров «flinch / stagger» на процедурных существах с параметрами.
- Числа damping/stiffness из туториалов нельзя считать проверенными.

---

## 4. Модульное тело в движении: швы, скиннинг жёстких кусков, блендинг

### Takeaway
Жёсткий скиннинг (одна кость на вершину) даёт щели в суставах при повороте кости; решения — перекрытие/блендинг на стыке, либо отдельный инструмент (Simplygon Modular Seams, платный), либо запекание нормалей, которое скрывает шов. Для Unity прямых рекомендаций по сшивке модульных кусков в поиске не найдено.

### Cited Findings
- Rigid skinning привязывает каждую вершину к одной кости; вершина фиксирована в локальной системе этой кости — и это вызывает проблемы в суставах; исследования предлагают блендинг-патчи вокруг сустава — [PDF, skinning research (uoi.gr)](https://www.cse.uoi.gr/wp-content/uploads/publications/skinning.pdf) (академическая статья, полный текст не открывался).
- Rigid-компоненты «прекрасно работают для роботов, механических персонажей и транспорта, но не для органики» — тот же источник (по выдаче поиска).
- Simplygon Modular Seams: инструмент для выравнивания швов между взаимозаменяемыми частями на всех LOD; требует общих вершин на швах с допуском; платный — [Simplygon, Modular Seams](https://simplygon.com/features/modularseams).
- Практический приём: разрезать low-poly, склеить, запечь normal map с high-poly, разрезать обратно и скрыть шов запечённой картой — [Unreal forums, seams on multi-part skeletal character](https://answers.unrealengine.com/questions/221564/view.html) (форум, низкий вес).
- Bounds и culling: SkinnedMeshRenderer не пересчитывает bounds каждый кадр; при процедурной анимации это даёт ложное отсечение. Официальный ответ Unity (мануал 2021.2): с включённым Update When Offscreen bounds пересчитываются каждый кадр, цена — CPU на скиннинг невидимых мешей — [Unity manual 2021.2, SkinnedMeshRenderer](https://docs.unity3d.com/2021.2/Documentation/Manual/class-SkinnedMeshRenderer.html).
- Тот же мануал: `localBounds` задаёт AABB в локальном пространстве; можно вручную задать box, покрывающий худшую позу — [Unity manual, SkinnedMeshRenderer](https://docs.unity3d.com/ru/Manual/class-SkinnedMeshRenderer.html) (ссылка из выдачи).
- Форум Unity: при процедурном контроллере (LateUpdate меняет кости) bounds могут считаться неверно; Update When Offscreen иногда не помогает, в т.ч. при рендере только через command buffer — [Unity forums, Headlook/LateUpdate bone change screws up Culling](https://discussions.unity.com/t/headlook-lateupdate-bone-change-screws-up-culling/431302) (форум).

### Inferences
- Для химер и кусков мешей, которые «отходят от тела» при повороте, основной риск — не скиннинг как таковой, а несовпадение bounds с позой. Это значит: для процедурного тела нужно либо включать Update When Offscreen, либо задавать localBounds с запасом, и проверять отсечение на крайних позах.
- Если модульные куски жёсткие (rigid, без блендинга), швы будут видны на сгибах; смягчать их можно перекрытием деталей (overlap) на стыке, а не подгонкой геометрии. Вывод согласуется с общей практикой, но конкретных цифр перекрытия не найдено.

### Gaps
- Нет источника по foot sliding и interpenetration для процедурных существ в Unity.
- Нет источника по весовому блендингу на швах для Unity (только академическая статья).

---

## 5. Производительность: SkinnedMeshRenderer, LateUpdate-скрипты, LOD анимации, Burst

### Takeaway
Ключевые проверенные правила Unity: отключать анимацию вне видимости (Culling Mode = Cull Completely + Update When Offscreen = off), не анимировать scale-кривые, не держать лишние кости и IK-узлы, объединять меши в один SkinnedMeshRenderer. Встроенного LOD по дистанции для частоты анимации нет: его делают скриптом или сторонним пакетом. Burst/Jobs для анимации — низкоуровневый Animation Jobs API.

### Cited Findings
- Unity рекомендует: «Always optimize animations by setting the animator [Culling Mode] to Cull Completely, and disable the [skinned mesh renderer's] Update When Offscreen property. This saves Unity from updating animations when the character is not visible.» — [Unity 6.3 docs, Mecanim performance](https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/mecanim-peformanceand-optimization)
- Scale-кривые дороже translation и rotation; constant-кривые дешевле — тот же мануал.
- Layer с весом 0 пропускается целиком — тот же мануал.
- Разбиение персонажа на несколько SkinnedMeshRenderer снижает производительность; лишние кости стоят CPU (пример: 15 лишних костей на 30 дали +50% времени solve в Generic) — [Unity 2017.2 docs, Modeling optimized characters](https://docs.unity3d.com/2017.2/Documentation/Manual/ModelingOptimizedCharacters.html) (старая версия, но правила, по-видимому, актуальны — проверить).
- Оптимизации visibility culling и bounding volume работают только при одном Animation component и одном SkinnedMeshRenderer — тот же источник 2017.2.
- Старый баг (Unity 5.2 beta): стоимость Rendering.UpdateDirtyRenderers растёт с числом SkinnedMeshRenderer; помечен исправленным — [Unity issue tracker](https://issuetracker.unity3d.com/issues/renderer-rendering-dot-updatedirtyrenderers-skinned-mesh-renderers-cost-lots-of-cpu-power) (историческая справка).
- Режимы Animator Culling: AlwaysAnimate; CullUpdateTransforms (без retargeting, IK и записи трансформов при невидимости); CullCompletely — [Unity script reference, AnimatorCullingMode](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/animatorcullingmode.md).
- Встроенной частоты обновления анимации по дистанции в мануале нет; в официальном примере переключение Culling Mode делается в Update по порогу дистанции до камеры — [Unity forums, How does Animator Culling work?](https://discussions.unity.com/t/how-does-animator-culling-work/176574) (форум; пример из выдачи).
- Сторонний пакет «Animator LOD System» (Asset Store) обещает обновление каждые 2/4/8 кадров по уровням дистанции — не проверено, [Asset Store listing](https://assetsale.herokuapp.com/en/contents/152652) (ненадёжный зеркальный адрес, использовать как указатель на название, не как источник).
- Burst/Jobs: Animation C# Jobs расширяет Playable API job-системой (примеры — LookAt, TwoBoneIK); это низкоуровневый API — [Unity blog, Animation C# Jobs](https://unity.com/ru/blog/engine-platform/animation-c-jobs). Animation Rigging — отдельный пакет, связь с Burst в выдаче не подтверждена.
- Затраты на solve пропорциональны скелету: число костей, constraints, число вершин — форум; «тяжёлое, но частое вычисление в Burst работает во много раз быстрее» — [Unity forums, Animator options: how to render hundreds of animations efficiently](https://discussions.unity.com/t/animator-options-how-to-render-hundreds-of-animations-efficiently/860519) (форум, мнение).
- Zugalu (Thrive) — запечённые анимации делят общие данные для одинакового скелета, но runtime IK при этом теряется — [Unity, Zugalu case study](https://unity.com/resources/zugalu-entertainment-thrive-heavy-lies-the-crown). GPU-skinning crowd-пакеты указывают, что процедурный IK не поддерживается (кости запечены в статический массив) — из выдачи поиска, конкретный пакет не назван.

### Inferences
- Для десятков существ: процедурные LateUpdate-скрипты по костям — дорогой путь, потому что каждый скрипт работает над костями отдельно. Вывод: держать процедурную часть на небольшом числе костей (голова, хвост, 2–4 цепочки), остальное — keyframe или запечённое.
- Для дистанционной частоты: писать свой «тик» анимации (например, каждые N кадров для дальних существ), а не полагаться на встроенный culling — это умозрительно, без цифр из источников.
- Для химер с переменным набором деталей запекание разрушает переиспользование; вывод — процедура обязана жить в коде поверх одного общего скелета.

### Gaps
- Нет официальных цифр: сколько мс стоит N SkinnedMeshRenderer или M процедурных костей на типовом ПК. Нужен замер в самом проекте (профайлер Unity).
- Нет проверенного опубликованного примера «animation LOD по дистанции» с цифрами ускорения.
- Ссылка на документацию Unity 6.3 мануала подтверждена только выдачей и одной выборкой; 2017.2-доку использовать с оговоркой.

---

## 6. Настроение и состояние ИИ через движение (осанка, походка, взгляд головы)

### Takeaway
Есть сильные примеры процедурной «телесности» (Rain World, Shadow of the Colossus), но ни один из найденных источников не даёт правил «агрессия → осанка X» с числами. Конкретные слова про «страх через походку» не подтверждены первичным текстом. Рабочий принцип, который следует из примеров: состояние передаётся через скорость, вес и паузы, а не через отдельные позы.

### Cited Findings
- Rain World: тело персонажа — две сферы, соединённые на фиксированном расстоянии; конечности и хвост — косметические оверлеи, которые процедурно двигаются от входа игрока — [Unity blog, Exploring procedural design in Rain World: The Watcher](https://unity.com/blog/exploring-procedural-design-rain-world). Joar (консультант по существам) говорит, что «никогда не ставил целью процедурную анимацию» — начинал со смеси классики и процедуры и потом перешёл в основном на процедуру.
- В той же статье тела и движение кодируются отдельно для каждого вида; «требуют уникального кода» — там же. Это прямо противоречит идее одного универсального процедурного контроллера.
- В статье НЕ описано, как AI-состояние влияет на позу или походку; модули (PreyTracker, ThreatTracker) задают приоритеты, но не механику позы — там же.
- В статье нет раздела о читаемости и настроении через движение — там же.
- GDC 2016 Animation Bootcamp, Rain World (Joar и James Therrien) — упомянуто в выдаче: Joar «размещает точки в пространстве и даёт им вести тело», а не рисует каждый кадр. Имя в выдаче написано неоднозначно, лекция закрыта подпиской GDC Vault и не открыта; использовать как упоминание, не как факт.
- Shadow of the Colossus: Tanaka (genDESIGN) — колоссы «жёсткие, тусклые и без эмоций», процедурная анимация использовалась для колоссов — [genDESIGN, interview](https://gendesign.co.jp/qa_01/interview04en.html).
- SotC: Wander, «раскачанный» на колоссе, — связка dual pendulum + add motion + IK, по описанию Microsoft developer blog — [Microsoft devblog, a really cool technical demo](https://learn.microsoft.com/en-nz/archive/blogs/torgo3000/a-really-cool-technical-demo) (вторичная выдача поиска; ссылка на описание в блоге, а не на технический разбор SotC).
- Для Monster Hunter Wilds поведение монстров определяется ролью и экологией (стая, среда) — см. [Gamereactor](https://www.gamereactor.eu/now-they-go-in-packs-capcom-details-how-open-world-and-creature-population-will-change-player-strategy-in-monster-hunter-wilds-1400333/) (интервью, прямых «поз-тэллов» нет).

### Inferences
- Настроение через движение работает прежде всего через «жёсткость/мягкость» и паузу, а не через отдельные позы: SotC прямо использует жёсткость как характер. Это соответствует тому, что пружинные слои (раздел 3) дают «живость» без новых клипов.
- Для химер: состояние ИИ можно передавать одним параметром (например, «напряжение» = damping ниже + амплитуда головы выше + частота походки выше). Это гипотеза; проверять в игре.
- Head-look и взгляд — дешёвый и сильный сигнал «заметил игрока»; вывод умозрительный, но в SotC колоссы «оживали», когда поворачивались к Wander.

### Gaps
- Нет первичного источника с правилом «страх/агрессия → походка» (с цифрами скорости или частоты шага).
- Нет разбора поведенческих тэллов Monster Hunter (например, предупреждающие позы перед рывком) из первичного источника.
- Нет источника по Hollow Knight, Dark Souls и The Last Guardian про состояние через позу (кроме упоминания в выдаче).

---

## Сводка: что можно использовать сейчас

1. Замах — отдельная фаза с явной длительностью (wind-up); процедурный удар без явного замаха читается плохо. Конкретных миллисекунд в источниках нет; ориентиры — только из фреймов файтингов и монстров без числовых значений.
2. Hit-stop и screenshake хранить в секундах, не в кадрах. Ориентир: Smash-формулы показывают, что hitlag растёт с уроном; конкретные значения брать из собственных замеров.
3. Реакции — пружина поверх keyframe на LateUpdate, один пружинный контроллер на корень, детали — только там, где нужна читаемость.
4. Швы: жёсткий скиннинг даёт щели в суставах; решать перекрытием на стыке. Bounds проверять на крайних позах или задавать localBounds с запасом.
5. Производительность: Cull Completely + Update When Offscreen off; не анимировать scale; ограничивать число процедурных костей; дистанционный LOD анимации — свой тик. Цифры — только замером в проекте.
6. Настроение — через жёсткость, вес и паузу; правила «состояние → поза» в источниках не найдены.

## Что открывалось и что нет
- Прочитано через WebFetch: Unity 6.3 Mecanim performance; Unity blog Rain World (Watcher); Siliconera (Colossi).
- Не открылось: Game Developer-статья про ARPG-читаемость (403); GDC Vault-лекции (платно, только листинги в выдаче); Simplygon, Bugnet и Asset Store-страницы проверены только по сниппетам.
- Перед использованием в решениях: цифры из Opsive, форумов и туториалов — перепроверить; Unity 2017.2 manual — правила по старой версии.
