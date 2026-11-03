cd /c/Users/semion/Documents/Chimera_game/ChimeraEvolution-modeli
SWB=$(cygpath -m /c/Temp/claude/C--Users-semion-Documents-Chimera-game-ChimeraEvolution-modeli/53623ae4-97d2-450f-a7bb-396e65699af4/scratchpad); W=$SWB/ch; T=Tools/Blender/proba; B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"; KL=Docs/models/handoff/klyuchi; D=Референсы/работа/модельная/2026-10-30-chelovek-podzharyj; M=$D/maski/vid-front.png; source $W/st.sh
L(){ grep -E "$1|Error|Traceback|line [0-9]+" | cut -c1-240; }
rm -f $W/r9?.blend $W/kr9.blend $W/pr9*
b(){ "$B" -b "$1" --factory-startup --python-exit-code 1 -P "$T/$2" -- "${@:3}" 2>&1; }
PYTHONIOENCODING=utf-8 /c/ProgramData/anaconda3/python.exe - <<'EOF'
import json
D='Референсы/работа/модельная/2026-10-30-chelovek-podzharyj/'
T=[[0,1.815,-.110],[0,1.79,-.124],[0,1.766,-.134],[0,1.752,-.133],[0,1.735,-.139],[0,1.72,-.144],[0,1.705,-.152],[0,1.697,-.153],[0,1.688,-.141],[0,1.678,-.135],[0,1.672,-.131],[0,1.662,-.133],[0,1.648,-.124],
[.012,1.760,-.131],[.034,1.768,-.124],[.057,1.755,-.095],
[.015,1.741,-.118],[.032,1.746,-.108],[.050,1.742,-.096],[.032,1.733,-.108],[.032,1.740,-.098],
[.034,1.722,-.114],[.010,1.722,-.135],[.018,1.697,-.128],[.008,1.688,-.142],[.060,1.715,-.092],[.040,1.688,-.108],[.026,1.672,-.116],[.012,1.672,-.127],[.022,1.635,-.116],[.046,1.648,-.082],
[.066,1.76,-.066],[.050,1.79,-.098]]
E=[[2,13],[13,14],[14,15],[3,13],[16,17],[17,18],[18,19],[19,16],[3,22],[22,23],[23,24],[24,8],[10,28],[28,27],[25,30],[30,29],[18,25]]
json.dump([{"imya":"лицо","ob":"голова","vid":"front","os":1,"abs":1,"x":[0,0.074],"z":[1.60,1.825],"niz_z":1.66,"y_max":-0.05,"tochki":T,"rebra":E}],open(D+'grani-lico.json','w',encoding='utf-8'),ensure_ascii=False)
# пресс: без центров кубиков (пирамидки давали глиф «П»): ярус — две плоскости, верхняя вперёд-вверх, нижняя вперёд-вниз; кромка вдоль оси и рядов
PR=[]; PE=[]
ZZ=[1.272,1.255,1.235,1.215,1.195,1.1725,1.15]
for j,z in enumerate(ZZ):
    gr=j%2==0
    PR+=[[0,z,-.006 if gr else -.002],[.042,z,-.002 if gr else .003]]
    if j: PE+=[[2*j-2,2*j],[2*j-1,2*j+1]]
    if gr and j<6: PE.append([2*j,2*j+1])
json.dump([{"imya":"пресс","ob":"Сердце","vid":"front","os":1,"x":[0,0.082],"z":[1.13,1.30],"nakl":0.2,"tochki":PR,"rebra":PE}],open(D+'grani-press.json','w',encoding='utf-8'),ensure_ascii=False)
S=[[0.15,1.505,.006],[0.105,1.492,.006],[0.07,1.465,.004],
   [0.14,1.486,-.010],[0.10,1.474,-.012],[0.072,1.452,-.008],
   [0.125,1.53,-.003],[0.07,1.525,-.002],
   [0.122,1.43,.004],[0.168,1.445,.001],[0.08,1.41,-.001],[0.103,1.385,.003],[0.088,1.362,-.008],[0.13,1.36,-.006]]
