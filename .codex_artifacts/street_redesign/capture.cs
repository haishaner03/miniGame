string folder="D:/UnityProject/miniGame/.codex_artifacts/street_redesign";
var go=new UnityEngine.GameObject("StreetRedesignCapture");go.hideFlags=UnityEngine.HideFlags.HideAndDontSave;
var cam=go.AddComponent<UnityEngine.Camera>();cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.075f,.09f,.07f);cam.nearClipPlane=.1f;cam.farClipPlane=100;
var main=UnityEngine.GameObject.Find("MainCamera").GetComponent<UnityEngine.Camera>();
cam.cullingMask=main.cullingMask;
System.Action<string,float,float,float,int,int> capture=delegate(string name,float x,float y,float size,int width,int height) {
    cam.transform.position=new UnityEngine.Vector3(x,y,-10);cam.transform.rotation=UnityEngine.Quaternion.identity;cam.orthographicSize=size;cam.aspect=(float)width/height;
    var rt=new UnityEngine.RenderTexture(width,height,24);var tex=new UnityEngine.Texture2D(width,height,UnityEngine.TextureFormat.RGB24,false);
    var old=UnityEngine.RenderTexture.active;
    try {cam.targetTexture=rt;cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,width,height),0,0);tex.Apply();System.IO.File.WriteAllBytes(folder+"/"+name+".png",tex.EncodeToPNG());}
    finally {UnityEngine.RenderTexture.active=old;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
};
try {
    capture("Street_Overview",20,24,25,1050,1250);
    capture("Street_Start_GameView",7.5f,5,3,1280,720);
    capture("Street_Market_GameView",20,27.5f,3,1280,720);
    capture("Street_Exit_GameView",30.4f,43.4f,3,1280,720);
} finally {UnityEngine.Object.DestroyImmediate(go);}
return folder+"/Street_Overview.png";
