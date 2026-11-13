# Техники процедурной анимации существ и бесплатные Unity-инструменты

Заход ресёрча, 12.11.2026. Цель: техники и бесплатные/открытые инструменты для процедурной анимации существ на скелетах, которые собираются в рантайме из графа (кости по имени и роли), пересобираются при графтах, ~45 существ в кадре, low-poly, Unity 6.

Условие проекта: платное не берём (решение геймдизайнера, «Без платного»). Платные инструменты отмечены и не рекомендуются.

Отдельная оговорка по источникам. Поиск дал ссылки на документацию и репозитории, но полные тексты многих руководств открыть не удалось. Утверждения о самих алгоритмах (IK, пружины, gait) ниже помечены как **общеизвестное, без источника в этом заходе** — их стоит проверить по первоисточнику, прежде чем на них опираться.

---

## 1. Техники

### 1.1 Процедурная локомоция с IK (постановка стопы, триггер шага, походка)

**Идея (общеизвестно, без источника в этом заходе).** Стопа «прибита» к точке на земле, пока нога в опоре. Когда расстояние от текущей точки стопы до целевой точки (проекция тела плюс смещение) превышает порог, запускается шаг: стопа переходит в новую точку по дуге. Шаги разных ног связываются фазами (phase offset): у двуногого — попеременно, у четвероногого — диагональными парами или «галопом», у многоногого — волновой походкой, где ноги в цепочке стартуют со сдвигом. Решатель IK на каждую ногу (two-bone для ноги с коленом, FABRIK/CCD для длинной цепи) вычисляет углы суставов.

**Что подтверждено источниками:**
- Группа техник по ногам существ и «полу-процедурной» локомоции описана в докладе Rune Skovbo Johansen (GDC 2009, «Dynamic Walking with Semi-Procedural Animation»), диссертация доступна: https://runevision.com/thesis/rune_skovbo_johansen_thesis.pdf ; слайды: https://blog.runevision.com/2009/03/locomotion-system-gdc-2009-slides.html
- Доклад по квадрупедам в *The Flame in the Flood*: процедурный позвоночник у волка против жёсткого у кабана, цель — «AAA-качество квадрупедной локомоции быстро»: https://gamedeveloper.com/art/video-animating-pups-boars-other-quadrupeds-in-i-the-flame-in-the-flood-
- Доклад Ubisoft «Fitting the World: A Biomechanical Approach to Foot IK» (Clifford Roche, Carlos Torres-Cros) — постановка стопы на неровном рельефе: https://gdcvault.com/play/1023009/contactUs (страница GDC Vault, тезисов в выдаче нет)
- Доклад GDC 2016 (Tobias Karlsson, Microsoft) про четвероногих «простыми и понятными» техниками, для аниматоров: упоминается в выдаче, тезисов не получено.
- Академическая работа: real-time генерация локомоции многоногих без motion data в динамической среде, HAL: https://hal.archives-ouvertes.fr/hal-01352977

**Вывод для нас.** Волк и лось — четвероногие, человек — двуногий, у ежа и змеи ходовой части по сути нет (у змеи цепь звеньев). Фазы походки должны задаваться по данным (число ног и их порядок в графе), а не зашиваться под два-четыре сустава, чтобы химера с иным числом ног не ломала походку.

### 1.2 Позвоночник и хвост: follow-the-leader, Verlet, пружины

**Общеизвестно, без источника в этом заходе:** цепочка «follow-the-leader» — каждый сустав подтягивается к позиции предыдущего на фиксированную длину сегмента (проход от головы к хвосту и обратно). Verlet-интеграция даёт гибкость без ConfigurableJoint. Для змеи в нашем проекте уже есть `SnakeBodyChain` — звенья-блоки на оси цепи, решение по цепи уже принято, тут менять не нужно.

**Подтверждено выдачей:** FABRIK (solver у Chain IK в Animation Rigging) — см. раздел 3.

### 1.3 Вторичное движение: демпфированные пружины, jiggle

**Общеизвестно, без источника в этом заходе:** критически демпфированная пружина `x'' = -k(x - target) - c x'` на каждый сустав/кусок (уши, хвост, грива, брюхо) даёт инерцию и «отставание» без IK. Дёшево: одна пружина на кусок, без физики. Подходит для деталей, которые не несут опору.

