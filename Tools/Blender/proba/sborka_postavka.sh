#!/usr/bin/env bash
# ПОСТАВКА НОВОЙ БАЗЫ ЧЕЛОВЕКА (03.11; геймдизайнер: r8 — рабочая база; список — Docs/models/PERESCHET-POSTAVKI-chelovek.md).
# Прежний FBX поставки — источник ключей, органов и черт; база и мост — файлы вне git.
#   bash sborka_postavka.sh <база.blend> <мост.blend> <прежний.fbx> <рабочая папка> <выход.fbx>
set -euo pipefail
BAZ="$1"; MOST="$2"; SRC="$3"; W="$4"; OUT="$5"
B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"; T=Tools/Blender/proba; KL=Docs/models/handoff/klyuchi; RAS=Docs/models/handoff/raskladki/chelovek-ezh-igly.json
mkdir -p "$W"; rm -f "$W"/p?.blend "$W"/igly.blend
s(){ local pat="$1"; shift; "$B" -b --factory-startup --python-exit-code 1 -P "$@" > "$W/shag.log" 2>&1 || { grep -A8 -E "Traceback|Error" "$W/shag.log" | cut -c1-220; echo "СТОП: $2"; exit 1; }; grep -E "$pat" "$W/shag.log" | cut -c1-180 || true; }
b(){ local pat="$1" f="$2" sc="$3"; shift 3; "$B" -b "$f" --factory-startup --python-exit-code 1 -P "$T/$sc" -- "$@" > "$W/shag.log" 2>&1 || { grep -A8 -E "Traceback|Error" "$W/shag.log" | cut -c1-220; echo "СТОП: $sc"; exit 1; }; grep -E "$pat" "$W/shag.log" | cut -c1-180 || true; }
b БАЗА "$BAZ" baza_iz_klyucha.py "$W/p0.blend"
b "швов|РАСПРАВ" "$W/p0.blend" perenos_klyuchej.py "$W/p1.blend" "$SRC" "$MOST" --klyuchi Волк,Лось,Волк1 --kist_zhestko 0.96 --raspravit ${RASPR:-0} --kontrol 0 --otchet "$W/perenos.json"
[ -z "${LEPKA:-}" ] || b ЛЕПКА "$W/p1.blend" klyuch_lepka.py "$W/p1.blend" $LEPKA
b ОРГАН "$W/p1.blend" perenos_organov.py "$W/p2.blend" "$SRC" "$MOST" --kak_est @Волк1,@Лось
s ИГЛЫ "$T/shipy_ezha.py" -- "$W/p2.blend" "$RAS" "$W/igly.blend"
s ОРГАН "$T/organ_objekt.py" -- "$W/p2.blend" "$W/igly.blend" иглы Шкура@Ёж "$W/p3.blend"
s ОРГАН "$T/organ_objekt.py" -- "$W/p3.blend" "$W/igly.blend" игломёт Игломёт@Ёж "$W/p4.blend"
s "ВЕС|ПЕРЕВЯЗ|ошибк" "$T/vesa_final.py" -- "$W/p4.blend" "$W/p5.blend" --klyuchi "Волк:$KL/chelovek-volk-poza.json,Лось:$KL/chelovek-los-poza.json,Волк1:$KL/chelovek-volk-poza-st1.json" --rost "Волк:$KL/chelovek-volk-poza-st1.json"
s "ОБЛАСТ" "$T/oblasti_k2.py" -- "$W/p5.blend" "$W/p6.blend" --klyuchi Волк,Лось
s "ВЕСА" "$T/eksport_k2.py" -- "$W/p6.blend" "$OUT"
echo "== готово: $OUT"
