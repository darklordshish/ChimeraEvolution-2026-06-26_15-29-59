"""Read-only extraction of project species for concept-art sourcing."""
from pathlib import Path
import json
import yaml
import sys
sys.stdout.reconfigure(encoding='utf-8')

ROOT = Path(__file__).resolve().parents[4]
result = {}
for name in ['Человек', 'Волк', 'Лось', 'Ёж', 'Змея']:
    path = ROOT / 'Assets/_Chimera/Data' / (name + '.asset')
    content = '\n'.join(path.read_text(encoding='utf-8').splitlines()[3:])
    data = yaml.safe_load(content)['MonoBehaviour']
    refs = {r['rid']: r for r in data.get('references', {}).get('RefIds', [])}
    organs = []
    for organ in data['organs']:
        item = {k: v for k, v in organ.items() if k not in ['visualParts', 'visualScale', 'visualOffset', 'visualEuler', 'visualAlignToBody', 'carries'] and v not in [0, 0.0, '', None, [], {}]}
        item['visible_parts'] = len(organ.get('visualParts', []))
        item['ability_data'] = [refs.get(a.get('rid'), {}) for a in organ.get('abilities', [])]
        organs.append(item)
    result[name] = {'organs': organs}
target = Path(__file__).with_name('species-source.json')
target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
for name, data in result.items():
    print(name)
    for o in data['organs']:
        print(' ', o['organName'], '/', o['slot'], '/ cost', o.get('cost', 0), '/ chassisOnly', o.get('chassisOnly', 0), '/', ','.join(a.get('type', {}).get('class', '') for a in o['ability_data']))
