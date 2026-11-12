# Ресёрч: сменная шкура (мех, кожа, чешуя, иглы) на модульных low-poly телах

Дата: 2026-11-10. Контекст: Unity 6 URP, плоско-затенённые тела ~2000 тр, куски — skinned-меши из FBX без UV, тинт `_BaseColor` через MaterialPropertyBlock, орган «Шкура» донора прививается на шасси другого вида в рантайме, геометрия шкуры ~10% (~200 тр), остальное текстурой. Только бесплатное/открытое.

Пометки: [ИСТОЧНИК] — найдено в поиске/прочитано; [ВЫВОД] — моё рассуждение, источником не подтверждено; [НЕ ПРОВЕРЕНО] — помню, ссылки не открывал. Страницы Medium (Голус) и Catlike не открылись (403/не читались) — по ним только то, что видно в выдаче поиска.

## 1. Как наложить шкуру донора на ЛЮБОЕ шасси без ручной работы по шасси

### 1.1 Триплпланар (три проекции по осям)
- Нода Shader Graph Triplanar: проецирует текстуру по осям X/Y/Z и смешивает по нормали; параметры Position, Normal, Tile, Blend, Input Space (Object, View, World, Tangent, AbsoluteWorld; по умолчанию AbsoluteWorld), Type = Default/Normal, Normal Output Space. Только во фрагментной стадии. [ИСТОЧНИК] https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Triplanar-Node.html
- Мировое пространство: текстура «плывёт», когда объект движется; объектное по живому skinned — «плывёт» при анимации, т.к. позиции вершин деформируются костями. [ИСТОЧНИК] https://forums.unrealengine.com/t/triplanar-mapping-for-skeletal-meshes-without-uv-maps/234056 ; https://help.maxon.net/r3d/houdini/en-us/Content/html/Tri+Planar.html.md
- Стандартный фикс в офлайн-рендерерах: режим Reference/Pref — проекция по координатам и нормалям ПОЗЫ ПОКОЯ; нужны rest-позиции И rest-нормали. [ИСТОЧНИК] https://help.maxon.net/r3d/houdini/en-us/Content/html/Tri+Planar.html.md ; https://sharktacos.github.io/BrushedShading/docs/triPref.html ; https://docs.otoy.com/houdini/Triplanar.html
- В Unreal предлагают Pre-Skinned Local Position/Normal. В Unity прямого аналога нет — на форумах запекают исходные позиции в доп. UV-канал; оговорка: брать из ИСХОДНОГО меша, а не из результата skinning (нет гарантии, что стартовая вершина лежит в bind-позе). [ИСТОЧНИК] https://discussions.unity.com/t/getting-an-unmodified-vertex-position-in-fragment-shader/838141 ; https://discussions.unity.com/t/uv1-textures-become-triplanar-when-uv0-texture-is-used-as-alpha-on-skinned-mesh/1541162
- [ВЫВОД] Skinning трогает позицию/нормаль/тангенс, но не UV-каналы, поэтому rest-позиция в `uv2` (Vector3 через `Mesh.SetUVs(2, List<Vector3>)`) и rest-нормаль в `uv3` доезжают до фрагмента нетронутыми. Это «triplanar в rest-пространстве»: не плывёт при анимации, не требует UV-развёртки, работает на любом шасси. Растяжение на сгибах (локоть, шея) есть, но на 2000-тр low-poly с крупной текстурой терпимо.
- [ВЫВОД] Пространство запекания — ЕДИНОЕ для всего шасси (корень существа в позе покоя, в метрах), а не локальное каждого куска: иначе узор рвётся на швах между кусками меша.
- [ВЫВОД] Проекция в пространстве кости (bone-local) — у каждого куска свой rest, разрыв на стыках; запекание в общее пространство лучше.

