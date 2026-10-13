using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation of one starting weapon; combat values come from the equipped definition.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class StartingWeaponCardView : MonoBehaviour
{
    public MeleeWeaponDefinition definition;
    public TMP_Text weaponName, damage, cooldown, action, unlockHint;
    public Image artwork, statusLine;
    public Color availableColor, unavailableColor;

    public void RefreshAvailability()
    {
        if (definition == null) return;
        bool unlocked = SurvivorMetaProgress.IsWeaponUnlocked(definition.kind);
        GetComponent<Button>().interactable = unlocked;
        weaponName.text = definition.displayName;
        damage.text = definition.damage.ToString(CultureInfo.InvariantCulture);
        cooldown.text = definition.cooldown.ToString("0.00", CultureInfo.InvariantCulture) + " 秒";
        action.text = unlocked ? "装备出发  →" : "尚未解锁";
        action.color = unlocked ? availableColor : unavailableColor;
        if (statusLine != null) statusLine.color = action.color;
        if (artwork != null) artwork.color = unlocked ? Color.white : new Color(.65f, .65f, .65f, 1f);
        int cost = definition.kind == SurvivorWeaponKind.Bow ? SurvivorMetaProgress.BowCost : SurvivorMetaProgress.HammerCost;
        unlockHint.text = unlocked ? "" : "幸存者成长解锁 · " + cost + " 点";
        unlockHint.gameObject.SetActive(!unlocked);
    }
}