### 1.4 Пособы поз: pose-to-pose и интерполяция

**Общеизвестно, без источника в этом заходе:** набор ключевых поз (например, «idle», «walk-фаза-0…N», «угроза», «атака») и интерполяция между ними по параметру (скорость, фаза). Процедурно — ключи строятся по скелету графа: поза = смещения по именам костей, а не клипы на конкретный риг. Это совместимо с тем, что скелет собирается в рантайме.

### 1.5 Active ragdoll / physics-based animation (PD-контроллер через ConfigurableJoint)

**Подтверждено источниками:**
- Механика: на каждом кости — ConfigurableJoint с угловым приводом (angular drive), целевой вращение задаётся из анимированной копии. Так работает `Hairibar.Ragdoll`: две иерархии — Target (анимированная, с мешем) и Ragdoll (физическая, без мешей), каждый симулируемый кость получает ConfigurableJoint к родителю, на каждом шаге физики силы тянут Ragdoll к позе Target, перед рендером поза Ragdoll копируется в Target: https://github.com/hairibar/Hairibar.Ragdoll
- Официальная справка по ConfigurableJoint: https://docs.unity3d.com/Manual/class-ConfigurableJoint.html
- `sergioabreu-g/active-ragdolls` — модульная архитектура (базовый ActiveRagdoll + независимые модули + поведенческий скрипт на родителе), angular drive на суставах: https://github.com/sergioabreu-g/active-ragdolls
- Unity Discussions: в Unity Physics (DOTS) нет эквивалента многоосевого drive у ConfigurableJoint, поэтому active ragdoll там сложнее: https://discussions.unity.com/t/joint-motor-more-than-one-axis/904353

**Оговорка:** явного PD-контроллера с gains и тюнингом в источниках не нашлось — только «angular drive» без цифр. Баланс (self-balancing) в найденных репо не описан подробно.

**Вывод для нас.** Физический active ragdoll на 45 существах дорог и даёт непредсказуемое поведение в бою; для ~45 существ нужен дешёвый «кинематический» путь. Active ragdoll уместен разве что для смерти/падения, не для ходьбы.

### 1.6 Ретаргетинг по ролям (семантические риги)

**Что найдено:** Mecanim Humanoid требует сопоставления костей с ролями. Страница документации Unity (`UsingHumanoidChars`) перенаправила на новый адрес, а его вытащить не удалось — **по этому пункту источника нет, статус «не проверено»**. Из общего знания (без источника в этом заходе): Humanoid рассчитан на двуногий скелет с фиксированным набором ролей; для нечеловекообразных существ используют Generic rig, где ретаргетинга по ролям нет. Ретаргетинг по ролям у нас свой: кости найдены по имени из графа — это и есть семантический риг, его можно делать своим кодом.

### 1.7 Аддитивные слои (настроение, личность)

**Общеизвестно, без источника в этом заходе:** аддитивная поза (delta от базовой) поверх локомоции — например, «напряжение», «страх» — через AnimationLayerMixer / additive-клип или прямой множитель смещения. Удобно: слой психики (`Telegraph`, `EmotionTint`) сигнал, а не кость, выставляет амплитуду.

---

## 2. Unity-инструменты и API

### 2.1 Animation Rigging (официальный пакет, Unity)

- Версия: 1.3.1 (по странице документации). Совместим с Unity 2023.2 и новее: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html
- Состав: TwoBoneIK, ChainIK (FABRIK), Damped Transform, Multi-Aim и др. Подтверждено API-справкой: `TwoBoneIKConstraint` (один на GameObject, есть hint-трансформ), `ChainIKConstraint` / `ChainIKConstraintJob`, `AnimationRuntimeUtils.SolveFABRIK`: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/api/UnityEngine.Animations.Rigging.TwoBoneIKConstraint.html , https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/api/UnityEngine.Animations.Rigging.AnimationRuntimeUtils.html
- Работает на Animation Jobs + RigBuilder (создаёт PlayableGraph для Animator): https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.0/manual/ConstraintComponents.html
- **Можно ли собрать на рантайм-скелете:** теоретически да — констрейнты это компоненты, их можно добавить `AddComponent` и привязать трансформы из графа. **Не подтверждено источником:** в найденных страницах нет описания построения риг-иерархии из кода. Нужна проверка на прототипе.
- **Лицензия:** на странице пакета не указана; в выдаче только общая Unity Companion License (условия зависят от наличия валидной лицензии Unity). **Точный текст LICENSE.md пакета не проверен** — открыть файл в установленном пакете перед принятием.
- Производительность: официальных цифр нет. Есть жалобы на тяжесть на Quest 2: https://discussions.unity.com/t/performance-of-animation-rigging-is-very-rough-on-quest-2/859595 (это форум, цифр нет в выдаче).
- **Минус для нас:** пакет завязан на Animator + RigBuilder + PlayableGraph на каждое существо. На 45 существах это 45 графов. Наш скелет не Humanoid, и тяжесть на каждый констрейнт — риск.

