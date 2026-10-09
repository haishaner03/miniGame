var ground = UnityEngine.GameObject.Find("GroundTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
bool applied = false;
foreach (var p in ground.cellBounds.allPositionsWithin) if (ground.GetTile(p) is SurvivorGrassAutoTile) { applied = true; break; }
var cameraObject = new UnityEngine.GameObject("GrassPreviewCamera");
cameraObject.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
var camera = cameraObject.AddComponent<UnityEngine.Camera>();
camera.orthographic = true;
camera.orthographicSize = 13f;
camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
camera.backgroundColor = new UnityEngine.Color(0.09f, 0.10f, 0.09f);
camera.transform.position = ground.transform.TransformPoint(new UnityEngine.Vector3(16f, 27f, -10f));
camera.transform.rotation = UnityEngine.Quaternion.identity;
camera.nearClipPlane = 0.1f;
camera.farClipPlane = 100f;
var target = new UnityEngine.RenderTexture(1024, 1024, 24);
var texture = new UnityEngine.Texture2D(1024, 1024, UnityEngine.TextureFormat.RGB24, false);
var oldTarget = UnityEngine.RenderTexture.active;
string path = "D:/UnityProject/miniGame/.codex_artifacts/grass_autotile/Scene_" + (applied ? "After" : "Before") + ".png";
try
{
    camera.targetTexture = target;
    camera.Render();
    UnityEngine.RenderTexture.active = target;
    texture.ReadPixels(new UnityEngine.Rect(0, 0, 1024, 1024), 0, 0);
    texture.Apply();
    System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
}
finally
{
    UnityEngine.RenderTexture.active = oldTarget;
    camera.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(cameraObject);
    UnityEngine.Object.DestroyImmediate(texture);
    target.Release();
    UnityEngine.Object.DestroyImmediate(target);
}
return path;
