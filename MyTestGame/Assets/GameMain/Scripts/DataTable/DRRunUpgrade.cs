using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityGameFramework.Runtime;

namespace Flower
{
    public sealed class DRRunUpgrade : DataRowBase
    {
        private int id;
        public override int Id => id;
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Effect { get; private set; }
        public float Value { get; private set; }
        public int MaxStacks { get; private set; }
        public string Branch { get; private set; } = "Common";
        public string RequiredEffect { get; private set; } = string.Empty;

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            string[] columns = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            if (columns.Length != 8 && columns.Length != 10)
                throw new FormatException("RunUpgrade requires 8 or 10 tab-separated columns.");
            id = int.Parse(columns[1], CultureInfo.InvariantCulture);
            Name = columns[3];
            Description = columns[4];
            Effect = columns[5];
            Value = float.Parse(columns[6], CultureInfo.InvariantCulture);
            MaxStacks = int.Parse(columns[7], CultureInfo.InvariantCulture);
            Branch = columns.Length == 10 ? columns[8] : "Common";
            RequiredEffect = columns.Length == 10 ? columns[9] : string.Empty;
            Validate();
            return true;
        }

        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)
        {
            using (var stream = new MemoryStream(bytes, startIndex, length, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                id = reader.Read7BitEncodedInt32();
                Name = reader.ReadString();
                Description = reader.ReadString();
                Effect = reader.ReadString();
                Value = reader.ReadSingle();
                MaxStacks = reader.Read7BitEncodedInt32();
                Branch = stream.Position < stream.Length ? reader.ReadString() : "Common";
                RequiredEffect = stream.Position < stream.Length ? reader.ReadString() : string.Empty;
            }
            Validate();
            return true;
        }

        private void Validate()
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(Name) || Value <= 0f ||
                float.IsNaN(Value) || float.IsInfinity(Value) || MaxStacks < 1)
                throw new FormatException("Invalid RunUpgrade row: " + id);
            switch (Effect)
            {
                case "Damage": case "Range": case "AttackSpeed": case "MoveSpeed":
                case "MaxHealth": case "DashCooldown": case "DashDistance": case "LifeSteal":
                case "DamagePercent": case "FreezeChance": case "BurnChance": case "Heal": case "Weapon":
                case "Knockback":
                case "ComboDamage": case "FreezeDuration": case "FrozenDamage": case "Shatter":
                case "FrostSpread": case "BurnDamage": case "BurnDuration": case "BurnExplosion": case "BurnSpread":
                case "DashArmor": case "RhythmBoost": case "LowHealthRegen": case "LowHealthDamage":
                case "EliteKillHeal": case "DashInvincible": case "DashEcho": case "SpeedRange":
                case "HyperStrike": case "ComboFrenzy":
                case "FrostAura": case "FrostShield": case "FrostKnockback": case "FrostMomentum": case "FrostRangeBoost":
                case "BurnHaste": case "BurnZone": case "BurnArmor": case "BurnCrit": case "BurnExecute":
                case "FrostfireBurst": case "ThermalShock": case "HasteFreeze": case "HasteBurn": case "DualElement":
                case "BerserkerPact": case "GlassCannon": case "BloodPact":
                    break;
                default: throw new FormatException("Unknown RunUpgrade effect: " + Effect);
            }
            if (Branch != "Common" && Branch != "Quick" && Branch != "Frost" && Branch != "Fire" && Branch != "Gamble")
                throw new FormatException("Unknown upgrade branch: " + Branch);
        }
    }
}
