cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution-modeli
SWB=$(cygpath -m /c/Temp/claude/C--Users-semion-Documents-Chimera-game-ChimeraEvolution-modeli/53623ae4-97d2-450f-a7bb-396e65699af4/scratchpad); W=$SWB/ch; T=Tools/Blender/proba; B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"; KL=Docs/models/handoff/klyuchi; D=Референсы/работа/модельная/2026-10-30-chelovek-podzharyj; M=$D/maski/vid-front.png; source $W/st.sh
L(){ grep -E "$1|Error|Traceback|line [0-9]+" | cut -c1-240; }
rm -f $W/r9?.blend $W/kr9.blend $W/pr9*
b(){ "$B" -b "$1" --factory-startup --python-exit-code 1 -P "$T/$2" -- "${@:3}" 2>&1; }
PYTHONIOENCODING=utf-8 /c/ProgramData/anaconda3/python.exe $T/podzharyj_tochki.py $D
b $W/kr3a.blend kist_po_listu.py $W/r9a.blend --mash ${MASH:-1.12} --naruzhu ${NAR:-0.006} | L КИСТЬ
b $W/r9a.blend prorezhivanie.py $W/r9b.blend Поджарый Сердце,хребет,Руки,Ноги,голова,Чутьё 1 | L "^XX"
b $W/r9b.blend prorezhivanie.py $W/r9c.blend Поджарый шея 0.4 gruppa | L "^D"
b $W/r9c.blend press_yarusy.py $W/r9d.blend --a 0 --g 0 --glad 10 --z 1.12:1.31 --x0 0.07 --x1 0.11 | L "^XX"
b $W/r9d.blend grani_oblasti.py $W/r9e.blend --oblasti $D/grani-press.json | L ГРАНИ
b $W/r9e.blend chistka_vytyanutyh.py $W/r9f.blend --ob Сердце,хребет | L ВЫТЯН
b $W/r9f.blend plecho_po_listu.py $W/r9g.blend --maska $M --tablica $W/plecho_t.json | L "^XX"
b $W/r9g.blend glad_polosa.py $W/r9h.blend --tochki "0,1,1.33,0.13" --shagi 10 --ob Сердце,Руки,хребет | L "^XX"
b $W/r9h.blend grani_oblasti.py $W/r9m.blend --oblasti $D/grani-spina.json | L ГРАНИ
b $W/r9m.blend grani_oblasti.py $W/r9i.blend --oblasti $D/grani-pered.json | L ГРАНИ
b $W/r9i.blend grani_oblasti.py $W/r9j.blend --oblasti $D/grani-lico.json | L ГРАНИ
b $W/r9j.blend glad_polosa.py $W/r9k.blend --z 1.50:1.62 --kraj 0.03 --shagi 6 --ob шея | L "^XX"
"$B" -b --factory-startup --python-exit-code 1 -P $T/zakryt_lozhe.py -- $W/r9k.blend $W/r9l.blend 2>&1 | L "^XX"
b $W/r9l.blend chistka_shcherbin.py $W/kr9.blend --ugol 70 --radius 0.05 --shagi 2 --ob Сердце,хребет,Руки --z 1.0:1.58 | L "ЩЕРБИНЫ"
st r9; grep -E "ДЫРА изнанка" $W/pr9.log | head -4 | cut -c1-100
"$B" -b $W/kr9.blend --factory-startup -P $W/plotn.py 2>&1 | grep -E "всего"
"$B" -b $W/kr9.blend --factory-startup -P $W/gol_kadr.py -- $W/go >/dev/null 2>&1
cd $W && /c/ProgramData/anaconda3/python.exe -c "
from PIL import Image
ims=[Image.open(f'pr9_{v}.png').crop((230,120,670,820)).resize((440,700)) for v in ['front','34','34back','back']]+[Image.open(f'go_{v}.png').resize((428,500)) for v in ['front','profile','34']]
c=Image.new('RGB',(1780,1200),'white')
for j,i in enumerate(ims[:4]): c.paste(i,(j*445,0))
for j,i in enumerate(ims[4:]): c.paste(i,(j*445,700))
c.save('r9z.png')"
