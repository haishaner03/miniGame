if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play mode required.");
var player=UnityEngine.GameObject.Find("SurvivorPlayer");
var movement=player.GetComponent<SurvivorMovement>();
var animator=player.GetComponent<UnityEngine.Animator>();
var renderer=player.GetComponent<UnityEngine.SpriteRenderer>();
var inputField=typeof(SurvivorMovement).GetField("input",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
var updateAnimation=typeof(SurvivorMovement).GetMethod("UpdateAnimation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
bool wasEnabled=movement.enabled;float speed=animator.speed;
string folder="D:/UnityProject/miniGame/.codex_artifacts/survivor_gait";
var camGo=new UnityEngine.GameObject("GaitValidationCamera");camGo.hideFlags=UnityEngine.HideFlags.HideAndDontSave;
var cam=camGo.AddComponent<UnityEngine.Camera>();cam.orthographic=true;cam.orthographicSize=.85f;cam.nearClipPlane=.1f;cam.farClipPlane=100;
cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;cam.backgroundColor=new UnityEngine.Color(.12f,.15f,.12f);
cam.transform.position=player.transform.position+new UnityEngine.Vector3(0,.45f,-10);
var report=new System.Text.StringBuilder();
try {
    movement.enabled=false;animator.speed=0;
    foreach(string state in new[]{"RunRight","RunLeft"}) {
        bool left=state=="RunLeft";
        inputField.SetValue(movement,left?UnityEngine.Vector2.left:UnityEngine.Vector2.right);
        updateAnimation.Invoke(movement,null);
        if(renderer.flipX!=left)throw new System.Exception("Wrong facing for "+state);
        var visited=new System.Collections.Generic.HashSet<string>();
        for(int i=0;i<16;i++) {
            animator.Play(state,0,(i+.25f)/8f);animator.Update(0);
            string expected="Survivor_RunRight_"+(i%8).ToString("00");
            if(renderer.sprite==null||renderer.sprite.name!=expected)throw new System.Exception(state+" frame="+i+" expected="+expected+" actual="+(renderer.sprite==null?"null":renderer.sprite.name));
            visited.Add(renderer.sprite.name);
            if(i<8) {
                var rt=new UnityEngine.RenderTexture(256,256,24);var tex=new UnityEngine.Texture2D(256,256,UnityEngine.TextureFormat.RGB24,false);var previous=UnityEngine.RenderTexture.active;
                try {cam.targetTexture=rt;cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,256,256),0,0);tex.Apply();System.IO.File.WriteAllBytes(folder+"/Unity_"+state+"_"+i.ToString("00")+".png",tex.EncodeToPNG());}
                finally {UnityEngine.RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
        report.AppendLine(state+": 16/16 sampled frame matches across two loops; distinct="+visited.Count+"; flipX="+renderer.flipX);
    }
} finally {
    inputField.SetValue(movement,UnityEngine.Vector2.zero);updateAnimation.Invoke(movement,null);animator.speed=speed;animator.Update(0);movement.enabled=wasEnabled;UnityEngine.Object.DestroyImmediate(camGo);
}
report.AppendLine("Restored idle, movement and animator speed; no scene transform modified.");
System.IO.File.WriteAllText(folder+"/UnityValidation.txt",report.ToString());
return report.ToString();