SE=[[0,1],[1,2],[3,4],[4,5],[9,11],[11,10]]
C=[]
for z in (1.445,1.385,1.325,1.265): C+= [[0,z,-.003],[.012,z,0]]
for z in (1.425,1.325,1.235): C.append([.055,z,.002])
P_=[]
for z in (1.17,1.11,1.065): P_+= [[0,z,-.003],[.012,z,0]]
for z in (1.14,1.08): P_.append([.05,z,.002])
json.dump([{"imya":"лопатка","ob":"Руки","vid":"back","x":[0.05,0.19],"z":[1.33,1.55],"tochki":S,"rebra":SE},
           {"imya":"центр спины","ob":"Сердце","vid":"back","os":1,"x":[0,0.095],"z":[1.20,1.46],"nakl":0.2,"tochki":C,"rebra":[[0,2],[2,4],[4,6]]},
           {"imya":"поясница","ob":"хребет","vid":"back","os":1,"x":[0,0.085],"z":[1.03,1.23],"nakl":0.2,"tochki":P_,"rebra":[[0,2],[2,4]]}],open(D+'grani-spina.json','w',encoding='utf-8'),ensure_ascii=False)
EOF
b $W/kr3a.blend kist_po_listu.py $W/r9a.blend --mash ${MASH:-1.12} --naruzhu ${NAR:-0.006} | L КИСТЬ
b $W/r9a.blend prorezhivanie.py $W/r9b.blend Поджарый Сердце,хребет,Руки,Ноги,голова,Чутьё 1 | L "^XX"
b $W/r9b.blend prorezhivanie.py $W/r9c.blend Поджарый шея 0.4 gruppa | L "^D"
b $W/r9c.blend press_yarusy.py $W/r9d.blend --a 0 --g 0 --glad 10 --z 1.12:1.31 --x0 0.07 --x1 0.11 | L "^XX"
b $W/r9d.blend grani_oblasti.py $W/r9e.blend --oblasti $D/grani-press.json | L ГРАНИ
b $W/r9e.blend chistka_vytyanutyh.py $W/r9f.blend --ob Сердце,хребет | L ВЫТЯН
b $W/r9f.blend plecho_po_listu.py $W/r9g.blend --maska $M --tablica $W/plecho_t.json | L "^XX"
b $W/r9g.blend glad_polosa.py $W/r9h.blend --tochki "0,1,1.33,0.13" --shagi 10 --ob Сердце,Руки,хребет | L "^XX"
b $W/r9h.blend grani_oblasti.py $W/r9i.blend --oblasti $D/grani-spina.json | L ГРАНИ
b $W/r9i.blend grani_oblasti.py $W/r9j.blend --oblasti $D/grani-lico.json | L ГРАНИ
b $W/r9j.blend glad_polosa.py $W/r9k.blend --z 1.50:1.62 --kraj 0.03 --shagi 6 --ob шея | L "^XX"
"$B" -b --factory-startup --python-exit-code 1 -P $T/zakryt_lozhe.py -- $W/r9k.blend $W/r9l.blend 2>&1 | L "^XX"
b $W/r9l.blend chistka_shcherbin.py $W/kr9.blend --ugol 70 --radius 0.05 --shagi 2 --ob Сердце,хребет,Руки --z 1.0:1.58 | L "ЩЕРБИНЫ"
st r9; grep -E "ДЫРА изнанка" $W/pr9.log | head -4 | cut -c1-100
"$B" -b $W/kr9.blend --factory-startup -P $W/plotn.py 2>&1 | grep -E "всего"
"$B" -b $W/kr9.blend --factory-startup -P $W/gol_kadr.py -- $W/go >/dev/null 2>&1
cd $W && /c/ProgramData/anaconda3/python.exe -c "
from PIL import Image
ims=[Image.open(f'pr9_{v}.png').crop((230,120,670,620)).resize((440,500)) for v in ['front','34','34back','back']]+[Image.open(f'go_{v}.png').resize((428,500)) for v in ['front','profile','34']]
c=Image.new('RGB',(1780,1000),'white')
for j,i in enumerate(ims[:4]): c.paste(i,(j*445,0))
for j,i in enumerate(ims[4:]): c.paste(i,(j*445,500))
c.save('r9z.png')"
