#!/usr/bin/env bash
# СБОРКА ШАБЛОНА К2 С КЛЮЧАМИ ОДНИМ ЗАХОДОМ (22.10): переподгонка ключей «Волк» (голое тело витрины + черты колонки ст.2),
# «Лось» (колонка ст.2 лося) и ступени 1 волка (колонка ст.1) на сетке шасси, куски-черты из целей, огранка тела, грудь→живот,
# органы на шаблоне, складки, ВЕСА ФИНАЛЬНЫЕ С ПЕРЕВЯЗКОЙ, ключи областей, выгрузка FBX. Позы и суставы зависят только от ориентиров — пишутся в поставку как есть.
#   [CEL_TELO=цель.obj] [OGR="ключи огранки"] [OT_OGRANKI=1] bash sborka_k2.sh <шасси.blend> <рабочая папка> <выход.fbx>
# Цели и ориентиры — в _refs-local/эксперименты (вне git), пути ниже.
set -euo pipefail
K2="$1"; W="$2"; FBX="$3"
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
T=Tools/Blender/proba; SK=Docs/models/rig/chelovek-skelet.json
R="C:/Users/semion/Documents/Chimera_game/_refs-local/эксперименты"
TELO="$R/oboroten-telo-st2-2026-10-21"; KOL="$R/oboroten-st2-cel-2026-10-20"; LOS="$R/los-st2-cel-2026-10-21"; ST1="$R/oboroten-st1-cel-2026-10-22"
# CEL_TELO — цель голого тела ст.2 (по умолчанию $TELO/cel_A_norm.obj); подменяется целью с вклеенным торсом (vkleit_tors.py),
# ориентиры остаются от старой цели — торс их не двигает (27.10)
# с 27.10 — торс из сида 7 Hunyuan по витрине тело.png, вклеен в старую цель (ГеймБосс: S7b по умолчанию; полки нет)
CEL_TELO="${CEL_TELO:-$R/oboroten-telo-st2-sidy-2026-10-27/vkleika_s7b.obj}"
mkdir -p "$W"/{volk,los,st1}
# шаг падает — сборка стоит (27.10: раньше grep глотал падение, и выгрузка шла из недоделанного файла); журнал шага — $W/shag.log
bl() { if ! "$B" -b --factory-startup --python-exit-code 1 -P "$@" > "$W/shag.log" 2>&1; then tail -20 "$W/shag.log"; echo "СБОЙ: $1"; exit 1; fi
       grep -E "ПОДГОНКА: до|ПОКРЫТИЕ|ЦЕПЬ|КУСОК: «|СЛИТО|ОГРАНКА: (тело|всего)|ЛИНИИ: {|КРАСИВЫЕ|ГРИВА-МАССА|ОРГАН-ГЛАДЬ|ШОВ:|ПОЛОСА:|БЛИЖЕ|ГРИВА:|ОГРАНКА: живот|ПЕРЕНОС: @|ФОРМОВКА|СВОДКА|ДОБАВЛЕНО|ОРГАН|КЛЮЧИ ОБЛАСТЕЙ|ВЕСА|ПЕРЕВЯЗКА|вывернутые грани по|ЭКСПОРТ" "$W/shag.log" || true; }
