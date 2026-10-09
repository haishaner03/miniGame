from pathlib import Path
import json, shutil, sys
from unity_mcp import connect, rpc

ROOT=Path('D:/UnityProject/miniGame/MyTestGame')
HERE=Path(__file__).parent
DEST=ROOT/'Assets/GameMain/Res/Map/Tiles4x4/Ground/AutoGrass'

def call(tool,args):
    result=rpc('tools/call',{'name':tool,'arguments':args})
    print(json.dumps(result,ensure_ascii=False))
    if result.get('isError'): raise RuntimeError('MCP tool error')
    structured=result.get('structuredContent')
    if structured and structured.get('success') is False: raise RuntimeError('Unity tool failed')
    return result

connect()
mode=sys.argv[1]
if mode=='prepare':
    backup=HERE/'backup'
    backup.mkdir(exist_ok=True)
    for asset in ('Assets/GameMain/Scenes/Level/ZombieLevel01.unity',
                  'Assets/GameMain/Res/Map/Tiles4x4/Ground/Ground.prefab',
                  'Assets/GameMain/Res/Map/01_Grass_4x4.png',
                  'Assets/GameMain/Res/Map/01_Grass_4x4.png.meta'):
        dst=backup/Path(asset).name
        if not dst.exists(): shutil.copy2(ROOT/asset,dst)
    DEST.mkdir(parents=True,exist_ok=True)
    for name in ('GrassEdges_47.png','GrassCenters_3x1.png','SoilSeamless.png','SoilCrackedSeamless.png'):
        shutil.copy2(HERE/name,DEST/name)
    call('create_script',{'path':'Assets/GameMain/Scripts/Map/SurvivorGrassAutoTile.cs',
                         'contents':(HERE/'SurvivorGrassAutoTile.cs').read_text(encoding='utf-8')})
elif mode=='state':
    print(json.dumps(rpc('resources/read',{'uri':'mcpforunity://editor/state'}),ensure_ascii=False))
    call('read_console',{'action':'get','types':['error','warning'],'count':'12','include_stacktrace':False})
elif mode in ('assets','apply','verify','capture'):
    call('execute_code',{'action':'execute','compiler':'codedom',
                        'code':(HERE/(mode+'.cs')).read_text(encoding='utf-8'),'safety_checks':True})
else: raise ValueError(mode)