### 1.2 UV-свап текстуры на шасси
- Классика Spore: меши с UV, шкурка — процедурные «кисти» в атлас на лету; ручные слои блендятся с игровой краской по альфа-маске. [ИСТОЧНИК] SIGGRAPH 2007 «Player-Driven Procedural Texturing»: https://history.siggraph.org/learning/player-driven-procedural-texturing-by-debry-goffin-hecker-quigley-shodhan-et-al/ ; https://www.chrishecker.com/My_Liner_Notes_for_Spore ; https://janeng.com/?p=143
- [ВЫВОД] Для нас: нужны UV на 5 шасси (разворот low-poly дёшев, но это авторская работа, плюс стыки между кусками) и перенос узора между разными развёртками. Противоречит принципу «шкура любого донора на любом шасси без авторинга по шасси». Запасной путь для одного-двух шасси.

### 1.3 Декали
- [ВЫВОД] Проекционные декали URP работают по depth-буферу и не следуют за анимацией skinned-тела — «плывут». Для шкуры по всему телу не годятся; годятся для шрамов/пятен.

### 1.4 Детальные карты и маски
- [ВЫВОД] Базовая окраска (тинт) + детальная tileable-карта с маской региона; детальная — та же rest-проекция из 1.1. Это и есть предлагаемая схема.

### 1.5 Vertex colors
- [ВЫВОД] Годятся как МАСКА региона и вес смешивания, не как носитель узора: при 2000 тр разрешение узора = разрешение вершин. На плоско-затенённых гранях цвет вершин даёт ступенчатые границы — совпадает со стилем.

## 2. Normal map на плоско-затенённом low-poly

- [ИСТОЧНИК] Polycount: карта нормалей относительна затенению низкополи; сглаживание на плоских участках даёт треугольные артефакты (каждая грань проецирует свою версию), решение — жёсткие рёбра/разделение. https://polycount.com/discussion/comment/2608292 ; https://polycount.com/discussion/comment/1561596
- Unity: карта нормалей хранит мелкую деталь, низкополи — крупную форму. https://docs.unity.cn/ru/2017.3/Manual/StandardShaderMaterialParameterNormalMap.html
- [ВЫВОД] На плоской грани нормаль постоянна → базис постоянен по грани → рельеф чешуи/меха читается как «блики внутри фасеты» на фоне фасетного освещения. Выходит гранёный силуэт с мелкой фактурой; работает и обычно красиво, если рельеф крупный (не шум в 1 пиксель) и сила нормали умеренная.
- Tangent-space vs object-space:
  - Без UV у меша нет тангент. Триплпланар-нормали считают базис для каждой проекции из нормали (swizzle по оси). Голус: переиспользование тангент меша — лишь приближение; корректно считать свои тангент/битангент для каждой из трёх проекций. [ИСТОЧНИК, по выдаче поиска] https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a . Методы смешивания whiteout / UDN / reoriented — детали не проверялись.
  - Нода Triplanar с Type=Normal сама смешивает нормали; Normal Output Space по умолчанию Tangent. [ИСТОЧНИК] ссылка на Shader Graph выше.
  - Object-space normal map: привязана к конкретному мешу, для переносимой шкуры непригодна. [ВЫВОД]
  - [ВЫВОД] Для rest-пространства: смешивать в rest-пространстве, затем строить базис для возмущения из текущей (skinned) нормали фасеты и произвольной касательной; на фасетах с постоянной нормалью этого достаточно.
- Запекание рельефа:
  - [ВЫВОД] Тайловая карта нормалей из хайполи (напр. рельеф чешуи 8k тр на плоском тайле, ортопроекция вниз) — одна на донора, не зависит от шасси. Бесплатно: Blender bake, xNormal.
  - Запекание на каждый меш — требует UV и переделки при смене шасси; не подходит.

## 3. Ограничение шкуры регионом тела и смешивание с базой

