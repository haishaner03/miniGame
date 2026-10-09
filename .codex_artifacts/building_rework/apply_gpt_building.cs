string path = "Assets/GameMain/Res/Map/StreetBlock01/Buildings3D/Building_ConvenienceStore_GptImage2.png";
UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
if (importer == null) return "Importer not found: " + path;
importer.textureType = UnityEditor.TextureImporterType.Sprite;
importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
importer.spritePixelsPerUnit = 256f;
importer.filterMode = UnityEngine.FilterMode.Point;
importer.mipmapEnabled = false;
importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
importer.alphaIsTransparency = true;
importer.maxTextureSize = 2048;
importer.SaveAndReimport();
var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
if (sprite == null) return "Sprite not found after import: " + path;
var root = UnityEngine.GameObject.Find("BuildingVisuals");
if (root == null) return "BuildingVisuals not found";
var renderers = root.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true);
int changed = 0;
string[] names = new string[renderers.Length];
float[] scales = new float[] {0.82f, 0.72f, 0.76f, 0.84f, 0.70f, 0.74f, 0.78f, 0.72f, 0.84f};
for (int i = 0; i < renderers.Length; i++)
{
    var r = renderers[i];
    r.sprite = sprite;
    r.drawMode = UnityEngine.SpriteDrawMode.Simple;
    float s = scales[Mathf.Min(i, scales.Length - 1)];
    r.transform.localScale = new UnityEngine.Vector3(s, s, 1f);
    r.flipX = (i == 2 || i == 5 || i == 7);
    r.sortingOrder = 3;
    names[i] = r.gameObject.name;
    changed++;
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
UnityEditor.AssetDatabase.SaveAssets();
return "Applied " + changed + " building SpriteRenderers; sprite=" + sprite.name + "; ppu=" + importer.spritePixelsPerUnit + "; pivot=bottom; names=" + string.Join(",", names);

