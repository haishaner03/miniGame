import json, urllib.request, sys
from pathlib import Path

URL = 'http://127.0.0.1:8080/mcp'
session = None
counter = 0
def rpc(method, params=None, notification=False):
    global session, counter
    counter += 1
    body = {'jsonrpc':'2.0','method':method,'params':params or {}}
    if not notification: body['id']=counter
    headers={'Content-Type':'application/json','Accept':'application/json, text/event-stream'}
    if session: headers['Mcp-Session-Id']=session
    request=urllib.request.Request(URL,data=json.dumps(body).encode(),headers=headers)
    with urllib.request.urlopen(request,timeout=50) as response:
        session=response.headers.get('Mcp-Session-Id') or session
        data=response.read().decode()
    if not data.strip(): return None
    if data.lstrip().startswith('{'): result=json.loads(data)
    else:
        events=[json.loads(line[5:].strip()) for line in data.splitlines() if line.startswith('data:')]
        result=next(e for e in events if e.get('id')==counter)
    if 'error' in result: raise RuntimeError(json.dumps(result['error']))
    return result.get('result')

def connect():
    info=rpc('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'Codex-local-unity','version':'1.0'}})
    rpc('notifications/initialized',notification=True)
    return info

if __name__ == '__main__':
    connect()
    if len(sys.argv)>1:
        spec=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
        if spec.get('code_file'):
            spec['params']['arguments']['code']=Path(spec['code_file']).read_text(encoding='utf-8-sig')
        result=rpc(spec['method'],spec.get('params'))
        if spec.get('output'):
            Path(spec['output']).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
            print(spec['output'])
        else: print(json.dumps(result,ensure_ascii=False))
    else:
        print(json.dumps(rpc('resources/read',{'uri':'mcpforunity://instances'}),ensure_ascii=False))
        print(json.dumps(rpc('resources/read',{'uri':'mcpforunity://editor/state'}),ensure_ascii=False))
