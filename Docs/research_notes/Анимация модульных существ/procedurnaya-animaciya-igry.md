# Процедурная анимация в играх и продакшн-технологиях: кейсы

Заметки для линии механик и модельной линии. Бюджет ресёрча — 15 вызовов инструментов. Часть первоисточников закрыта (403 на gamedeveloper.com и gameanim-статьях о Rain World и Bereznyak, GDC Vault отдаёт только метаданные сессий), поэтому многие конкретные техники ниже подтверждены вторичными описаниями, а не слайдами. Такие места помечены как «не проверено первоисточником».

Обозначения: [подтверждено] — видно в тексте источника, который открыт; [из сниппета] — только в выдаче поиска; [не проверено] — утверждение из вторичного источника или непроверенная атрибуция.

---

## 1. Overgrowth (Wolfire Games, Дэвид Розен, GDC 2014)

### Takeaway
Overgrowth делает анимацию из небольшого набора ключевых поз с процедурной интерполяцией и кривыми между ними, а не из полноценных клипов на каждый сценарий. Конкретные техники для бега, атак и реакций на удар по доступным первоисточникам не восстановлены.

### Cited Findings
- Тема доклада: «простые процедурные техники для интерактивной плавной анимации с очень малым числом ключевых кадров»; примеры из Overgrowth, Receiver и Black Shades — [GDC Vault](https://gdcvault.com/play/1020049/Animation-Bootcamp-An-Indie-Approach) [из сниппета]
- Доклад идёт около 25–30 минут и доступен бесплатно на GDC Vault. Длительность в источниках расходится — [GDC Vault](https://gdcvault.com/play/1020049/Animation-Bootcamp-An-Indie-Approach) [из сниппета]
- Анимация Overgrowth в основном управляется ключевыми позами, процедура накладывается сверху — мнение форумного комментатора, не официальное — [Polycount](https://polycount.com/discussion/comment/2093086) [из сниппета]
- Ролл в Overgrowth сложнее, чем смешивание между пятью ключевыми кадрами; для объяснения его упростили — [Wolfire devlog, ModDB](https://www.moddb.com/news/angular-and-linear-keyframe-blending) [из сниппета]
- Обсуждается реализация процедурной анимации Wolfire в движке Urho, где используется блендинг одиночных ключей с пользовательскими кривыми — [Urho3D forum](https://discourse-urho3d.github.io/t/implementing-wolfires-procedural-animation-in-urho/2077) [из сниппета]

### Inferences
- Подход «ключевые позы + процедурная интерполяция + физика» экономит клипы, но требует ручного задания поз. Для нашего случая (тела из мешей и кусков, без классического риггинга) это ближе к «позы как данные», чем к «клипы на каждое действие».
- Инструкции по бегу, атакам и реакциям, которые ищет линия, в доступном тексте не найдены. Их надо брать из слайдов или видео доклада.

### Gaps
- Не удалось прочитать тело доклада или слайды (GDC Vault — только метаданные). Нет конкретики по циклу бега, по замаху и удару, по реакции на попадание.
- Блог Wolfire (blog.wolfire.com, 2008 и далее) не открыт. Упомянутый пост по процедурной анимации не проверен.
- Сниппет-атрибуция «докладчик — программист Overgrowth» не подтверждена.

---

## 2. Rain World (Videocult)

### Takeaway
По доступным данным, существа Rain World (слизнекот, ящерицы, грифы) строятся процедурно, с физикой точек и IK. Что именно сделано с конечностями ящериц, не подтверждено открытым источником. Автор анимации — Joar Jakobsson (вместе с James Therrien на GDC 2016).

### Cited Findings
- Доклад GDC 2016 «Procedural Animation in Rain World»: Joar Jakobsson и James Therrien — [транскрипт, coconote](https://coconote.app/notes/75365925-df44-49db-b8ac-ca71168efdef/transcript) [из сниппета]. В транскрипте упомянуто, что ящерицы бывают разных цветов; о конечностях в выдержке не сказано.
- Джакобсон о подходе: «Передо мной стояла задача заставить конечности слизнекота двигаться, а поскольку код — это то, как в компьютерной игре что-то двигается, я так и сделал» — цитата из интервью Unity [unity.com blog, Exploring procedural design in Rain World: The Watcher](https://unity.com/blog/exploring-procedural-design-rain-world) [из сниппета; точный URL интервью не сверен]
- Game Developer описывает технику как набор точек в пространстве, что делает существ мягкими и гибкими — [Game Developer, Animating Rain World and its squishy creatures](https://gamedeveloper.com/design/video-animating-i-rain-world-i-and-its-many-squishy-stretchy-creatures) [из сниппета; страница 403 при fetch]
- Общее описание IK как основы процедурной анимации — [Zucconi](https://www.alanzucconi.com/2017/04/17/procedural-animations/) [подтверждено]
- Rain World упоминается как пример: гриф — концы крыльев двигаются программно, остальные кости связаны шарнирами — [Zucconi](https://www.alanzucconi.com/2017/04/17/procedural-animations/) [подтверждено]. Это описание автора статьи, а не первоисточник; подтверждение от Videocult нужно отдельно.

### Inferences
- Главный работающий приём по этим источникам — кинематика, управляемая кодом, плюс шарниры и физика поверх. Это не «полная симуляция».
- Выразительность у Rain World, по всей видимости, держится на том, что процедура задаёт не только позу, но и мягкость тела. Прямых данных о «читаемости атак» и «характере» нет.

### Gaps
- Не найдено описание процедурных ног ящериц (IK, шаги, таргеты) из первоисточника.
- Не найдено описание того, как достигается читаемость и «характер» существ.
- Затраты по CPU и ограничения не описаны.

---

## 3. Grow Home / Grow Up (Ubisoft Reflections)

### Takeaway
BUD движется процедурно: руки и ноги ставятся по физике и ограничениям, а не по клипам. Ценой становится потеря полного контроля, которую команда компенсировала тюнингом. Отдельного GDC-доклада Reflections про это в выдаче не найдено.

### Cited Findings
- Игра выросла из эксперимента с процедурной анимацией: команда не пробовала такой степени свободы движения раньше — [Ubisoft News, 2015](https://news.ubisoft.com/en-us/article/7fDmgKCCwWJQtyoVoOvvGM/get-ready-to-grow-home) [из сниппета]
- Старший программист Andy Buck говорит, что процедурное движение персонажа выглядит органичнее, но отнимает часть контроля — [Game Developer, Surrendering control without letting go](https://gamedeveloper.com/design/surrendering-control-without-letting-go-procedural-character-movement) [из сниппета; 403 при fetch]
- BUD цепляется и лазает почти по любой поверхности, и это движение — центр геймплея — [Game Developer, Surrendering control without letting go](https://gamedeveloper.com/design/surrendering-control-without-letting-go-procedural-character-movement) [из сниппета]
- Grow Up сохранила ощущение процедурного движения, но изменила структуру уровней и добавила горизонтальное перемещение — обзоры PC Gamer [из сниппета]
- По Zucconi, Grow Home задаёт положения ног и стоп в коде, а связи ограничены как у рэгдолла — [Zucconi](https://www.alanzucconi.com/2017/04/17/procedural-animations/) [подтверждено]

### Inferences
- Для Chimera это пример «код задаёт конечности, физика завершает», что близко к тому, что нужно для составных тел без скелетного риггинга. Но Grow Home — один персонаж с фиксированным набором конечностей, а не набор модульных частей.

### Gaps
- Нет первоисточника по механике захвата и лазания (как выбираются точки опоры, как сглаживаются переходы).
- GDC-доклад Reflections не найден.

---

## 4. Ubisoft IK Rig и Motion Matching (For Honor)

### Takeaway
Ubisoft использует две разные технологии. IK Rig (Александр Бережняк, GDC 2016) переносит и переиспользует анимации между персонажами разных размеров и скелетов через покадровые правила и IK. Motion Matching (Simon Clavet, GDC 2016) выбирает кадр из базы захвата движения каждый кадр. Оба подхода дорогие по данным и CPU; подробные цифры в открытых источниках не найдены.

### Cited Findings
- IK Rig: «Procedural Pose Animation» / «IK Rig: Moving Forward», докладчик Alexander Bereznyak, Ubisoft Toronto — [GDC Vault](https://gdcvault.com/play/1022984/IK-Rig-Procedural-Pose) [из сниппета]. Названия доклада в разных источниках разные.
- Технология позволяет делиться анимациями между любыми персонажами и менять базовые анимации, чтобы получать новые действия и типы движения — [GDC Vault](https://gdcvault.com/play/1022984/IK-Rig-Procedural-Pose) [из сниппета]
- Подход «системный»: правила применяются каждый кадр в рантайме, результирующие желаемые позиции подаются в IK, который двигает кости — [GDC Vault / сниппет](https://gdcvault.com/play/1022984/IK-Rig-Procedural-Pose) [из сниппета]
- Интервью: Бережняк перешёл в Ubisoft Toronto из Украины, чтобы развивать новые технологии анимации; IK Rig описан как «технология переноса движения между персонажами разных размеров и скелетов» — [gameanim.com](https://www.gameanim.com/2016/04/12/interview-alexander-bereznyak/) [подтверждено; тело интервью не загрузилось]
- Motion Matching, Simon Clavet (Ubisoft Montreal), GDC 2016 «Motion Matching and The Road to Next-Gen Animation» — [GDC Vault](https://www.gdcvault.com/play/1022985/Motion-Matching-and-The-Road) [из сниппета]
- Каждый кадр проверяются все данные захвата, находится лучший кадр, и переход делается туда. Подход очень требователен к объёму данных и CPU — [Game Watch (японская сводка)](https://game.watch.impress.co.jp/docs/news/749136.html) [из сниппета; не проверено первоисточником]
- Дата первого использования Motion Matching: ResetEra утверждает, что впервые в For Honor (2017). Это форум, не источник; дату стоит перепроверить — [ResetEra](https://www.resetera.com/threads/motion-matching-the-cutting-edge-technique-powering-the-last-of-us-2.49388/) [из сниппета; не авторитетно]

### Inferences
- IK Rig работает через правила и IK, а не через клипы. Это напрямую применимо к существам из собранных частей, если есть общий скелет или граф связей. Но перенос анимаций между скелетами предполагает совпадение топологии.
- Motion Matching для существ без захвата движения ценен меньше: он опирается на базу реальных движений, а её у модульных существ нет.
- Затраты: Motion Matching — каждый кадр перебор базы, что дорого при большом числе агентов.

### Gaps
- Не найдены детали IK-решателя, структура правил IK Rig, цифры CPU и объёма данных из первоисточника.
- Не найдено применение к нечеловекоподобным существам.

---

## 5. Прочие кейсы

### 5a. Shadow of the Colossus
**Takeaway:** Конкретной технической публикации про IK и постановку ног колоссов в выдаче не найдено. Нужно читать доклад Масанобу Танаки (CEDEC 2017).
- Танака описывает колоссов как «жёстких и без эмоций» и называет четвероногих и змееподобных колоссов примерами процедурной анимации; смещение ноги от исходной позиции использовалось для коррекции позы — [genDESIGN interview](https://gendesign.co.jp/qa_01/interview04en.html), [из сниппета]; сниппет CEDEC-слайдов — [из сниппета; слайды о Trico]
- Два сайта в выдаче утверждали, что IK ставит ноги колоссов на ландшафт; источники выглядят ненадёжными, на них не опираться.
- **Gaps:** первоисточник по ногам колоссов не открыт; старый технический постмортем (возможно GDC 2006) в выдаче не найден.

### 5b. Wobbledogs
- Атрибуция «Joar Jakobsson» не подтверждена. По результатам поиска, создатель игры — Tom Astle, бывший gameplay-инженер The Sims 4; издатель — Auroch Digital — [Wikipedia](https://en.wikipedia.org/wiki/Wobbledogs), [Auroch Digital](https://aurochdigital.com/wobbledogs) [из сниппета]
- Механика: физически симулируемые собаки, процедурная генерация тел, мутация через генетику и стадии (кокон) — [Wikipedia](https://en.wikipedia.org/wiki/Wobbledogs) [из сниппета]
- Техническая статья по ИИ и физике — [Game Developer, Behind the AI and physics of Wobbledogs](https://gamedeveloper.com/design/behind-the-ai-and-physics-of-i-wobbledogs-i-procedurally-goofy-wobbledogs) [только URL из выдачи, не открыта]
- **Gaps:** содержание статьи не прочитано; способ сборки тел и анимации не проверен.

### 5c. Totally Accurate Battle Simulator (Landfall) и Gang Beasts
- Активный рэгдолл: физика тела с моторами, управляемыми поверх рэгдолла. TABS, по обзорам, использует такой подход — [Medium, Analysis of Active Ragdolls](https://medium.com/@junk.u.spam/analysis-of-active-ragdolls-in-games-82c95f8ed7a5) [из сниппета]
- Рэгдолл: тело из жёстких тел и шарниров; «непредсказуемо», даёт комичный результат; экономит бюджет на анимацию смерти; Gang Beasts опирается на неконтролируемость — [Zucconi](https://www.alanzucconi.com/2017/04/17/procedural-animations/) [подтверждено]
- Вся сцена TABS заключена между двумя невидимыми безфрикционными плоскостями на расстоянии метра, чтобы решать проблемы коллизий — [Rock Paper Shotgun, зеркало](https://wwwrockpapers.pages.dev/posts/the-unknowable-chaos-of-physics-in-totally-accurate-battle-simulator) [из сниппета; зеркало, не первоисточник]
- Внутренняя реализация TABS (цели шарниров, баланс) не найдена. Нужны интервью разработчиков.

### 5d. Assassin's Creed (животные), Exanima, Peridot, Codeer, «procedural walk»
- Не искались в этом заходе: лимит вызовов исчерпан. Открытый вопрос.

---

## 6. Общие уроки: читаемость, скольжение ног, «плавучесть», характер

### Takeaway
Единственный источник с конкретными уроками — Rune Vision (блог-девлог про процедурных существ, 2021–2024). Остальное по этим вопросам не подтверждено.

### Cited Findings
- Чисто кинематическая система (FK + IK), физика только для постановки стоп: точки опоры по рейкастам — [Rune Vision](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html) [подтверждено]
- Раннее решение: стопы двигались по простым дугам; ноги получили таймирование и вращение стоп, которое решает сам IK, а не задано входом — [Rune Vision](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html) [подтверждено]
- Требование: анимация работает без ручной подстройки под каждое существо, потому что существа генерируются в рантайме — [Rune Vision](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html) [подтверждено]
- Ранние результаты «довольно деревянные», подход работает только на малых шагах; на галопе стопы должны быть в воздухе большую часть обратного хода, иначе туловище волочится по земле — [Rune Vision](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html) [подтверждено]
- Проблема IK: несколько решений удовлетворяют ограничениям, поэтому позы «прыгают» между конфигурациями — [Rune Vision](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html) [подтверждено]. Позвоночник, хвост и шея в этом проекте не обработаны — [Rune Vision](https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html) [подтверждено]
- Рэгдолл-подход: «мёртвые ноги» от чистой физики, связь с целью как «кусок верёвки» — [Zucconi](https://www.alanzucconi.com/2017/04/17/procedural-animations/) [подтверждено]
- Скольжение ног, «плавучесть» и уроки по телеграфу атак — в доступных источниках не найдены. Это основной пробел.

### Inferences
- Проблемы «ног, которые не переключаются, и поз, которые прыгают» повторяются у разных авторов. Это следует закладывать в тест (проверку непрерывности позы), а не полагаться на глаз.
- Характер существа, по этим источникам, держится на параметрах тела (мягкость, пружинность), а не на отдельных клипах.

### Gaps
- Нет первоисточника по телеграфу атак, читаемости боя и уроков по «плавучести».
- Нет первоисточника по тому, как накладывается настроение или личность на процедурное движение.

---

## Применимость к модульным и собранным телам (кратко)

- **Сильнее всего подходит:** кинематика, управляемая кодом, с рейкастами для стоп; IK по правилам каждый кадр; вращение стоп, которое решает сам IK. Это то, что Rune Vision описывает как рабочее для существ без ручной подстройки.
- **Слабее подходит:** Motion Matching (нужна база захвата); полный рэгдолл (непредсказуем, для читаемости боя не годится).
- **Риски, подтверждённые источниками:** прыжки между решениями IK; галоп и волочение туловища; отсутствие позвоночника и хвоста в решении.
- **Открыто:** как IK Rig переносит анимацию между скелетами разной топологии — это ключевой вопрос для составных тел.

---

## Список источников (открытые)

- GDC Vault, Overgrowth/Rosen — https://gdcvault.com/play/1020049/Animation-Bootcamp-An-Indie-Approach
- Polycount (мнение) — https://polycount.com/discussion/comment/2093086
- Wolfire devlog, блендинг ключей — https://www.moddb.com/news/angular-and-linear-keyframe-blending
- Urho3D, реализация Wolfire — https://discourse-urho3d.github.io/t/implementing-wolfires-procedural-animation-in-urho/2077
- Rain World GDC 2016, транскрипт — https://coconote.app/notes/75365925-df44-49db-b8ac-ca71168efdef/transcript
- Unity blog, Rain World — https://unity.com/blog/exploring-procedural-design-rain-world
- Game Developer, Rain World (403) — https://gamedeveloper.com/design/video-animating-i-rain-world-i-and-its-many-squishy-stretchy-creatures
- Rune Vision, процедурные существа 2021–2024 — https://blog.runevision.com/2025/01/procedural-creature-progress-2021-2024.html
- Zucconi, Introduction to Procedural Animations — https://www.alanzucconi.com/2017/04/17/procedural-animations/
- Ubisoft News, Grow Home — https://news.ubisoft.com/en-us/article/7fDmgKCCwWJQtyoVoOvvGM/get-ready-to-grow-home
- Game Developer, Grow Up/BUD (403) — https://gamedeveloper.com/design/surrendering-control-without-letting-go-procedural-character-movement
- GDC Vault, IK Rig (Bereznyak) — https://gdcvault.com/play/1022984/IK-Rig-Procedural-Pose
- gameanim, интервью Bereznyak — https://www.gameanim.com/2016/04/12/interview-alexander-bereznyak/
- GDC Vault, Motion Matching (Clavet) — https://www.gdcvault.com/play/1022985/Motion-Matching-and-The-Road
- Game Watch, сводка по Motion Matching — https://game.watch.impress.co.jp/docs/news/749136.html
- genDESIGN, интервью Танаки (Colossus) — https://gendesign.co.jp/qa_01/interview04en.html
- Wikipedia, Wobbledogs — https://en.wikipedia.org/wiki/Wobbledogs
- Medium, Active ragdolls — https://medium.com/@junk.u.spam/analysis-of-active-ragdolls-in-games-82c95f8ed7a5
- Rock Paper Shotgun (зеркало), TABS — https://wwwrockpapers.pages.dev/posts/the-unknowable-chaos-of-physics-in-totally-accurate-battle-simulator

## Что доделать (по приоритету)

1. Слайды и видео доклада Rosen (Overgrowth) — конкретика по бегу, атаке и реакции. Нужен доступ к GDC Vault или видео на YouTube.
2. Слайды Джакобсона и Therrien (GDC 2016, Rain World) — ноги ящериц и читаемость.
3. CEDEC 2017, Танака — ноги колоссов и Trico.
4. Доклад Bereznyak (IK Rig) целиком — решатель и перенос между скелетами.
5. Не охвачено: Assassin's Creed, Exanima, Peridot, Codeer («procedural walk»).