# --lap_glad 10 (27.10): рельеф ключа — от цели, не от шаблона (белая линия и пресс человека К2 давали «щит до паха»)
VSTR="--vstrechnye 1 --vstr_maxd 0.1,предплечье:0.05,предплечье.L:0.05,бедро:0.03,бедро.L:0.03 --zhestkost 30,15,8,4,2 --lap_glad 10"
# OGR — ключи огранки; OT_OGRANKI=1 — начать с огранки в готовой рабочей папке (подгонки и куски — оттуда): проба огранки
# на стенде за ~3 мин вместо 15 (27.10)
# FORM_GRUD, RASPRAVIT — шаги формовки в ПОКОЕ ключа после огранки (грудь → живот, расправление складок); в покое они мнут то,
# что огранка выпрямила в позе (27.10, shum.py) — пустая строка выключает
# GRIVA — ключи griva_massa (шерсть гривы и хвоста); 0.35/6/0.8 срезали объём гривы (IoU ст.2 0.680 → 0.611) — мягче
# органы ст.1: гребень — шерсть массой, когти — палочки длиннее 2.5 см срезаются (27.10)
ORG_GREBEN="${ORG_GREBEN:---sherst 0.5:4 --srez 0.08}"; ORG_KOGTI="${ORG_KOGTI:---srez 0.015 --srez_ot Руки}"
# толщина органов (--tolshchina) изнанку не снимает — оболочки цели лежат по коже и местами смотрят внутрь (детектор 28.10);
# лечится двусторонним материалом органов в игре. ORG_USHI — ключи органа ушей
ORG_USHI="${ORG_USHI:-}"
GRIVA="${GRIVA:---dolya 0.7 --glad 3 --prizhim 1.0 --zad 1.1:0.2:0.16}"   # G2: объём гривы цел (IoU 0.679/0.576), шипов меньше; 0.55/4/0.95 — снова срезала
FORM_GRUD="${FORM_GRUD-}"; RASPRAVIT="${RASPRAVIT-}"   # выкл с 27.10: огранка в позе (shum.py: торс ст.2 5.1°/24 % → 3.4°/14 %, развороты 189 → 171)
OGR="${OGR:---rezhim massy --ploshchad_massy 0.035 --povorot 20 --linii 1 --sila 0.9 --iter 10 --maks_sdvig 0.02 --zhivot 0.075:1.0:1.13:1.27 --v_poze Docs/models/handoff/klyuchi/chelovek-volk-poza.json}"   # E′ — выбор дизайн-линии 27.10; живот — две массы через середину до таза

if [ -z "${OT_OGRANKI:-}" ]; then
# ВЕСА ФИНАЛЬНЫЕ ДО СНЯТИЯ КЛЮЧЕЙ (27.10, пункт А): ключ снимается обратным скиннингом — при тех весах, что уйдут в игру
# (не больше 4 костей, швы с общими весами). Иначе любая правка весов потом уводит ключ в позе или требует перевязки,
# а перевязка сохраняет и складку скиннинга, которую правка весов должна была снять (проба 27.10: развороты ключа 132 → 329)
# ЛИНИИ ПЕРЕЛОМА (27.10, пункт Г): рёбра шаблона вдоль линий листа дизайн-линии — огранка ломает грани по ним (Г), а не где
# легли k-средние; вершины едут вдоль поверхности, топология и кольца швов те же
echo "== Шаблон: линии перелома"
bl $T/linii_k2.py -- "$K2" "$W/k2_linii.blend" --linii "Референсы/работа/модельная/2026-10-27-linii-k2/linii-lista.json" --otchet "$W/linii.json"
K2="$W/k2_linii.blend"
# треугольники шаблона — красивые (27.10: длинные тонкие треугольники поперёк двух плоскостей огранки давали «пилу» на животе);
# рёбра линий, края и швы не поворачиваются, вершин столько же. KRASIVYE=1 — вкл (по умолчанию выкл: меняет грани базы человека на
# ст.0 — изломов 13 → 17 %; развороты ст.2 171 → 129, IoU профиля ст.1 0.572 → 0.534; «пила» оказалась контуром стенда)
if [ "${KRASIVYE:-0}" = 1 ]; then echo "== Шаблон: красивые треугольники"; bl $T/krasivye_treug.py -- "$K2" "$W/k2_kras.blend"; K2="$W/k2_kras.blend"; fi
echo "== Шаблон: веса финальные"
bl $T/vesa_final.py -- "$K2" "$W/k2_vesa.blend"
K2="$W/k2_vesa.blend"
echo "== Волк: подгонка к голому телу, колонка ст.2 — вторая цель"
bl $T/podgonka_v_poze.py -- "$K2" "$CEL_TELO" "$TELO/orientiry-celi.json" $SK "$W/volk/k.blend" $VSTR \
   --poza "$W/volk/poza.json" --sustavy "$W/volk/sustavy.json" --cel_pokoj "$W/volk/cel_pokoj.obj" \
   --dop_cel "$KOL/cel_A_norm.obj" --dop_lm "$KOL/orientiry-celi-v-poze-lista.json" --dop_pokoj "$W/volk/kol_pokoj.obj"
