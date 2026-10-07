using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;

namespace ThienDao.Sim
{
    // Naming helpers. The names themselves live in Resources/Lore/*.json (LoreDatabase) and each world draws its
    // own share of them (WorldData.Lore); only the rules of naming are here.
    public static class Lore
    {
        // When a world's named pool runs out: two syllables and a sect suffix, e.g. "Thanh Hạc Tông".
        public static string GeneratedSectName(WorldLore lore, ref DetRandom rng)
        {
            var syl = lore.Syllables;
            int a = rng.Range(0, syl.Length), b = rng.Range(0, syl.Length - 1);
            if (b >= a) b++;
            return $"{syl[a]} {syl[b]} {lore.SectSuffixes[rng.Range(0, lore.SectSuffixes.Length)]}";
        }

        // Thôn / Trấn / Thành by size.
        public static string Tier(int population) => population >= 400 ? "Thành" : population >= 150 ? "Trấn" : "Thôn";

        // The pill that helps through each gate (indexed by the realm being broken into). Part of the rules,
        // so fixed rather than drawn per world.
        public static string PillFor(Realm next) =>
            next == Realm.TrucCo ? "Trúc Cơ Đan" : next == Realm.KetDan ? "Kết Kim Đan" : next == Realm.NguyenAnh ? "Bổ Thiên Đan" : "Hoàng Long Đan";

        // Hands out each pool entry once per world before inventing two-syllable names.
        public sealed class Picker
        {
            readonly List<string> _left;
            readonly HashSet<string> _used = new HashSet<string>();
            readonly string[] _syllables;

            public Picker(string[] pool, string[] syllables)
            {
                _left = new List<string>(pool);
                _syllables = syllables;
            }

            public string Next(ref DetRandom rng)
            {
                if (_left.Count > 0)
                {
                    int k = rng.Range(0, _left.Count);
                    string name = _left[k];
                    _left.RemoveAt(k);
                    _used.Add(name);
                    return name;
                }
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    int a = rng.Range(0, _syllables.Length), b = rng.Range(0, _syllables.Length - 1);
                    if (b >= a) b++;
                    string name = _syllables[a] + " " + _syllables[b];
                    if (_used.Add(name)) return name;
                }
                return _syllables[rng.Range(0, _syllables.Length)] + " " + _used.Count;
            }
        }
    }
}
