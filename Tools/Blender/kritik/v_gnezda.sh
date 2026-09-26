#!/bin/bash
# ЧЕРНОВИК ВИДА → ПОСТАВКА В ГНЁЗДАХ (модельная линия, 27.09). Генераторы вида пишут раскладки по-старому (узел донора,
# доли места) — так их удобно считать. Поставка — в гнёздах (спека «место — плейсхолдер»). Этот скрипт переводит:
#   1) генераторы → out/*-draft.json;  2) черновики → handoff/;  3) Руки и Ноги — в гнёзда по графу (nest_convert);
#   4) стенд собирает вид, замер Пасти и признаков Чутья → в гнёзда (nest_convert.past / senses), mawNests.
# Запуск (из корня репо, редактор своей папки поднят):  bash Tools/Blender/kritik/v_gnezda.sh Ёж ezh hedgehog
set -e
SP=$1; F=$2; DIR=$3
PY=/c/ProgramData/anaconda3/python.exe
S=$(cygpath -m "${TMP:-/tmp}")/v_gnezda; mkdir -p "$S"
H=Docs/models/handoff
( cd Anatomy/species/$DIR && for g in graph head_layout organs_layout; do PYTHONIOENCODING=utf-8 $PY $g.py >/dev/null; done )
for k in graph head-layout organs-layout; do cp Anatomy/species/$DIR/out/$F-$k-draft.json $H/$F-$k.json; done
PYTHONIOENCODING=utf-8 $PY Anatomy/tools/nest_convert.py $F Руки Ноги
unity command --timeout 180 run_script --file Tools/Blender/kritik/Kadr.cs --entry Kadr.Shot \
  --args "[\"$SP\",\"$H/$F-graph.json\",\"$H/$F-head-layout.json\",\"$H/$F-organs-layout.json\",0.042,\"profile\",0,0.5,1.0]" >/dev/null
Q='var r=GameObject.Find("~СТЕНД"); var ci=System.Globalization.CultureInfo.InvariantCulture; var p=new System.Text.StringBuilder(); var s=new System.Text.StringBuilder(); var seen=new System.Collections.Generic.HashSet<string>(); foreach (var x in r.GetComponentsInChildren<MeshRenderer>()) { var n=x.name; var t=x.transform; var q=t.rotation; var mf=x.GetComponent<MeshFilter>(); var row=string.Format(ci,"{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}\n", mf.sharedMesh.name, t.position.x,t.position.y,t.position.z, q.x,q.y,q.z,q.w, t.lossyScale.x,t.lossyScale.y,t.lossyScale.z); if (n=="Пасть") p.Append(row); else if ((n=="глаза"||n=="уши"||n=="нос"||n=="ямки") && t.position.x > -0.001f && seen.Add(n)) s.Append(n+";"+row); } System.IO.File.WriteAllText("S/past.txt", p.ToString()); System.IO.File.WriteAllText("S/sens.txt", s.ToString()); return p.Length+s.Length;'
unity command --timeout 60 eval --code "${Q//S\//$S/}" >/dev/null
unity command run_script --file Tools/Blender/kritik/Kadr.cs --entry Kadr.Wipe >/dev/null
PYTHONIOENCODING=utf-8 $PY -c "import sys; sys.path.insert(0,'Anatomy/tools'); import nest_convert as n; n.past('$F', r'$S/past.txt'); n.maw_nests('$F'); n.senses('$F', r'$S/sens.txt')"
unity command --timeout 120 run_script --file Tools/Agent/Graph.cs --entry Graph.Check --args "[\"$H/$F-graph.json\"]" | grep -o '"result":"[^"]*"'
