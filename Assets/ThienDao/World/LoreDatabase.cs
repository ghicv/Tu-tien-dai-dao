using System;
using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.World
{
    // The lore of the cultivation world, one file per kind under Resources/Lore/ (editable without touching code):
    //   world.json   continents, seas, the great regions and their kingdoms
    //   sects.json   righteous and demonic sects, sect-name suffixes
    //   people.json  surnames and given names
    //   places.json  village names, volcanoes, thunder grounds, syllables for invented names
    //   items.json   herbs, pháp bảo, natural treasures
    //   beasts.json  yêu thú, their epithets and clans
    // Each world draws its own subset (WorldLore), so no two seeds share the same names.

    [Serializable] public sealed class RegionPool { public string kind; public string[] names; public string[] kingdoms; }
    [Serializable] public sealed class WorldNames { public string[] continents; public string[] seas; public RegionPool[] regions; }
    [Serializable] public sealed class SectNames { public string[] righteousSects; public string[] demonicSects; public string[] sectSuffixes; }
    [Serializable] public sealed class PeopleNames { public string[] surnames; public string[] givenNames; }
    [Serializable] public sealed class PlaceNames { public string[] places; public string[] volcanoes; public string[] thunderPlaces; public string[] syllables; }
    [Serializable] public sealed class ItemNames { public string[] herbs; public string[] treasures; public string[] naturalTreasures; }
    [Serializable] public sealed class BeastNames
    {
        public string[] beasts; public string[] beastEpithets; public string[] beastFromWolf; public string[] beastFromDeer;
        public string[] beastFromRabbit; public string[] beastClans;
    }

    public static class LoreDatabase
    {
        static WorldNames _world;
        static SectNames _sects;
        static PeopleNames _people;
        static PlaceNames _places;
        static ItemNames _items;
        static BeastNames _beasts;

        public static WorldNames World { get { Ensure(); return _world; } }
        public static SectNames Sects { get { Ensure(); return _sects; } }
        public static PeopleNames People { get { Ensure(); return _people; } }
        public static PlaceNames Places { get { Ensure(); return _places; } }
        public static ItemNames Items { get { Ensure(); return _items; } }
        public static BeastNames Beasts { get { Ensure(); return _beasts; } }

        static void Ensure()
        {
            if (_world != null) return;
            _world = Load<WorldNames>("world");
            _sects = Load<SectNames>("sects");
            _people = Load<PeopleNames>("people");
            _places = Load<PlaceNames>("places");
            _items = Load<ItemNames>("items");
            _beasts = Load<BeastNames>("beasts");
        }

        // Re-read the files (after editing them in the Editor).
        public static void Reload() => _world = null;

        static T Load<T>(string file)
        {
            var asset = Resources.Load<TextAsset>("Lore/" + file);
            if (asset == null) throw new InvalidOperationException($"Thiếu dữ liệu thế giới: Resources/Lore/{file}.json");
            return JsonUtility.FromJson<T>(asset.text);
        }

        public static string[] KingdomsOf(RegionKind kind) => Pool(kind)?.kingdoms ?? new string[0];

        public static RegionPool Pool(RegionKind kind)
        {
            foreach (var p in World.regions)
                if (p.kind == kind.ToString()) return p;
            return null;
        }

        public static bool IsDemonicSect(string name) => Array.IndexOf(Sects.demonicSects, name) >= 0;
    }

    // The names one world uses: drawn from the database by its seed, so worlds differ yet a seed always gives the same.
    public sealed class WorldLore
    {
        public string Continent;   // e.g. "Thiên Nam" (the đại lục)
        public string Sea;
        public string[] RegionNames = new string[(int)RegionKind.Count];
        public string[][] Kingdoms = new string[(int)RegionKind.Count][];
        public string[] RighteousSects, DemonicSects, SectSuffixes;
        public string[] Surnames, GivenNames;
        public string[] Places, Volcanoes, ThunderPlaces, Syllables;
        public string[] Herbs, Treasures, NaturalTreasures;
        public string[] Beasts, BeastEpithets, BeastFromWolf, BeastFromDeer, BeastFromRabbit, BeastClans;

        public static WorldLore Draw(uint seed)
        {
            var rng = new DetRandom(seed ^ 0x10AEu);
            var w = LoreDatabase.World;
            var l = new WorldLore
            {
                Continent = One(w.continents, ref rng),
                Sea = One(w.seas, ref rng),
                // Big pools: a share of each, so worlds feel different; small pools are kept whole.
                RighteousSects = Some(LoreDatabase.Sects.righteousSects, 0.6f, 14, ref rng),
                DemonicSects = Some(LoreDatabase.Sects.demonicSects, 0.65f, 8, ref rng),
                SectSuffixes = LoreDatabase.Sects.sectSuffixes,
                Surnames = Some(LoreDatabase.People.surnames, 0.55f, 24, ref rng),
                GivenNames = Some(LoreDatabase.People.givenNames, 0.6f, 36, ref rng),
                Places = Some(LoreDatabase.Places.places, 0.7f, 50, ref rng),
                Volcanoes = Some(LoreDatabase.Places.volcanoes, 0.7f, 6, ref rng),
                ThunderPlaces = Some(LoreDatabase.Places.thunderPlaces, 0.7f, 6, ref rng),
                Syllables = LoreDatabase.Places.syllables,
                Herbs = Some(LoreDatabase.Items.herbs, 0.6f, 8, ref rng),
                Treasures = Some(LoreDatabase.Items.treasures, 0.6f, 10, ref rng),
                NaturalTreasures = Some(LoreDatabase.Items.naturalTreasures, 0.7f, 5, ref rng),
                Beasts = Some(LoreDatabase.Beasts.beasts, 0.7f, 5, ref rng),
                BeastEpithets = Some(LoreDatabase.Beasts.beastEpithets, 0.7f, 12, ref rng),
                BeastFromWolf = LoreDatabase.Beasts.beastFromWolf,
                BeastFromDeer = LoreDatabase.Beasts.beastFromDeer,
                BeastFromRabbit = LoreDatabase.Beasts.beastFromRabbit,
                BeastClans = Some(LoreDatabase.Beasts.beastClans, 0.7f, 6, ref rng),
            };
            for (int k = 1; k < (int)RegionKind.Count; k++)
            {
                var pool = LoreDatabase.Pool((RegionKind)k);
                l.RegionNames[k] = pool != null && pool.names.Length > 0 ? One(pool.names, ref rng) : ((RegionKind)k).ToString();
                l.Kingdoms[k] = Shuffled(pool?.kingdoms ?? new string[0], ref rng);
            }
            l.RegionNames[0] = l.Sea;
            l.Kingdoms[0] = new string[0];
            return l;
        }

        public string PersonName(ref DetRandom rng) => Surnames[rng.Range(0, Surnames.Length)] + " " + GivenNames[rng.Range(0, GivenNames.Length)];

        static string One(string[] pool, ref DetRandom rng) => pool[rng.Range(0, pool.Length)];

        static string[] Shuffled(string[] pool, ref DetRandom rng)
        {
            var a = (string[])pool.Clone();
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        // A random share of the pool (at least `min`, or all of it when smaller), in random order.
        static string[] Some(string[] pool, float share, int min, ref DetRandom rng)
        {
            var a = Shuffled(pool, ref rng);
            int n = Mathf.Min(a.Length, Mathf.Max(min, Mathf.RoundToInt(a.Length * share)));
            var list = new List<string>(a);
            return list.GetRange(0, n).ToArray();
        }
    }
}
