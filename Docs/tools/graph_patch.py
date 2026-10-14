"""Вносит позу узлов (выход `Tools/Agent/GraphFromSkeleton.cs`) в граф вида `Docs/models/handoff/<вид>-graph.json`.

Меняются только поля позы: у корня — origin, dir, length; у узла от родителя — freeOrigin, origin, dir, length;
у узла в долях — dir и rel.at/offX/offZ/len (rel.r0/r1 и все прочие поля узла остаются). Порядок узлов и форматирование
файла сохраняются (json с отступом 2, ensure_ascii=False, как у поставок).

    python Docs/tools/graph_patch.py Docs/models/handoff/los-graph.json poza.json
"""
import json
import sys


def main(graph_path, poza_path):
    graph = json.load(open(graph_path, encoding='utf-8'))
    poza = {n['name']: n for n in json.load(open(poza_path, encoding='utf-8'))['nodes']}
    changed = 0
    for node in graph['nodes']:
        p = poza.get(node['name'])
        if not p:
            continue
        for key in ('origin', 'dir', 'length', 'freeOrigin'):
            if key in p:
                node[key] = p[key]
        if 'rel' in p and 'rel' in node:
            for key in ('at', 'offX', 'offZ', 'len'):
                node['rel'][key] = p['rel'][key]
        changed += 1
    with open(graph_path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(graph, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(f'{graph_path}: поза внесена в {changed} узлов из {len(graph["nodes"])}')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