echo "== Волк: куски (грива и хвост — колонка; голова, кисти, лапы — голое тело)"
bl $T/kusok_celi.py -- "$W/volk/k.blend" "$W/volk/kol_pokoj.obj" "$W/volk/m.fbx" "$W/volk/m.json" --vesa "$W/volk/kol_pokoj.obj.vesa.json" \
   --golova_z 9 --griva_x 0.28 --hvost_x 0.15 --hvost_z 0.95 --lapy_z -1 --ostrov 800 --prizhim_d 0.005 --prizhim_k 0.05 --prizhim_verh 9 \
   --bez_kostej предплечье,голень,стопа --dolya 0.1 --hvost_naklon 12 --normali 1 --ushi 1.90:0.06:1.6 --blend "$W/volk/kk_m.blend"
bl $T/kusok_celi.py -- "$W/volk/k.blend" "$W/volk/cel_pokoj.obj" "$W/volk/g.fbx" "$W/volk/g.json" --vesa "$W/volk/cel_pokoj.obj.vesa.json" \
   --griva_z 9 --hvost_z -1 --lapy_z 0.13 --kisti_z 0.9 --golova_po_vesam 1 --golova_sheya_z 1.5 --dolya 0.15 --ushi 1.80:0.06:1.4 --blend "$W/volk/kk_g.blend"
bl $T/slit_kuski.py -- "$W/volk/k.blend" @Волк "$W/volk/kk.blend" "$W/volk/kk_g.blend" "$W/volk/kk_m.blend"
fi
echo "== Волк: огранка тела, грудь → живот"
# огранка k-средних, стены — рёбра линий Г. Режим «массы от линий» (--rezhim massy --ploshchad_massy 0.035 --povorot 20 --linii 1)
# торс спокойнее; без предела двигала вершины до 11 см (развороты 186 → 216), с --sila 0.9 --maks_sdvig 0.02 (E′) — 176, IoU 0.666/0.574;
# выбор D/E′ — у дизайн-линии (ряд Референсы/работа/модельная/2026-10-27-ogranka-BDE/)
# грива и хвост массой прядей, а не стружкой: укрупнение, шипы Таубином, прижим к телу — только шерсть (голова, кисти, лапы не
# трогаются; ГеймБосс 27.10: «осколки-шипы 2–5 см по контуру головы и плеч»)
bl $T/griva_massa.py -- "$W/volk/kk.blend" "$W/volk/kkgm.blend" $GRIVA
# край гривы на груди — двумя наклонными участками, низ прижат к телу (дизайн-линия 27.10, FEEDBACK-2026-10-27-codex-ogranka-BDE §2)
# шов куска лапы ст.2 (критик ряда 27.10, п.3): лапа на голени без «сапога» — край и полоса у него на тело, лапа сужается в ногу
# (дыра хвоста сзади — артефакт стенда: в игре черты двусторонние; шов хвоста тянул края по всей длине хвоста — снят)
bl $T/shov_kuska.py -- "$W/volk/kkgm.blend" "$W/volk/kkgm.blend" --kosti голень,голень.L,стопа,стопа.L --polosa ${SHOV_LAPY:-0.12}
# полосы корней на тело ключа ст.2 (скилл privyazka-poverhnosti, швы — дизайн-линия 28.10b): хвост ≈0.2H, грива ≈0.2H, H — голова
bl $T/privyazat_polosu.py -- "$W/volk/kkgm.blend" "$W/volk/kkgm.blend" --kusok @Волк --imya Волк --oblast hvost --polosa ${POL_HV2:-0.05} --vesa 1
bl $T/privyazat_polosu.py -- "$W/volk/kkgm.blend" "$W/volk/kkgm.blend" --kusok @Волк --imya Волк --oblast griva --polosa ${POL_GR2:-0.05} --vesa 1
bl $T/griva_kraj.py -- "$W/volk/kkgm.blend" "$W/volk/kkg.blend" --z_seredina 1.40 --z_pravyj 1.56 --z_levyj 1.52 --x_konec 0.26 --x_maks 0.30
bl $T/ogranka.py -- "$W/volk/kkg.blend" "$W/volk/o.blend" $OGR
bl $T/formovka.py -- "$W/volk/o.blend" "$W/volk/f.blend" --sustavy "$W/volk/sustavy.json" --golen 1 --koleno 0 --talia 1 $FORM_GRUD

