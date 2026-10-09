from pathlib import Path
import json, shutil, sys
sys.path.insert(0,'D:/UnityProject/miniGame/.codex_artifacts/grass_autotile')
from unity_mcp import connect,rpc
ROOT=Path('D:/UnityProject/miniGame/MyTestGame'); HERE=Path(__file__).parent
DEST=ROOT/'Assets/GameMain/Res/Map/StreetBlock01/Polished'
DEST.mkdir(parents=True,exist_ok=True)
def call(tool,args):
    r=rpc('tools/call',{'name':tool,'arguments':args}); print(json.dumps(r,ensure_ascii=False))
    if r.get('isError') or r.get('structuredContent',{}).get('success') is False: raise RuntimeError(str(r))
    return r
connect()
mode=sys.argv[1]
if mode=='copy':
    for p in HERE.glob('*.png'): shutil.copy2(p,DEST/p.name)
    (HERE/'backup').mkdir(exist_ok=True)
    for path in ['Assets/GameMain/Scenes/Level/ZombieLevel01.unity','Assets/GameMain/Res/Map/StreetBlock01/Pavement.asset','Assets/GameMain/Res/Map/StreetBlock01/Asphalt.asset']:
        dst=HERE/'backup'/Path(path).name
        if not dst.exists(): shutil.copy2(ROOT/path,dst)
    print('copied',len(list(HERE.glob('*.png'))),'png files')
elif mode in ('assets','apply','verify','capture'):
    call('execute_code',{'action':'execute','compiler':'codedom','safety_checks':True,'code':(HERE/(mode+'.cs')).read_text(encoding='utf-8-sig')})
elif mode=='state':
    print(json.dumps(rpc('resources/read',{'uri':'mcpforunity://editor/state'}),ensure_ascii=False))
    call('read_console',{'action':'get','types':['error','warning'],'count':'15','include_stacktrace':False})
else: raise ValueError(mode)
