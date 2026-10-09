using System;
using UnityEditor;
using UnityEngine;

public static class ZombieEncounterSetup
{
    public const string Folder = "Assets/GameMain/Entity/Encounters/";
    [MenuItem("Tools/Zombie/Build Elite and Boss Encounters")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        if(!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/GameMain/Entity","Encounters");
        var config=AssetDatabase.LoadAssetAtPath<ZombieRunConfig>("Assets/GameMain/Resources/ZombieRunConfig.asset");
        config.eliteEncounterPrefab=BuildEnemy(false);
        config.bossEncounterPrefab=BuildEnemy(true);
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        ZombieHudSetup.Build();
        Debug.Log("Elite encounter (room 3) and final boss prefabs / UGUI ready.");
    }
    private static GameObject BuildEnemy(bool boss)
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/GameMain/Entity/Zombie.prefab");
        try
        {
            root.name=boss?"ZombieBossTyrant":"ZombieEliteHunter";
            root.transform.localScale=Vector3.one*(boss?1.45f:1.05f);
            var zombie=root.GetComponent<ZombieChaser>();
            zombie.archetype=ZombieChaser.ZombieArchetype.Tank;
            zombie.tankHealthMultiplier=1;zombie.tankSpeedMultiplier=1;
            zombie.maxHealth=boss?3600:1000;
            zombie.moveSpeed=boss?1.6f:2f;
            zombie.attackDamage=boss?30:26;zombie.tankAttackDamageBonus=0;
            zombie.attackDistance=.8f;zombie.animationFps=9;
            zombie.freezeDurationMultiplier=boss?.3f:.55f;
            zombie.receivedKnockbackMultiplier=boss?.15f:.4f;
            zombie.idleWander=false;zombie.destroyAfterDeath=true;
            var renderer=root.GetComponent<SpriteRenderer>();renderer.color=boss?new Color(.95f,.53f,.4f):new Color(.94f,.8f,.42f);
            var collider=root.GetComponent<CircleCollider2D>();collider.offset=Vector2.zero;collider.radius=.2f/root.transform.localScale.x;
            var champion=root.AddComponent<ZombieChampion>();champion.isBoss=boss;champion.displayName=boss?"街区暴君":"猎食者";
            champion.slamRadius=boss?1.7f:1.15f;champion.warningDuration=boss?.9f:.8f;
            champion.slamDamage=boss?30:26;champion.chargeDamage=boss?34:28;
            champion.chargeSpeed=boss?6.5f:7f;champion.chargeDistance=boss?4.5f:4f;
            champion.actionCooldown=boss?2.5f:2.7f;
            var bar=new SerializedObject(root.GetComponent<WorldHealthBar>());
            bar.FindProperty("width").floatValue=boss?1.25f:.95f;
            bar.FindProperty("offsetY").floatValue=boss?.92f:.72f;
            bar.FindProperty("zombieHeight").floatValue=.04f;
            bar.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root,Folder+root.name+".prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
