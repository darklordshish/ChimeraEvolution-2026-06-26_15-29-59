# Трек 7 (ML / генеративное): открытые image/multiview-to-3D, структурное условие, авто-риг, low-poly, серии форм

Дата сверки: 30.10.2026. Бюджет вызовов исчерпан (~15), многое сверено только по страницам репозиториев/карточек; всё, чего не видел, помечено «неизвестно». Я НЕ нашёл подтверждённых открытых релизов 2026 года по этим методам (поиск по Hunyuan 3.x, Omni, BPT/DeepMesh не дал первичных источников 2026) — актуальность «на 2026» не доказана, свежие версии могли выйти незамеченными.

## 1. Image/multiview-to-3D: какие кандидаты, лицензии кода и весов, VRAM, время

### Takeaway
Из проверенного лицензионно чисто для продукта: TRELLIS.2 (MIT), TripoSG (MIT код и веса), PartCrafter (MIT код), Puppeteer (Apache-2.0 код). Семейство Hunyuan3D (в т.ч. текущая 2mv) идёт под Tencent Community License с исключением ЕС/Великобритании/Южной Кореи — юридический риск для проекта, если выпуск там возможен. Stable Fast 3D — условно-бесплатно только до $1M годового дохода. Мультивью-вход подтверждён у Hunyuan3D-2mv; у TRELLIS.2 и TripoSG на страницах только одно изображение.

