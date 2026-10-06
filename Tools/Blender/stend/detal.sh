#!/bin/bash
# ДЕТАЛЬ НАСКВОЗЬ: собрать пилот-руку в Blender → положить в проект → пересоздать виды → снять анфас, профиль, отвод 0/10/30.
# Запуск из корня репо:   bash Tools/Blender/stend/detal.sh [OUT]
#   OUT — папка для кадров и промежуточного (по умолчанию ./_stend; в .gitignore её нет — не коммитить)
# Нужно: Unity этой папки поднят (скилл chimera-unity, set_autotick), Blender 5.x по пути BLENDER,
#        образец волка Anatomy/species/wolf/ref/mv/volk-C-raw.obj (локально, вне git — см. письмо передачи).
# NOKEY=1 bash ... — деталь без ключа «двуногий» (диагностика: дефект от ключа или от переноса на кости).
set -e
OUT=${1:-_stend}; mkdir -p "$OUT"
BLENDER=${BLENDER:-"/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"}
PART=volk-ruki-chetveronogij
unity command --timeout 300 chimera-species >/dev/null                      # Data — свежие (граф волка мог меняться)
unity command --timeout 120 run_script --file Tools/Blender/stend/DumpGraph.cs --entry DumpGraph.Run --args "[\"Волк\",\"$OUT/volk-graph-world.json\"]" >/dev/null
"$BLENDER" -b -P Tools/Blender/detali/noga_iz_obrazca.py -- Anatomy/species/wolf/ref/mv/volk-C-raw.obj "$OUT/volk-graph-world.json" \
    "$OUT/$PART.fbx" "$OUT/$PART.json" 380 2>&1 | grep -E "разделено|Traceback|Error" || true
cp "$OUT/$PART.fbx" Assets/_Chimera/Models/Parts/$PART.fbx; cp "$OUT/$PART.json" Docs/models/handoff/parts/$PART.json
unity command --timeout 120 eval --code "UnityEditor.AssetDatabase.Refresh(); return 1;" >/dev/null
unity command --timeout 120 eval --code "UnityEditor.EditorUtility.RequestScriptReload(); return 1;" >/dev/null; sleep 60
unity command --timeout 300 chimera-species >/dev/null; unity command open_scene --path Assets/Scenes/Полигон.unity >/dev/null
C='["Assets/_Chimera/Data/Человек.asset","Assets/_Chimera/Data/Волк.asset","Руки",'
for v in front profile; do
  unity command --timeout 180 run_script --file Tools/Agent/Stand.cs --entry Stand.Compare --args "${C}\"$v\",1.6]" >/dev/null
  unity command --timeout 120 capture_game_view --camera "СтендCam" --width 1600 --height 1000 --save_path "Кадры/pf-$v.png" >/dev/null
done
for d in 0 10 30; do
  unity command --timeout 180 run_script --file Tools/Agent/Stand.cs --entry Stand.Compare --args "${C}\"front\",1.6]" >/dev/null
  unity command --timeout 60 run_script --file Tools/Blender/stend/AbductMine.cs --entry AbductMine.Run --args "[$d]" >/dev/null
  unity command --timeout 120 capture_game_view --camera "СтендCam" --width 1600 --height 1000 --save_path "Кадры/ab-$d.png" >/dev/null
done
unity command run_script --file Tools/Agent/Stand.cs --entry Stand.Wipe >/dev/null
mv Assets/Кадры/*.png "$OUT/" && rm -rf Assets/Кадры Assets/Кадры.meta
git checkout -- Assets/_Chimera/Data "Docs/Диаграммы" 2>/dev/null || true      # производные — коммитит координатор
# ВНИМАНИЕ: после отката Data на сцене старые виды. Ещё кадры — сперва снова chimera-species
echo "кадры: $OUT/pf-front.png pf-profile.png ab-0/10/30.png (химера в середине ряда, кроп ~ x 540–900)"
