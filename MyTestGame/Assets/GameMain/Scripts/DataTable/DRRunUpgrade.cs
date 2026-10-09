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

        public override bool ParseDataRow(string dataRowString, object userData)
        {
            string[] columns = dataRowString.Split(DataTableExtension.DataSplitSeparators);
            if (columns.Length != 8)
                throw new FormatException("RunUpgrade requires 8 tab-separated columns.");
            id = int.Parse(columns[1], CultureInfo.InvariantCulture);
            Name = columns[3];
            Description = columns[4];
            Effect = columns[5];
            Value = float.Parse(columns[6], CultureInfo.InvariantCulture);
            MaxStacks = int.Parse(columns[7], CultureInfo.InvariantCulture);
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
                    break;
                default: throw new FormatException("Unknown RunUpgrade effect: " + Effect);
            }
        }
    }
}
