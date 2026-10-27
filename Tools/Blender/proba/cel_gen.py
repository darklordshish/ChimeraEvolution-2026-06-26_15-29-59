"""3D-ЦЕЛЬ ПО ВЫРЕЗКАМ ЛИСТА — Hunyuan3D-2mv (Space `tencent/Hunyuan3D-2mv`, gradio; перенесено из черновиков 27.10, так сняты все
цели с 14.10; разрешение геймдизайнера на выгрузки вырезок — память `chimera-vneshnyaya-generaciya`). Сырьё — в
`Chimera_game/_refs-local/эксперименты/<папка>/` (вне git).

  GRADIO_TEMP_DIR=<черновик> python cel_gen.py <папка вырезок> <папка выхода> <метка> [ракурсы front,left,right] [сид 1234]
  → <папка выхода>/cel_mv_<метка>.glb   (без GRADIO_TEMP_DIR gradio пишет в системный temp и падает на правах)
"""
import sys, shutil, os
from gradio_client import Client, handle_file
V, out, tag = sys.argv[1], sys.argv[2], sys.argv[3]
views = (sys.argv[4] if len(sys.argv) > 4 else 'front,left,right').split(','); seed = int(sys.argv[5]) if len(sys.argv) > 5 else 1234
os.makedirs(out, exist_ok=True)
c = Client('tencent/Hunyuan3D-2mv', download_files=os.path.join(os.environ.get('GRADIO_TEMP_DIR', '.'), 'gtmp'), httpx_kwargs={'timeout': 600})
f = lambda n: handle_file(os.path.join(V, n + '.png')) if n in views else None
r = c.predict(caption=None, image=None, mv_image_front=f('front'), mv_image_back=f('back'), mv_image_left=f('left'), mv_image_right=f('right'),
              steps=30, guidance_scale=5.0, seed=seed, octree_resolution=384, check_box_rembg=True, num_chunks=8000, randomize_seed=False,
              api_name='/shape_generation')
p0 = r[0] if isinstance(r[0], str) else (r[0].get('value') or r[0].get('path') or r[0].get('name'))
dst = os.path.join(out, 'cel_mv_%s%s' % (tag, os.path.splitext(p0)[1]))
shutil.copy(p0, dst); print('ЦЕЛЬ: сгенерирована', dst)
