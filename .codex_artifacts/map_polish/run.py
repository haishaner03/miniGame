from pathlib import Path
import sys,json
sys.path.insert(0,'D:/UnityProject/miniGame/.codex_artifacts/grass_autotile')
from unity_mcp import connect,rpc
ROOT=Path(__file__).parent
connect()
if sys.argv[1]=='resource':
    result=rpc('resources/read',{'uri':sys.argv[2]})
elif sys.argv[1]=='code':
    result=rpc('tools/call',{'name':'execute_code','arguments':{'action':'execute','compiler':'codedom','safety_checks':True,'code':(ROOT/sys.argv[2]).read_text(encoding='utf-8-sig')}})
else:
    result=rpc('tools/call',{'name':sys.argv[1],'arguments':json.loads(sys.argv[2])})
if len(sys.argv)>3 and sys.argv[1]=='code':
    (ROOT/sys.argv[3]).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(str(ROOT/sys.argv[3]))
else: print(json.dumps(result,ensure_ascii=False))
if result.get('isError') or result.get('structuredContent',{}).get('success') is False: sys.exit(1)
