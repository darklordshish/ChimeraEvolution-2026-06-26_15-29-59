#!/usr/bin/env bash
# СБОРКА ШАБЛОНА К2 С КЛЮЧАМИ ОДНИМ ЗАХОДОМ (22.10): переподгонка ключей «Волк» (голое тело витрины + черты колонки ст.2),
# «Лось» (колонка ст.2 лося) и ступени 1 волка (колонка ст.1) на сетке шасси, куски-черты из целей, огранка тела, грудь→живот,
# органы на шаблоне, складки, ВЕСА ФИНАЛЬНЫЕ С ПЕРЕВЯЗКОЙ, ключи областей, выгрузка FBX. Позы и суставы зависят только от ориентиров — пишутся в поставку как есть.
#   bash sborka_k2.sh <шасси.blend> <рабочая папка> <выход.fbx>
# Цели и ориентиры — в _refs-local/эксперименты (вне git), пути ниже.
set -euo pipefail
K2="$1"; W="$2"; FBX="$3"
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
T=Tools/Blender/proba; SK=Docs/models/rig/chelovek-skelet.json
R="C:/Users/semion/Documents/Chimera_game/_refs-local/эксперименты"
TELO="$R/oboroten-telo-st2-2026-10-21"; KOL="$R/oboroten-st2-cel-2026-10-20"; LOS="$R/los-st2-cel-2026-10-21"; ST1="$R/oboroten-st1-cel-2026-10-22"
mkdir -p "$W"/{volk,los,st1}
# шаг падает — сборка стоит (27.10: раньше grep глотал падение, и выгрузка шла из недоделанного файла); журнал шага — $W/shag.log
bl() { if ! "$B" -b --factory-startup --python-exit-code 1 -P "$@" > "$W/shag.log" 2>&1; then tail -20 "$W/shag.log"; echo "СБОЙ: $1"; exit 1; fi
       grep -E "ПОДГОНКА: до|ПОКРЫТИЕ|ЦЕПЬ|КУСОК: «|СЛИТО|ОГРАНКА: (тело|всего)|ЛИНИИ: {|ПЕРЕНОС: @|ФОРМОВКА|СВОДКА|ДОБАВЛЕНО|ОРГАН|КЛЮЧИ ОБЛАСТЕЙ|ВЕСА|ПЕРЕВЯЗКА|вывернутые грани по|ЭКСПОРТ" "$W/shag.log" || true; }
VSTR="--vstrechnye 1 --vstr_maxd 0.1,предплечье:0.05,предплечье.L:0.05,бедро:0.03,бедро.L:0.03 --zhestkost 30,15,8,4,2"

# ВЕСА ФИНАЛЬНЫЕ ДО СНЯТИЯ КЛЮЧЕЙ (27.10, пункт А): ключ снимается обратным скиннингом — при тех весах, что уйдут в игру
# (не больше 4 костей, швы с общими весами). Иначе любая правка весов потом уводит ключ в позе или требует перевязки,
# а перевязка сохраняет и складку скиннинга, которую правка весов должна была снять (проба 27.10: развороты ключа 132 → 329)
# ЛИНИИ ПЕРЕЛОМА (27.10, пункт Г): рёбра шаблона вдоль линий листа дизайн-линии — огранка ломает грани по ним (Г), а не где
# легли k-средние; вершины едут вдоль поверхности, топология и кольца швов те же
echo "== Шаблон: линии перелома"
bl $T/linii_k2.py -- "$K2" "$W/k2_linii.blend" --linii "Референсы/работа/модельная/2026-10-27-linii-k2/linii-lista.json" --otchet "$W/linii.json"
K2="$W/k2_linii.blend"
echo "== Шаблон: веса финальные"
bl $T/vesa_final.py -- "$K2" "$W/k2_vesa.blend"
K2="$W/k2_vesa.blend"
echo "== Волк: подгонка к голому телу, колонка ст.2 — вторая цель"
bl $T/podgonka_v_poze.py -- "$K2" "$TELO/cel_A_norm.obj" "$TELO/orientiry-celi.json" $SK "$W/volk/k.blend" $VSTR \
   --poza "$W/volk/poza.json" --sustavy "$W/volk/sustavy.json" --cel_pokoj "$W/volk/cel_pokoj.obj" \
   --dop_cel "$KOL/cel_A_norm.obj" --dop_lm "$KOL/orientiry-celi-v-poze-lista.json" --dop_pokoj "$W/volk/kol_pokoj.obj"