- Маска региона: (a) канал vertex color (R = «спина/плечи», G = «брюхо»…) — однократная покраска 5 шасси в Blender, через skinning проходит бесплатно; (b) процедурная маска в rest-пространстве без авторинга: `mask = smoothstep(a,b, restNormal.y) * smoothstep(yLow,yHigh, restPos.y/boundsHeight)` + ограничение по z (плечи); (c) текстурная маска — нужны UV. [ВЫВОД]
- [ВЫВОД] (b) как дефолт для всех шасси, (a) как переопределение, где автомаска плоха. Требует единой ориентации осей в rest (по контракту моделей так и есть).
- Смешивание: `albedo = lerp(base*tint, skinTex*tint2, mask*skinWeight)`, нормаль — lerp/whiteout возмущений; `skinWeight` — параметр рендерера (0/1 при снятии/надевании органа) → переключение в рантайме без пересборки геометрии. [ВЫВОД]
- `_BaseColor` (состав/эмоция) остаётся множителем поверх. [ВЫВОД]
- Окраска брони в Monster Hunter через маски по каналам текстуры: [НЕ ПРОВЕРЕНО] помню, источников не открывал.

## 4. URP специфика

- Lit URP: одна базовая карта, одна карта нормалей, всё через UV; триплпланара нет. Нужен свой Shader Graph (Lit target) или HLSL. [ВЫВОД] В Shader Graph нода Triplanar принимает Position/Normal входами — можно подать rest-позицию из UV2 и rest-нормаль из UV3. Ограничение ноды: только фрагментная стадия. [ИСТОЧНИК]
- Плоское затенение: нормаль грани из `ddx/ddy` позиции (Custom Function) либо дублирование вершин по граням (у нас так и есть). [ВЫВОД]
- SRP Batcher и MPB: Unity прямо пишет, что MPB убирает совместимость материала с SRP Batcher; советуют разные материалы. https://docs.unity3d.com/Manual/DrawCallBatching-Properties.html ; https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html ; запрос roadmap «MPB + SRP Batcher» — Under Consideration: https://activation.unity3d.com/roadmap/75-material-property-block-srp-batcher-compatibility . Блог bugnet.io утверждает обратное — противоречит документации, не использовать. [ИСТОЧНИК]
- [ВЫВОД] Существ на экране десятки, рендереров на существо — десятки: потеря SRP Batcher на них не критична; MPB оставить, материалы-экземпляры не плодить. MPB (`SetFloat("_SkinWeight")`, `SetTexture`) на кастомном шейдере работает.
- Стоимость [ВЫВОД]: триплпланар = 3 выборки albedo + 3 normal на фрагмент; неощутимо для такого количества существ. Можно оставить 2 доминирующих проекции или пропускать третью при весе < ~0.1. Включить мипы и анизотропию; нормали BC5/DXT5nm.

## 5. Примеры

- Spore (Maxis): UV-атлас, процедурные кисти шкуры + ручные слои, всё на лету при редактировании меша. Источники выше. GDC-доклада с этими авторами в выдаче не нашёл.
- Триплпланар как стандарт там, где UV невозможны (террейн, большие поверхности): Shader Graph; Unity manual https://docs.unity3d.com/Manual/built-in-shader-examples-tri-planar-texturing.html ; Catlike https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/ (страница не читалась; по выдаче — три проекции, нули без UV).
- Нормали триплпланара: Голус (ссылка выше); https://3dreference.notion.site/Example-Tri-Planar-Projection-e0670c270c184e18bfab4c0cffe382ee ; https://medium.com/@jasonbooth_86226/improved-triplanar-projections-b990a49637f9
- Shipped-кастомайзеров с именно rest-space triplanar на skinned не нашёл. [НЕ ПРОВЕРЕНО]

## 6. Рекомендация для CHIMERA