### 2.2 Playables API и Animation C# Jobs

- Animation Jobs (C# Jobs для анимации) появились в 2018.2 и опираются на Playable API; три типа: AnimationScriptPlayable, IAnimationJob, AnimationStream. Unity сам пишет, что это низкоуровневый API, требующий понимания Playable API: https://unity.com/es/blog/engine-platform/animation-c-jobs
- Ограничение Burst: в job нельзя managed-объекты (GameObject и его компоненты), данные переносятся в NativeArray. Источник: пересказ в https://epheria.github.io/en/posts/UnityJobSystemBurst.en (из выдачи поиска, страница не открыта целиком)
- **Известная проблема:** в трекере Unity — «several animation tasks are scheduled on the main thread instead of job workers»; статус противоречивый (есть исправление в 2023.1 и отметка «Won't Fix»). Нужно перепроверить в нашей версии 6000.4 до того, как на это полагаться: https://issuetracker.unity.com/issues/425/several-animation-tasks-are-scheduled-on-the-main-thread-instead-of-job-workers
- Цифры производительности: **не найдены.** Отдельный пост: 2000 персонажей при ~30 fps, но узкое место было в pathfinding, а не в анимации: https://forum.arongranberg.com/t/ecs-followerentity-performance-issues/17235 (форум, пересказ, не измерение). Совет сообщества для толп — VAT (vertex animation textures) на GPU: https://esotericsoftware.com/forum/d/26185-question-regarding-unity-runtime-performance-for-many-characters
- **Вывод:** для толпы на 45 существах процедурные костные ноги на CPU — вопрос профайлинга, а не давно известной цифры. Вариант, не требующий Animator на каждом существе: свой код в `LateUpdate` на Transform'ах (без Playable, без Animator). Это проще и предсказуемее при рантайм-скелетах.

### 2.3 Kinematica

- Статус: **не подтверждён.** Документация Kinematica показывает только preview-версии до 0.8 с предупреждением «не безопасно для продакшена»: https://docs.unity3d.com/es/2020.2/Manual/com.unity.kinematica.html
- Общие правила Unity: experimental/preview пакеты могут переходить в deprecated без выпуска (https://docs.unity3d.com/2022.2/Documentation/Manual/upm-lifecycle.html). Прямого подтверждения, что Kinematica deprecated, нет. **Не использовать** в любом случае: preview, и отсутствие поддержки — достаточное основание.

---

## 3. Открытые репозитории (проверено по страницам)

| Репо | Лицензия | Unity | Что делает | Зрелость |
|---|---|---|---|---|
| `Sopiro/Unity-Procedural-Animation` — https://github.com/Sopiro/Unity-Procedural-Animation | MIT | не указано | Процедурная ходьба четвероногих и шестиногих, демо лазания; IK-метод не назван | ~410 звёзд; дата обновления не видна (25 коммитов). Хорош как чертёж, не как библиотека |
| `hairibar/Hairibar.Ragdoll` — https://github.com/hairibar/Hairibar.Ragdoll | MIT | не указано | Keyframed active ragdoll: Target + Ragdoll, ConfigurableJoint на кости, силы к позе Target | ~266 звёзд |
| `sergioabreu-g/active-ragdolls` — https://github.com/sergioabreu-g/active-ragdolls | Apache-2.0 | 2020.1.9f1 (на другие версии «вероятно работает») | Модульный active ragdoll: базовый класс + независимые модули + поведенческий скрипт на родителе; angular drive на суставах | ~366 звёзд, 36 коммитов; учебный |
| `PhilS94/Unity-Procedural-IK-Wall-Walking-Spider` — https://www.Github.com/PhilS94/Unity-Procedural-IK-Wall-Walking-Spider | **не определена** (в метаданных License: None) | — | Паук, ходит по стенам, ноги через IK по окружению | Не использовать код без проверки LICENSE |
| `MinaPecheux/UnityTutorials-ProceduralAnimations` — https://github.com/MinaPecheux/UnityTutorials-ProceduralAnimations | **не проверено** | — | Туториал: IK-контроллеры через Animation Rigging, простая процедурная анимация | Проверить LICENSE |
| `corle-bell/active-ragdolls` | Apache-2.0 (по README) | — | Форк sergioabreu-g | Не нужен отдельно |
| `brucebarnes/CJ-Test` | не проверено | — | Учебный пример ConfigurableJoint | Учебный |

**Не найдено:** открытого, зрелого, MIT-лицензированного пакета для квадрупедов или многоногих с актуальной Unity 6 поддержкой. Кандидаты выше — учебные.

**Платные (не берём, для справки):**
- Final IK (RootMotion) — $90 за место по листингу Asset Store; поддерживает Humanoid, Generic, Legacy: https://assetstore.unity.com/packages/tools/animation/final-ik-14290/reviews (страница с отзывами, цена с неё)
- PuppetMaster (RootMotion) — цена не найдена; разработчик пишет, что один puppet стоит примерно как два обычных ragdoll по производительности: https://rootmotion.freshdesk.com/support/solutions/articles/77000057786-faq
- Creature IK (Asset Store) — $22 за место, FABRIK-решатель, узкий набор: https://assetstore-fallback.unity.com/packages/tools/animation/creature-ik-final-joints-solver-208500
- Procedural Spider (Asset Store) — $79.99, исходники включены, но закрытая лицензия.

---

## 4. Что из этого подходит проекту (предварительно, для решения геймдизайнера)

Ограничения проекта: скелеты рантайм-сборки из графа, пересборка при графтах, ~45 существ, low-poly, без платного.

1. **Свой лёгкий слой на Transform'ах (рекомендуемый путь, гипотеза).** IK-решатели (two-bone, FABRIK/CCD — FABRIK описан в исходниках Animation Rigging и открыт), шаги по порогу расстояния, фазы походки по данным графа, пружины на вторичное движение. Всё в `LateUpdate`, без Animator и без PlayableGraph на существо. Плюс: работает с любым скелетом, по именам костей. Минус: писать и тестировать самим; стоимость — несколько классов, тесты на чистой математике (EditMode) вполне ложатся на наш пайплайн.
2. **Animation Rigging как опция для отдельных узлов** (например, голова look-at, Multi-Aim), если проверим, что `AddComponent`-построение рабочее и на 45 существах укладывается в бюджет. Требует прототипа и профайла.
3. **Active ragdoll только для смерти/падения** (паттерн Hairibar или sergioabreu-g). Для ходьбы — нет.
4. **Не брать:** Kinematica (preview, статус неясен), платные RootMotion/Creature IK, unlicensed репо как код.

Открытые вопросы для прототипа:
- Строится ли риг Animation Rigging из кода на рантайм-скелете без Humanoid (проверить `AddComponent` + привязку).
- Реальная цена Animation Jobs на 45 существах в Unity 6000.4 (проверить баг main-thread из трекера).
- Лицензия пакета Animation Rigging по LICENSE.md установленного пакета.

## 5. Пробелы (честно)

- Не найдены измеренные цифры производительности для 45 существ с процедурной анимацией (ни Animation Rigging, ни Animation Jobs).
- Не найден текст ни одного GDC-доклада по процедурной анимации в полном виде: только заголовки и аннотации GDC Vault.
- Статус Kinematica не подтверждён.
- Мануал Humanoid (`UsingHumanoidChars`) не прочитан — редирект.
- Лицензия Animation Rigging: только общая Unity Companion License, точный текст не открыт.
- Цена PuppetMaster не найдена.
- Ни одно из перечисленных открытых репо не проверено на Unity 6000.4 (поддержка версий не указана или старая: 2020.1.9f1).
- Общеизвестные описания алгоритмов (gait, пружины, follow-the-leader, Verlet, аддитивные слои) в этом заходе не подтверждены источниками — помечены в тексте.
