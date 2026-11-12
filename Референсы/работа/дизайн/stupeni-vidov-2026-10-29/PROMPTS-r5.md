# r5 · змея · правка только А+С · 29.10.2026

Примечание после сверки: предварительные 75–76 px в запросах не отделяли надёжно наружную ширину плеч от расстояния между центрами суставов. В итоговой сверке README ориентиры суставов указаны явно. Координаты диагонали в корректирующем запросе также не согласованы с заявленными 32°; это ошибка составления запроса, не выполненное измерение. Численные критерии по результату не пройдены.

## Единственная корректировка (дословно)

Use case: precise-object-edit. SINGLE FINAL CORRECTION of image1, the prior edit of r4. Image2 is original r4 for invariants.
You FAILED to narrow shoulders: top A+C still has joint width about 75 px versus pelvis 54, ratio 1.4. Do NOT subtly polish it. REDRAW ONLY third-column bodies, keeping all other columns identical to original.
FRONT A+C third-column top: drastic inward relocation and lowering of BOTH SHOULDER JOINTS. Joint centers target x=934 and x=984 (50 px span), pelvis remains x=932..986 (54px span). Joint centers y=151..154. Top of ears near y=117. From each lower ear, around (946,125) and (971,125), silhouette must travel as an unbroken diagonal 32 degrees outward/down toward upper-arm outer edge around (920,156) and (997,156). NO HORIZONTAL SHELF anywhere, no collarbone corner, no ball deltoid. Neck flares straight into upper arms like long sloping tent sides. Shoulder musculature lies UNDER sloping contour. Broaden pelvis slightly if required but DO NOT thin torso arms legs. FULL healthy musculature as in A. Do not interpret narrow JOINT span as skinny muscles. The center of each upper arm is tucked near torso rather than bodybuilder shoulders. Preserve head height y=88 feet y=393 and all long limb dimensions.
SIDE A+C middle and bottom: maintain visible gentle S. Head/neck forward LEFT, thorax back RIGHT, pelvis forward LEFT. Improve unified upper silhouette by REMOVING high round shoulder knob, shoulder joint lower than upper back and blends underneath it, not rising above. Preserve volume A, not skeletal. Top/middle human heads; bottom snake head + dense clear scales over healthy torso arms legs. Keep shorts no tails.
Everything except third-column bodies locked. Keep white background gray lighting, exact row heights/canvas 1536x1024. This is the only remaining correction; prioritize measurable shoulder geometry over fidelity to wrong original anatomy.

## Сборка итогового файла

Из второго результата встроенного imagegen в исходный r4 перенесён только прямоугольник А+С: x=800..1124, y=81..1009 (System.Drawing, без масштабирования). Всё вне него взято из r4, включая заголовок с исторической надписью r4. Имя итогового файла — `chelovek-zmeya-r5.png`. Это техническое сохранение остальных колонок; дополнительного изменения анатомии после единственной корректировки нет.

Встроенный imagegen; исходник — `chelovek-zmeya-r4.png`. Коммит и изменение витрины не разрешены заданием.

## Первый запрос (дословно)

Use case: precise-object-edit.
Edit target: attached CHIMERA snake sheet r4. Make r5 by editing ONLY column THREE, heading СТ.1 · А+С, all three body cells. All pixels outside column3 must remain unchanged, including title, all text, grid, human column1, A column2, snakes column4. Keep canvas 1536x1024, same figure heights and foot baselines.
Critical measurable redesign of A+C:
FRONT top cell: narrow shoulder JOINT centers to 1.0 times pelvis width, absolutely <=1.10. Pelvis width about 54 pixels at this scale, shoulder joint centers about 54 pixels apart rather than original 76. Ear-to-shoulder contour each side descends outward in ONE straight or slightly concave line 30–35 degrees below horizontal. Lower shoulder joints as needed to attain angle. No horizontal clavicle shoulder shelf, no square shoulder step, NO deltoid cap bulging above or beyond that sloping line. Upper arms grow smoothly downward from its end. Healthy strong full torso, arms, thighs, calves equal to A column2 in muscle volume; do not starve or thin limbs. Smooth muscle masses no bodybuilder grooves.
PROFILE middle cell and SCALED PROFILE bottom cell: clearly visible SOFT S BODY AXIS. Facing LEFT: neck and head lean LEFT forward, thorax bends RIGHT backward, pelvis comes LEFT forward again. About 10–14 pixel fore-aft axis excursion at this image scale, not a straight vertical posture. Keep chest depth and limb thickness as A, healthy not wasted. Blend shoulder into descending back contour, no round ball protruding ABOVE back line. Neck forward, thorax back, pelvis forward must be visible BOTH without and with scales. No extreme hunch or broken spine.
Bottom A+C cell preserve snake head and dense clearly readable scales on entire torso arms legs, shorts, human hands feet, no tail. Preserve current row height: feet y~1000 crown y~715; middle feet y~702 crown y~408; top feet y~393 crown y~88. Same grayscale sculpted medium soft studio lighting white background. This is an anatomical silhouette change, not a texture change. Difference versus A must be unmistakable in sloping narrow front shoulders and S shaped profile. Leave other columns strictly untouched.