1. Шкура = материал + текстуры донора + кусок геометрии (≤200 тр). Геометрия — только то, что читается СИЛУЭТОМ: иглы ежа, грива, гребни, зубцы вдоль хребта. Чешуя, мех, кожа — текстурой и нормалью. [ВЫВОД, критерий «силуэт читается»]
2. Единый шейдер «ChimeraSkin» (Shader Graph Lit или HLSL). Входы: rest-позиция (uv2.xyz), rest-нормаль (uv3.xyz), vertex color — опциональная маска региона.
3. Запекание: скрипт при импорте/сборке `BodyMesh` пишет в каждый кусок rest-позицию и rest-нормаль в ОБЩЕМ пространстве корня шасси (из исходного меша, не из skinned-результата). Тест-сторож: у каждого куска uv2/uv3 той же длины, что вершины.
4. Триплпланар в rest-пространстве, тайлинг в метрах (`uv = restPos.zy * tile`). Масштаб — развилка для геймдизайнера: абсолютный (мелкая чешуя змеи на лосе остаётся мелкой — читается как «пересаженный орган») или относительный (тайл × размер шасси/эталон — «вписанная» шкура). Число `tilePerMeter` — в записи органа.
5. Смешивание: `skinWeight` (0..1) через MPB; маска региона — процедурная (нормаль вверх + высота + перёд) по умолчанию, vertex color — переопределение. Край маски — широкий smoothstep; на фасетах будет ступенчатым — для стиля ок.
6. Нормаль: триплпланар whiteout, сила параметром органа. Рельеф донора — один тайловый normal map, запечённый в Blender с хайполи (8k тр чешуи на тайл), 256–512 px, BC5, затухание силы по дистанции.
7. Тинт состава/эмоции — MPB `_BaseColor` множителем на итоговый альбедо. Профилировать на ~20 существах.
8. Текстуры донора: albedo (серо-тонированная, чтобы тинт работал) + normal + опц. маска.

### Грабли
- Швы: единое rest-пространство на всё шасси, иначе рвётся на границах кусков.
- Плоские фасеты: блендинг по нормали почти бинарный (одна ось на фасету) — резкие смены проекций на косых гранях; Blend 4–8, при необходимости веса по сглаженной нормали (запечь в uv4).
- Растяжение проекции в сильно согнутых местах; у змеи цепь звеньев-блоков (`SnakeBodyChain`) — rest-координаты запекать на звено, перепроверить.
- Прививка меняет габарит/пропорцию тела → при смене масштаба меша rest-позиции масштабировать вместе (или хранить в единичных координатах и умножать на uniform шасси).
- `BoneMesher`-кэш и рантайм-пересборка не должны терять каналы uv2/uv3.
- Импортёр FBX может выбросить неиспользуемые UV-каналы без ошибок — проверять Import Settings и сторожем.
- Нормаль-карта даёт блики внутри фасеты, но не силуэт: иглы/гриву не «выжимать» текстурой.
- Анизотропная фильтрация (4–8): tileable normal на косых гранях мылится.
- Проверять кадром (`ShotSpecies` + `capture_game_view`) на каждом шасси; численного детектора «шкура читается» нет.

## Источники (сводка)
- Shader Graph Triplanar node: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Triplanar-Node.html
- Ben Golus, Normal Mapping for a Triplanar Shader: https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a
- Catlike Coding, Triplanar Mapping: https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/
- Unity manual, tri-planar texturing: https://docs.unity3d.com/Manual/built-in-shader-examples-tri-planar-texturing.html
- UE forum, triplanar для skeletal без UV: https://forums.unrealengine.com/t/triplanar-mapping-for-skeletal-meshes-without-uv-maps/234056
- Maxon Redshift Tri Planar (Reference): https://help.maxon.net/r3d/houdini/en-us/Content/html/Tri+Planar.html.md
- Octane Triplanar: https://docs.otoy.com/houdini/Triplanar.html
- Brushed Shading Pref: https://sharktacos.github.io/BrushedShading/docs/triPref.html
- Unity forum, нескинненная позиция: https://discussions.unity.com/t/getting-an-unmodified-vertex-position-in-fragment-shader/838141
- Unity forum, UV1 на skinned: https://discussions.unity.com/t/uv1-textures-become-triplanar-when-uv0-texture-is-used-as-alpha-on-skinned-mesh/1541162
- Unity MPB и SRP Batcher: https://docs.unity3d.com/Manual/DrawCallBatching-Properties.html ; https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html ; https://activation.unity3d.com/roadmap/75-material-property-block-srp-batcher-compatibility
- Polycount: https://polycount.com/discussion/comment/2608292 ; https://polycount.com/discussion/comment/1561596
- Spore SkinPaint: https://history.siggraph.org/learning/player-driven-procedural-texturing-by-debry-goffin-hecker-quigley-shodhan-et-al/ ; https://www.chrishecker.com/My_Liner_Notes_for_Spore ; https://janeng.com/?p=143