if [ -z "${OT_OGRANKI:-}" ]; then
echo "== Лось: подгонка, кусок головы с рогами и копыт"
bl $T/podgonka_v_poze.py -- "$K2" "$LOS/cel_A_norm.obj" "$LOS/orientiry-celi.json" $SK "$W/los/k.blend" --imya Лось $VSTR \
   --poza "$W/los/poza.json" --sustavy "$W/los/sustavy.json" --cel_pokoj "$W/los/cel_pokoj.obj"
bl $T/kusok_celi.py -- "$W/los/k.blend" "$W/los/cel_pokoj.obj" "$W/los/k.fbx" "$W/los/k.json" --imya Лось --vesa "$W/los/cel_pokoj.obj.vesa.json" \
   --golova_z 1.55 --golova_x 0.7 --griva_z 9 --hvost_z -1 --lapy_z 0.08 --blend "$W/los/kk.blend"

echo "== Ступень 1 волка: подгонка к колонке ст.1; хвост — черта @Волк1, уши, гребень, когти — органы на шаблоне"
bl $T/podgonka_v_poze.py -- "$K2" "$ST1/cel_A_norm.obj" "$ST1/orientiry-celi.json" $SK "$W/st1/k.blend" --imya Волк1 $VSTR    --poza "$W/st1/poza.json" --sustavy "$W/st1/sustavy.json" --cel_pokoj "$W/st1/cel_pokoj.obj"
KC="$T/kusok_celi.py -- $W/st1/k.blend $W/st1/cel_pokoj.obj"
COM="--imya Волк1 --stupen 1 --ves1 0.0 --vesa $W/st1/cel_pokoj.obj.vesa.json --lapy_z -1 --prizhim_d 0.005 --prizhim_k 0.05 --bez_kostej голень,стопа --normali 1"
bl $KC "$W/st1/h.fbx" "$W/st1/h.json" $COM --stupen 1 --ves1 1.0 --griva_z 9 --hvost_x 0.45 --hvost_z 0.95 --hvost_y 0.05 --hvost_k_osi 0.15    --hvost_naklon 20 --ostrov 300 --dolya 0.12 --blend "$W/st1/hvost.blend"
bl $KC "$W/st1/u.fbx" "$W/st1/u.json" $COM --st1_ushi 1.66:0.06:-0.04 --griva_z 9 --hvost_z -1 --ostrov 100 --dolya 0.2 --blend "$W/st1/ushi.blend"
bl $KC "$W/st1/g.fbx" "$W/st1/g.json" $COM --st1_niz 1.20 --st1_verh 1.90 --st1_z 9 --st1_y 0.0 --griva_x 0.25 --hvost_z -1 --ostrov 300    --dolya 0.12 --blend "$W/st1/greben.blend"
bl $KC "$W/st1/c.fbx" "$W/st1/c.json" $COM --golova_z 9 --griva_z 9 --hvost_z -1 --kisti_z 0.9 --kisti_zazor 0.012 --ostrov 20 --dolya 0.3    --blend "$W/st1/kogti.blend"

