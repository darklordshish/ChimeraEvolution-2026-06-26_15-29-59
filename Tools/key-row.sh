#!/usr/bin/env bash
# РЯД КЛЮЧА ВИДА В ИГРЕ — одной командой: тело шасси с формой-ключом при нескольких весах, профиль и анфас,
# плюс нога крупно на последнем весе; лист с подписью «коммит · дата», чтобы по картинке было видно, с какой версии
# он снят (14.10 письма линий дважды разминулись: один смотрел пятый проход, другой снимал шестой).
#
# Запускает та линия, что правит ключ, в своей папке и своём редакторе (headless годится, autotick включён):
#   bash Tools/key-row.sh                       # человек, ключ «Волк», веса 0 0.35 0.7 1
#   bash Tools/key-row.sh Человек Волк "0 0.5 1"
# Пишет Docs/Диаграммы/Кадры/Ключ-<ключ>/ряд-<коммит>.png и нога-<коммит>.png и печатает пути.
set -euo pipefail
species="${1:-Человек}"; key="${2:-Волк}"; weights="${3:-0 0.35 0.7 1}"
root=$(git rev-parse --show-toplevel); cd "$root"
commit=$(git rev-parse --short HEAD); dirty=$(git status --porcelain -- Assets | head -1)
[ -n "$dirty" ] && commit="$commit+правки"
asset="Assets/_Chimera/Data/$species.asset"
tmp="Assets/~КлючРяд"; out="Docs/Диаграммы/Кадры/Ключ-$key"; mkdir -p "$out"
u() { unity command "$@" >/dev/null; }

unity command eval --code "UnityEditor.AssetDatabase.Refresh(); return 1;" >/dev/null
for w in $weights; do
  u run_script --file Tools/Agent/KeyShot.cs --entry KeyShot.On --args "[\"$key\",$w]"
  for v in profile front; do
    u run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Build --args "[\"$asset\",\"$v\"]"
    u capture_game_view --camera "ПрофильCam" --width 700 --height 900 --save_path "${tmp#Assets/}/$w-$v.png"
  done
done
last=${weights##* }
u run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Band --args "[\"$asset\",\"profile\",0,0.6]"
u capture_game_view --camera "ПрофильCam" --width 900 --height 700 --save_path "${tmp#Assets/}/noga.png"
u run_script --file Tools/Agent/KeyShot.cs --entry KeyShot.Off
u run_script --file Tools/Agent/ShotSpecies.cs --entry ShotSpecies.Wipe

py=/c/ProgramData/anaconda3/python.exe; [ -x "$py" ] || py=python3
"$py" Docs/tools/key_row_sheet.py "$tmp" "$out" "$species" "$key" "$commit" "$last" $weights
rm -rf "$tmp" "$tmp.meta"
