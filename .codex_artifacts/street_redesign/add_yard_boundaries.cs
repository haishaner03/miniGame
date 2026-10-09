var root=UnityEngine.GameObject.Find("ZombieStreetLayout");
var cordons=root.transform.Find("StreetCordons");
string spritePath="Assets/GameMain/Res/Map/StreetBlock01/Polished/ChainFence.png";
var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(spritePath);
System.Action<string,float,float,float,float> fence=delegate(string name,float x0,float y0,float x1,float y1) {
    var old=cordons.Find(name);if(old!=null)UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);
    var go=new UnityEngine.GameObject(name);go.transform.SetParent(cordons);go.transform.position=new UnityEngine.Vector3((x0+x1)/2,(y0+y1)/2,0);
    bool vertical=System.Math.Abs(y1-y0)>System.Math.Abs(x1-x0);
    float length=vertical?System.Math.Abs(y1-y0):System.Math.Abs(x1-x0);
    var col=go.AddComponent<UnityEngine.BoxCollider2D>();col.size=vertical?new UnityEngine.Vector2(.25f,length):new UnityEngine.Vector2(length,.25f);
    for(float d=.43f;d<length;d+=.86f) {
        var panel=new UnityEngine.GameObject("YardFence");panel.transform.SetParent(go.transform);
        panel.transform.position=new UnityEngine.Vector3(vertical?x0:x0+d,vertical?y0+d:y0,0);
        panel.transform.rotation=UnityEngine.Quaternion.Euler(0,0,vertical?90:0);panel.transform.localScale=new UnityEngine.Vector3(1.3f,1,1);
        var sr=panel.AddComponent<UnityEngine.SpriteRenderer>();sr.sprite=sprite;
        var sort=panel.AddComponent<SurvivorDepthSort>();sort.Configure(vertical?0:-.22f);
    }
};
// Buildings complete these boundary runs; their street entrances remain clear.
fence("Safehouse_WestYard",0,8,5.1f,8);
fence("Safehouse_ServiceGap",9.9f,8,11.7f,8);
fence("WestAlley_GardenEdge",10.4f,20.2f,10.4f,22.1f);
fence("Market_ParkingEnclosure",23.15f,25f,23.15f,30.05f);
fence("Market_EastParkingEdge",29.5f,25f,29.5f,30.05f);
UnityEngine.Physics2D.SyncTransforms();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return "Added visible yard boundaries to prevent off-street shortcuts while preserving the western and eastern detours.";