### Cited Findings
- Hunyuan3D-2.1: лицензия «Tencent Hunyuan 3D 2.1 Community License» от 13.06.2025; не действует в ЕС, Великобритании и Южной Корее («Territory» — мир минус три региона), распространение и ЛЮБОЕ использование (включая выходы) вне Territory не лицензировано; порог 1 млн MAU требует отдельной лицензии Tencent на её усмотрение; выходы нельзя использовать для улучшения других ИИ-моделей, кроме Hunyuan 3D 2.1 и производных; права на выходы Tencent не заявляет — [LICENSE Hunyuan3D-2.1](https://raw.githubusercontent.com/Tencent-Hunyuan/Hunyuan3D-2.1/main/LICENSE)
- Hunyuan3D-2.1 VRAM: форма 10 ГБ, текстура 21 ГБ, обе — 29 ГБ; релиз 13.06.2025, полные веса и код обучения открыты — [Hunyuan3D-2.1 GitHub](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1)
- Hunyuan3D-2mv: тонкая настройка Hunyuan3D-2 под мультивью (пример: front/left/back, 30 шагов, octree 380); лицензия на карточке — «tencent-hunyuan-community» (текст на карточке не приведён; по аналогии с 2.1/Part, вероятно та же территориальная оговорка — не подтверждено именно для 2mv); VRAM на карточке не указан — [HF Hunyuan3D-2mv](https://huggingface.co/tencent/Hunyuan3D-2mv)
- TRELLIS.2 (Microsoft): MIT на модель и код (nvdiffrast/nvdiffrec — свои лицензии); ≥24 ГБ VRAM, проверено на A100/H100, только Linux; на H100 ~3 с (512³), ~17 с (1024³), ~60 с (1536³); вход — одно изображение; выход GLB с PBR — [TRELLIS.2 GitHub](https://github.com/microsoft/TRELLIS.2), [HF TRELLIS.2-4B](https://huggingface.co/microsoft/TRELLIS.2-4B)
- TripoSG (VAST-AI): код MIT, веса на HF — MIT; ≥8 ГБ VRAM; март 2025, 1.5B rectified flow, обучен на 2M пар изображение–SDF; есть опция лимита граней (`--faces`), вариант scribble+prompt (апрель 2025) — [TripoSG GitHub](https://github.com/VAST-AI-Research/TripoSG), [HF TripoSG](https://huggingface.co/VAST-AI/TripoSG)
- Stable Fast 3D: Stability Community License — бесплатно при годовом доходе ≤ $1M, выше нужна Enterprise-лицензия (касается и производных); ~6 ГБ VRAM для одного изображения; август 2024 (по arXiv ID, вывод) — [HF stable-fast-3d](https://huggingface.co/stabilityai/stable-fast-3d), [GitHub](https://github.com/Stability-AI/stable-fast-3d)
- InstantMesh, Wonder3D, SV3D, Zero123, Hunyuan3D 3.x/«Pro 3.0/3.1»: лицензии и VRAM НЕ проверены. Страница Scenario (август 2026) упоминает «Hunyuan 3D Pro 3.1, Polygen 1.5, Part, Pro 3.0, 2.1» и называет Part открытой базовой линией, а не production-геометрией; официальных заметок Tencent о 3.1/Polygen 1.5 не найдено — [Scenario](https://help.scenario.com/articles/5967392966-hunyuan-3d-models-the-essentials) (вторичный источник, помечено как неподтверждённое)

### Inferences
- Для Pro-линейки Hunyuan 3.x, судя по формулировке Scenario, открытыми остаются только 2.x/Part/Omni; новые — вероятно облачные (вывод, не доказано).
- Если проект распространяется в ЕС/Великобритании (Steam), использование выходов 2mv юридически неясно: отзыв лицензии вне Territory касается «use»; стоит отдельно спросить юриста. Замена на MIT-пару (TripoSG/TRELLIS.2) снимает вопрос, но они не имеют подтверждённого мультивью-входа в проверенных источниках.
- 24 ГБ у TRELLIS.2 — потребительский потолок (RTX 3090/4090); 8–10 ГБ у TripoSG/Hunyuan-формы реалистичнее для домашнего GPU.

### Gaps
- Текст лицензии именно Hunyuan3D-2mv/2.0; наличие мультивью у TripoSG/TRELLIS.2 (в README не нашёл); время генерации на потребительской карте; независимые сравнения качества на листе зверя; релизы 2026 года.

## 2. Условие структурой (скелет, части, поза) и part-aware генерация

### Takeaway
Структурное условие существует, но слабое для зверей: Hunyuan3D-Omni принимает скелетную позу, bbox, облако точек, воксели, но скелетное условие заявлено для ЛЮДЕЙ. Part-aware (PartCrafter) разбивает объект на части, но по числу частей, а не по анатомии. Условие «анатомией» (мышцы/массы) в найденных источниках не обнаружено.

### Cited Findings
- Hunyuan3D-Omni (3.3B, релиз 25.09.2025, поверх 2.1): условия — bbox, поза (скелет) для 3D-людей, облако точек, воксели; 10 ГБ VRAM; лицензия на странице не названа (ссылка на License.txt); бенчмарков/примеров качества на странице нет — [Hunyuan3D-Omni GitHub](https://github.com/Tencent-Hunyuan/Hunyuan3D-Omni); статья [arXiv 2509.21245](https://arxiv.org/html/2509.21245v1)
- Hunyuan3D-Part (23.09.2025): лицензия «Tencent Hunyuan 3D-Part Community License» с тем же исключением ЕС/Великобритании/Южной Кореи — [LICENSE](https://raw.githubusercontent.com/Tencent-Hunyuan/Hunyuan3D-Part/main/LICENSE). Состав: P3-SAM (нативная 3D-сегментация: семантические признаки, части, bbox) и X-Part (достраивает целые части; публичная «лёгкая» версия, полная — в Hunyuan3D-Studio; вход — сканы/ИИ-меши) — [Hunyuan3D-Part GitHub](https://github.com/Tencent-Hunyuan/Hunyuan3D-Part)
- PartCrafter (NeurIPS 2025): из одного RGB-изображения сразу несколько частей; число частей задаётся `--num_parts` или подсказывается VLM (нужен ключ Gemini — внешний сервис, не годится); MIT; ≥8 ГБ VRAM; открыт 13.07.2025 — [PartCrafter GitHub](https://github.com/wgsxm/PartCrafter)
- PartPacker, HoloPart: не проверял (неизвестно).
- Условие мышцами/анатомией: ни в одном проверенном репозитории не заявлено.

### Inferences
- Part-методы дают «части» по семантике объекта, которая в датасетах (Objaverse-стиль) шире анатомии; ожидать, что они вернут «плечо/бедро/мышцу волка», нет оснований.
- Скелетное условие Omni, возможно, переносимо как «предложение объёма» по позе человека (ступень 0) — для зверей/гибрида неизвестно.
- Практичнее использовать генерацию как априори объёма, а анатомические ограничения накладывать независимо (в духе §4.6 плана Codex).

### Gaps
- Условие для четвероногих, ControlNet-подобные структурные карты для 3D, anatomy-aware/SMPL-X для не-людей: не найдено. Лицензия весов Omni неизвестна.

## 3. Авто-риг

### Takeaway
UniRig и Puppeteer дают скелет+скин с экспортом в FBX, коды открыты (MIT / Apache-2.0); для зверей они заявлены, но надёжность на химерах и на наших 8-костных именованных скелетах не проверена. Лицензия весов UniRig не подтверждена.

### Cited Findings
- UniRig (SIGGRAPH 2025): код MIT; лицензия весов на странице не указана; ≥8 ГБ для вывода; вход .obj/.fbx/.glb/.vrm, выход FBX скелет и скин; публичные чекпоинты обучены на Articulation-XL2.0, полные Rig-XL/VRoid чекпоинты НЕ выложены; плохой скелет (нет хвоста/крыльев) заметно портит скин; преемник SkinTokens анонсирован — [UniRig GitHub](https://github.com/VAST-AI-Research/UniRig). Бумага: ~5 с на модель, категории включают животных — [arXiv 2504.12451](https://arxiv.org/html/2504.12451)
- Puppeteer: Apache-2.0; вход меш (пример олень), экспорт в FBX через `bpy==4.2.0`; веса и датасет Articulation-XL2.0 выложены 04.09.2025; авторы заявляют меньший Chamfer, чем у Pinocchio, RigNet, MagicArticulate, UniRig — [Puppeteer GitHub](https://github.com/Seed3D/Puppeteer) (метрики — по пересказу в поиске: [arXiv 2508.10898](https://arxiv.org/pdf/2508.10898))
- RigAnything (SIGGRAPH/TOG 2025): шаблон-независимый, авторегрессионный, секунды на форму; лицензия не найдена — [arXiv 2502.09615](https://arxiv.org/abs/2502.09615)
- RigNet: не проверял.

### Inferences
- Для проекта авто-риг не нужен для видов (скелет уже именованный и общий); его ценность — проверка/предложение весов для кусков и химер. Нужна сверка весов с нашим скинингом, а не замена скелета.

### Gaps
- Лицензия весов UniRig/Puppeteer/RigAnything; качество на четвероногих с ≤4 весами; VRAM Puppeteer; независимые сравнения.

## 4. Artist-like low-poly генерация

### Takeaway
MeshAnything/V2 ограничены 1600 граней и требуют «хороших» входных мешей; наш бюджет 2–2.5 тыс. тр ≈ 1–1.25 тыс. граней-четырёхугольников может помещаться, но тело целиком хуже кусков. Лицензии неясны («other»).

### Cited Findings
- MeshAnything V2 (ICCV 2025): не более 1600 граней, ~8 ГБ и ~45 с на A6000; нормализация в единичный куб, ось +Y; требуются острые формы — «feed-forward 3D генераторы часто дают плохие результаты»; лицензия: файл LICENSE.txt, текст не прочитан (агрегатор показывает «other») — [MeshAnythingV2 GitHub](https://github.com/buaacyw/MeshAnythingV2); [листинг](https://awesome.ecosyste.ms/projects/github.com%2Fbuaacyw%2Fmeshanythingv2)
- MeshAnything V1 (ICLR 2025): тот же лимит 1600, лицензия «other» — [MeshAnything GitHub](https://github.com/buaacyw/MeshAnything)
- BPT, DeepMesh, MeshGPT, Polygon Generation (Hunyuan Studio): подтверждённых сведений о лицензии, лимите граней, VRAM не получил (страница форка BPT пуста). Hunyuan3D Studio заявляет модуль Polygon Generation — [aifun.cc](https://www.aifun.cc/en/tencent-hybrid-3d-model-released-and-open-source.html) (вторичный, открытость неясна).

### Inferences
- Авто-ретопо нейросетью даёт «художественную» топологию, но не гарантирует гранёные плоскости нашего стиля; для наших целей надёжнее детерминированные методы (трек 8) — вывод.

### Gaps
- Всё про BPT/DeepMesh/MeshGPT; лицензия MeshAnything по тексту; качество на животных; ориентация на 2026.

## 5. Согласованная серия форм (ступени одного субъекта)

### Takeaway
В проверенных источниках нет метода «генерировать серию ступеней как один объект». Мультивью-согласованность (2mv) даёт один объект из ракурсов, но не серию. Детерминизм и совпадение топологии между сэмплами не гарантированы.

### Cited Findings
- Hunyuan3D-2mv принимает несколько ракурсов одного объекта и даёт одну форму — [HF 2mv](https://huggingface.co/tencent/Hunyuan3D-2mv); метода серии/морфов в карточке нет.
- Part-методы (PartCrafter и др.) генерируют одну сцену/объект из частей, не серию стадий — [PartCrafter](https://github.com/wgsxm/PartCrafter).
- Возможно, помогли бы video-diffusion-подходы (SV3D, Zero123), но их лицензии/свойства не проверены.

### Inferences
- Независимые сэмплы по трём листам ст.0–ст.3 дадут разную топологию и дрейф анатомии; серию придётся сводить подгонкой к общему меху (как делает проект: ключи формы + поза), а генерацию использовать лишь как цель.

### Gaps
- Публикации по «consistent shape series / shape interpolation в latent space» трёхмерных генераторов — не искал и не нашёл; стоит отдельный поиск (латентная интерполяция TRELLIS/Hunyuan VAE — гипотеза, не проверена).

## 6. Резюме по ключевым вопросам (кратко)
- Лицензионно чисто (по проверенному): TRELLIS.2, TripoSG (MIT), PartCrafter (MIT), Puppeteer (Apache), UniRig код (MIT). Условно: Stable Fast 3D ($1M). Рисковые: все Hunyuan (EU/UK/KR исключены), MeshAnything («other», не прочитано).
- Риггуемый, part-segmented, low-poly выход без постобработки: ни один проверенный метод это не гарантирует; части — PartCrafter/P3-SAM; риг — UniRig/Puppeteer; low-poly — MeshAnything с лимитом 1600.
- Ограничение анатомией: только слабые аналоги (скелет-поза у людей в Omni, число частей в PartCrafter). Нет свидетельств доказанного применения к зверям/химерам.
- Не проверено: качество на листах существ, релизы 2026, лицензии весов UniRig/Omni/Puppeteer.
