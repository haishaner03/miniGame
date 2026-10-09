import requests, json
url='http://127.0.0.1:8080/mcp'
s=requests.Session()
headers={'Accept':'application/json, text/event-stream','Content-Type':'application/json'}
body={'jsonrpc':'2.0','id':1,'method':'initialize','params':{'protocolVersion':'2025-06-18','capabilities':{},'clientInfo':{'name':'codex','version':'1.0'}}}
r=s.post(url,headers=headers,json=body,timeout=10)
print(r.status_code, r.headers.get('mcp-session-id'))
print(r.text[:500])
sid=r.headers.get('mcp-session-id'); headers['mcp-session-id']=sid
for i,method in enumerate(['tools/list','resources/list'],2):
  rr=s.post(url,headers=headers,json={'jsonrpc':'2.0','id':i,'method':method,'params':{}},timeout=10)
  print('\n---',method,rr.status_code,'---')
  print(rr.text[:20000])