fi
echo "== Сводка: ключ «Лось» и @Лось, @Волк1, органы на шаблоне, складки"
bl $T/svesti_klyuchi.py -- "$W/volk/f.blend" "$W/los/kk.blend" Лось "$W/svod.fbx" --cherty @Лось --blend "$W/svod0.blend"
# ТЕЛО СТ.1 — СВОЙ КЛЮЧ «Волк1» (геймдизайнер 28.10, FEEDBACK-2026-10-28-codex-telo-st1; код игры — main f18b93e: вес ключа рисуется
# ломаной база → Волк1 → Волк). Ключ подгонки ст.1 — огранка в позе ст.1 (без зоны острого V: он — решение ст.2) и в меш шасси
OGR_ST1="${OGR_ST1:---rezhim massy --ploshchad_massy 0.035 --povorot 20 --linii 1 --sila 0.6 --iter 10 --maks_sdvig 0.015 --zhivot 0 --v_poze Docs/models/handoff/klyuchi/chelovek-volk-poza-st1.json}"
# ноги ст.1 — человеческая опора (дизайн-линия §5 п.4): ключ «Волк1» на ногах ближе к базе
NOGI_ST1="${NOGI_ST1:---kosti голень:0.5,стопа:0.4,бедро:0.9}"
bl $T/kluch_blizhe.py -- "$W/st1/k.blend" "$W/st1/kb.blend" --imya Волк1 $NOGI_ST1
bl $T/ogranka.py -- "$W/st1/kb.blend" "$W/st1/o.blend" --imya Волк1 $OGR_ST1
bl $T/svesti_klyuchi.py -- "$W/svod0.blend" "$W/st1/o.blend" Волк1 "$W/svod.fbx" --blend "$W/svod.blend"
bl $T/griva_massa.py -- "$W/st1/hvost.blend" "$W/st1/hvost_m.blend" --kusok @Волк11 --imya Волк1 $GRIVA --ne_trogat нет --hvost_ne 0
bl $T/dobavit_kusok.py -- "$W/svod.blend" "$W/st1/hvost_m.blend" @Волк11 @Волк1 "$W/o1h.blend"
# хвост ст.1 вырезан с тела «Волк1», и игра с 28.10 рисует ст.1 им же — переносить не нужно. PERENOS=1 — прежний перенос на
# «Волк»×0.5 (27.10, когда своего ключа ст.1 не было)
# с 28.10 ноги ключа ст.1 тянутся к базе (kluch_blizhe) — корень хвоста уходит с тела подгонки: хвост переносится с «Волк1»
# подгонки на итоговый «Волк1» (вес 1); PERENOS=st1-05 — прежний перенос на «Волк»×0.5
if [ "${PERENOS:-volk1}" = st1-05 ]; then bl $T/perenesti_kusok.py -- "$W/o1h.blend" "$W/st1/k.blend" @Волк1 Волк1 Волк 0.5 "$W/o1.blend"
else bl $T/perenesti_kusok.py -- "$W/o1h.blend" "$W/st1/k.blend" @Волк1 Волк1 Волк1 1.0 "$W/o1.blend"; fi
bl $T/organ_objekt.py -- "$W/o1.blend" "$W/st1/ushi.blend" @Волк11 Чутьё@Волк "$W/o2.blend" $ORG_USHI
bl $T/organ_objekt.py -- "$W/o2.blend" "$W/st1/greben.blend" @Волк11 Шкура@Волк "$W/o3.blend" $ORG_GREBEN
bl $T/organ_objekt.py -- "$W/o3.blend" "$W/st1/kogti.blend" @Волк11 Руки@Волк "$W/o4.blend" $ORG_KOGTI
# полосы корней ст.1 на тело «Волк1»: хвост и гребень ≈0.12H (дизайн-линия 28.10b)
bl $T/privyazat_polosu.py -- "$W/o4.blend" "$W/o4.blend" --kusok @Волк1 --imya Волк1 --oblast vse --polosa ${POL_HV1:-0.03} --vesa 1
bl $T/privyazat_polosu.py -- "$W/o4.blend" "$W/o4.blend" --kusok Шкура@Волк --imya Волк1 --kluch рост --oblast vse --polosa ${POL_GR1:-0.03} --vesa 1
bl $T/formovka.py -- "$W/o4.blend" "$W/o5.blend" --sustavy "$W/volk/sustavy.json" --golen 1 --koleno 0 --talia 1 $RASPRAVIT

# ПЕРЕВЯЗКА-СТРАХОВКА: веса тела финальные с начала; куски и органы получают веса цели (`.vesa.json`) и бывают сверх 4 костей —
# здесь им 4 кости, а их база и «рост» перевязываются по позам ступеней ИЗ ПОСТАВКИ (их показывает игра; поправки
# poza_po_listu.py живут там). У тела сдвиг ключа обязан быть ~0. Дальше веса не меняются: ключи областей — после, выгрузка проверяет
echo "== Перевязка-страховка, ключи областей, выгрузка"
KL=Docs/models/handoff/klyuchi
bl $T/vesa_final.py -- "$W/o5.blend" "$W/o6.blend" \
   --klyuchi "Волк:$KL/chelovek-volk-poza.json,Лось:$KL/chelovek-los-poza.json,Волк1:$KL/chelovek-volk-poza-st1.json" --rost "Волк:$KL/chelovek-volk-poza.json"
bl $T/oblasti_k2.py -- "$W/o6.blend" "$W/o7.blend" --klyuchi Волк,Лось
bl $T/eksport_k2.py -- "$W/o7.blend" "$FBX"
echo "== готово"