echo "== Волк: куски (грива и хвост — колонка; голова, кисти, лапы — голое тело)"
bl $T/kusok_celi.py -- "$W/volk/k.blend" "$W/volk/kol_pokoj.obj" "$W/volk/m.fbx" "$W/volk/m.json" --vesa "$W/volk/kol_pokoj.obj.vesa.json" \
   --golova_z 9 --griva_x 0.28 --hvost_x 0.15 --hvost_z 0.95 --lapy_z -1 --ostrov 800 --prizhim_d 0.005 --prizhim_k 0.05 --prizhim_verh 9 \
   --bez_kostej предплечье,голень,стопа --dolya 0.1 --hvost_naklon 12 --normali 1 --ushi 1.90:0.06:1.6 --blend "$W/volk/kk_m.blend"
bl $T/kusok_celi.py -- "$W/volk/k.blend" "$W/volk/cel_pokoj.obj" "$W/volk/g.fbx" "$W/volk/g.json" --vesa "$W/volk/cel_pokoj.obj.vesa.json" \
   --griva_z 9 --hvost_z -1 --lapy_z 0.13 --kisti_z 0.9 --golova_po_vesam 1 --golova_sheya_z 1.5 --dolya 0.15 --ushi 1.80:0.06:1.4 --blend "$W/volk/kk_g.blend"
bl $T/slit_kuski.py -- "$W/volk/k.blend" @Волк "$W/volk/kk.blend" "$W/volk/kk_g.blend" "$W/volk/kk_m.blend"
echo "== Волк: огранка тела, грудь → живот"
bl $T/ogranka.py -- "$W/volk/kk.blend" "$W/volk/o.blend" --ploshchad 0.012 --sila 0.9 --iter 10
bl $T/formovka.py -- "$W/volk/o.blend" "$W/volk/f.blend" --sustavy "$W/volk/sustavy.json" --golen 1 --koleno 0 --talia 1 --grud 1.04:1.24:12

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

echo "== Сводка: ключ «Лось» и @Лось, @Волк1, органы на шаблоне, складки"
bl $T/svesti_klyuchi.py -- "$W/volk/f.blend" "$W/los/kk.blend" Лось "$W/svod.fbx" --cherty @Лось --blend "$W/svod.blend"
bl $T/dobavit_kusok.py -- "$W/svod.blend" "$W/st1/hvost.blend" @Волк11 @Волк1 "$W/o1h.blend"
# хвост ст.1 — на то тело, которое игра рисует на ст.1 («Волк»×0.5), а не на тело подгонки «Волк1» (зазор был 58 мм; 27.10)
bl $T/perenesti_kusok.py -- "$W/o1h.blend" "$W/st1/k.blend" @Волк1 Волк1 Волк 0.5 "$W/o1.blend"
bl $T/organ_objekt.py -- "$W/o1.blend" "$W/st1/ushi.blend" @Волк11 Чутьё@Волк "$W/o2.blend"
bl $T/organ_objekt.py -- "$W/o2.blend" "$W/st1/greben.blend" @Волк11 Шкура@Волк "$W/o3.blend"
bl $T/organ_objekt.py -- "$W/o3.blend" "$W/st1/kogti.blend" @Волк11 Руки@Волк "$W/o4.blend"
bl $T/formovka.py -- "$W/o4.blend" "$W/o5.blend" --sustavy "$W/volk/sustavy.json" --golen 1 --koleno 0 --talia 1 --raspravit 60

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
