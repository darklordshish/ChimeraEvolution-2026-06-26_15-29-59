#!/usr/bin/env bash
# СБОРКА ШАБЛОНА К2 С КЛЮЧАМИ ОДНИМ ЗАХОДОМ (22.10): переподгонка ключей «Волк» (голое тело витрины + черты колонки ст.2),
# «Лось» (колонка ст.2 лося) и ступени 1 волка (колонка ст.1) на сетке шасси, куски-черты из целей, огранка тела, грудь→живот,
# ключи областей, выгрузка FBX. Позы и суставы зависят только от ориентиров — пишутся в поставку как есть.
#   bash sborka_k2.sh <шасси.blend> <рабочая папка> <выход.fbx>
# Цели и ориентиры — в _refs-local/эксперименты (вне git), пути ниже.
set -euo pipefail
K2="$1"; W="$2"; FBX="$3"
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
T=Tools/Blender/proba; SK=Docs/models/rig/chelovek-skelet.json
R="C:/Users/semion/Documents/Chimera_game/_refs-local/эксперименты"
TELO="$R/oboroten-telo-st2-2026-10-21"; KOL="$R/oboroten-st2-cel-2026-10-20"; LOS="$R/los-st2-cel-2026-10-21"; ST1="$R/oboroten-st1-cel-2026-10-22"
mkdir -p "$W"/{volk,los,st1}
bl() { "$B" -b --factory-startup -P "$@" 2>&1 | grep -E "ПОДГОНКА: до|ПОКРЫТИЕ|ЦЕПЬ|КУСОК: «|СЛИТО|ОГРАНКА: (тело|всего)|ФОРМОВКА|СВОДКА|ДОБАВЛЕНО|КЛЮЧИ ОБЛАСТЕЙ|ВЕСА|ЭКСПОРТ|Error|Traceback" || true; }
VSTR="--vstrechnye 1 --vstr_maxd 0.1,предплечье:0.05,предплечье.L:0.05,бедро:0.03,бедро.L:0.03 --zhestkost 30,15,8,4,2"

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

echo "== Ступень 1 волка: подгонка к колонке ст.1, кусок (уши, гребень, хвост)"
bl $T/podgonka_v_poze.py -- "$K2" "$ST1/cel_A_norm.obj" "$ST1/orientiry-celi.json" $SK "$W/st1/k.blend" --imya Волк1 $VSTR \
   --poza "$W/st1/poza.json" --sustavy "$W/st1/sustavy.json" --cel_pokoj "$W/st1/cel_pokoj.obj"
bl $T/kusok_celi.py -- "$W/st1/k.blend" "$W/st1/cel_pokoj.obj" "$W/st1/k.fbx" "$W/st1/k.json" --imya Волк1 --stupen 1 --ves1 1.0 \
   --vesa "$W/st1/cel_pokoj.obj.vesa.json" --st1_ushi 1.66:0.06 --st1_niz 1.20 --st1_verh 1.90 --st1_z 9 --st1_y -9 --st1_hvost 1.0 \
   --griva_x 0.25 --hvost_x 0.45 --hvost_z 0.95 --hvost_y 0.05 --hvost_k_osi 0.15 --hvost_naklon 20 --lapy_z -1 --ostrov 300 \
   --prizhim_d 0.005 --prizhim_k 0.05 --bez_kostej предплечье,голень,стопа --dolya 0.12 --normali 1 --blend "$W/st1/kk.blend"

echo "== Сводка: ключ «Лось» и @Лось, @Волк1, ключи областей, выгрузка"
bl $T/svesti_klyuchi.py -- "$W/volk/f.blend" "$W/los/kk.blend" Лось "$W/svod.fbx" --cherty @Лось --blend "$W/svod.blend"
bl $T/dobavit_kusok.py -- "$W/svod.blend" "$W/st1/kk.blend" @Волк11 @Волк1 "$W/svod1.blend"
bl $T/oblasti_k2.py -- "$W/svod1.blend" "$W/svod2.blend" --klyuchi Волк,Лось
bl $T/eksport_k2.py -- "$W/svod2.blend" "$FBX"
echo "== готово"
