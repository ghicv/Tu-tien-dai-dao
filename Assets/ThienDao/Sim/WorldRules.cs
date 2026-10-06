using UnityEngine;

namespace ThienDao.Sim
{
    public enum Rule : byte { WorldQi, SpiritRoots, Breakthrough, TribulationHarshness, Births, Calamities, DemonicPath, Count }

    // Quy luật (GDD §14): the laws of the world Thiên Đạo may rewrite. Plain numbers read by the systems; they change
    // only through SetRuleCommand, so a run stays replayable from seed + command log.
    public sealed class WorldRules
    {
        public static readonly string[] Names =
        {
            "Linh khí thiên địa", "Tỉ lệ linh căn", "Tỉ lệ đột phá", "Thiên kiếp khắc nghiệt", "Sinh sản", "Thiên tai", "Ma đạo"
        };

        public static readonly string[] Help =
        {
            "Linh khí cả thế giới hồi về mức này so với tự nhiên: thấp là mạt pháp, cao là thời đại hoàng kim",
            "Số trẻ thức tỉnh linh căn mỗi năm (0 = không còn ai bước lên tiên lộ)",
            "Nhân với mọi tỉ lệ đột phá đại cảnh giới",
            "Nhân với khả năng vẫn lạc dưới thiên kiếp",
            "Nhân với tốc độ sinh của phàm nhân và thú hoang",
            "Nhân với tần suất thiên tai tự nhiên và ôn dịch (0 = thiên hạ thái bình)",
            "Cho phép tu sĩ tẩu hỏa sa vào ma đạo và tông môn hóa ma"
        };

        static readonly float[] Min = { 0.2f, 0f, 0.25f, 0f, 0.25f, 0f, 0f };
        static readonly float[] Max = { 2f, 5f, 4f, 3f, 3f, 5f, 1f };
        public static readonly float[] Step = { 0.1f, 0.25f, 0.25f, 0.25f, 0.25f, 0.5f, 1f };
        static readonly float[] Default = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };

        readonly float[] _v = (float[])Default.Clone();

        public float this[Rule r] => _v[(int)r];

        public bool DemonicAllowed => _v[(int)Rule.DemonicPath] > 0.5f;

        public static bool IsToggle(Rule r) => r == Rule.DemonicPath;

        public static float DefaultOf(Rule r) => Default[(int)r];

        public bool IsDefault(Rule r) => Mathf.Abs(_v[(int)r] - Default[(int)r]) < 0.001f;

        // Snapped to the rule's step so the same clicks always give the same value.
        public float Set(Rule r, float value)
        {
            int i = (int)r;
            float v = Mathf.Clamp(value, Min[i], Max[i]);
            v = Mathf.Round(v / Step[i]) * Step[i];
            _v[i] = Mathf.Clamp(v, Min[i], Max[i]);
            return _v[i];
        }

        public string Describe(Rule r) =>
            IsToggle(r) ? (_v[(int)r] > 0.5f ? "bật" : "tắt") : $"×{_v[(int)r]:0.##}";

        public void HashInto(ref ulong h)
        {
            foreach (float v in _v) StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(v));
        }
    }

    public sealed class SetRuleCommand : IWorldCommand
    {
        public readonly Rule Rule;
        public readonly float Value;

        public SetRuleCommand(Rule rule, float value)
        {
            Rule = rule;
            Value = value;
        }

        public void Apply(Simulation sim)
        {
            if (Rule >= Rule.Count) return;
            sim.Rules.Set(Rule, Value);
            sim.Events.Add(sim.Clock.Tick, EventKind.Divine, 1, $"Thiên Đạo sửa quy luật: {WorldRules.Names[(int)Rule]} {sim.Rules.Describe(Rule)}.");
        }
    }
}
