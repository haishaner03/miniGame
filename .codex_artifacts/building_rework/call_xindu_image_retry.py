import os,json,base64,urllib.request,urllib.error
from pathlib import Path
key=os.environ.get('XINDU_KEY')
prompt='''Use case: stylized-concept. Asset type: Unity 2D zombie-survival neighborhood building sprite. Generate one abandoned convenience store for a dark zombie street block. Top-down three-quarter view with a clearly visible tiled roof, front wall, and left side wall, with convincing depth and thickness. High-quality detailed pixel art with crisp pixel edges, cohesive lighting, richer material texture, and refined irregular details; avoid crude blocky shapes or a repeating tile-grid look. Weathered gray-green concrete, rust red roof trim, dark blue broken windows, boarded windows, cracked walls, dirt, rust, small entrance steps and subtle debris details. Centered full building silhouette, grounded directly on the ground, no floating, no large cast shadow beneath the building. Transparent background. No readable text, no characters, no watermark, no grid lines. Designed as a Unity Sprite, roughly 4:3 building footprint.'''
body={"model":"gpt-image-2","prompt":prompt,"size":"1024x1024"}
req=urllib.request.Request('https://xindu.xyz/v1/images/generations',data=json.dumps(body).encode(),headers={'Authorization':'Bearer '+key,'Content-Type':'application/json','Accept':'application/json'},method='POST')
try:
 with urllib.request.urlopen(req,timeout=180) as resp:
  raw=resp.read(); print('status',resp.status,'bytes',len(raw)); data=json.loads(raw)
except urllib.error.HTTPError as e:
 print('HTTP',e.code,e.read().decode(errors='replace')[:2000]); raise
Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework\xindu_response_retry.json').write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
items=data.get('data') or []
if not items: raise SystemExit('no data')
item=items[0]
if item.get('b64_json'):
 img=base64.b64decode(item['b64_json']); Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework\Xindu_Building_gpt-image-2.png').write_bytes(img); print('saved b64',len(img))
elif item.get('url'):
 print('URL',item['url'])
 img=urllib.request.urlopen(item['url'],timeout=180).read(); Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework\Xindu_Building_gpt-image-2.png').write_bytes(img); print('saved url',len(img))
else: print('keys',item.keys())
