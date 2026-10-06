#!/bin/bash
# ЗАМЕР ЛУЧОМ И СРЕЗОМ по граням настоящей сборки (поле + блоки) из файлов поставки — не по вершинам: на клетке 0.042
# окно по вершинам врёт на клетку, а на оси тела — ребро сетки (осевые точки брать со сдвигом 2–3 мм).
#   bash Tools/Blender/stend/ray.sh z <вид> <файл-стем> "x,y;x,y"   — z переда (луч вдоль +Z) в точках (x, y)
#   bash Tools/Blender/stend/ray.sh y <вид> <файл-стем> "x,z;x,z"   — y верха (луч вниз)
#   bash Tools/Blender/stend/ray.sh s <вид> <файл-стем> "h1,h2"     — срезы плоскостью Y=h: куски по X (ширина) и Z (глубина);
#                                                                      печатается ПРАВАЯ половина (x ≥ 0), тело симметрично
# <вид> — имя ассета (Человек, Волк, Лось, Ёж, Змея); <файл-стем> — chelovek, volk, los, ezh, zmeya (handoff/<стем>-*.json)
H=Docs/models/handoff; G="$H/$3-graph.json"; HD="$H/$3-head-layout.json"; O="$H/$3-organs-layout.json"
case "$1" in z) E=Bands.RayZ; A="\"$4\"";; y) E=Bands.RayY; A="\"$4\"";; s) E=Bands.Run; A="\"$4\",0.002";; *) echo "режим z|y|s"; exit 1;; esac
unity command --timeout 180 run_script --file Tools/Blender/stend/Bands.cs --entry $E --args "[\"$2\",\"$G\",\"$HD\",\"$O\",0.042,$A]" 2>&1 \
  | grep -o '"result":"[^"]*"' | sed 's/"result":"//; s/"$//' | sed 's/ | /\n/g'
