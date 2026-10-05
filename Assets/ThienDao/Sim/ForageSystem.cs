using System;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    // Wild forage on the same coarse grid as qi: grows back each month by season, grazed down by herbivores.
    public sealed class ForageSystem
    {
        public const int Block = 8;

        readonly WorldData _w;
        public readonly int BW, BH;
        readonly float[] _cap;
        readonly float[] _forage;

        public ForageSystem(WorldData w)
        {
            _w = w;
            BW = w.W / Block;
            BH = w.H / Block;
            _cap = new float[BW * BH];
            _forage = new float[BW * BH];
            RebuildCapBlocks(0, 0, w.W - 1, w.H - 1);
            Array.Copy(_cap, _forage, _cap.Length);
        }

        public void RebuildCapBlocks(int x0, int y0, int x1, int y1)
        {
            int bx0 = Mathf.Clamp(x0 / Block, 0, BW - 1), bx1 = Mathf.Clamp(x1 / Block, 0, BW - 1);
            int by0 = Mathf.Clamp(y0 / Block, 0, BH - 1), by1 = Mathf.Clamp(y1 / Block, 0, BH - 1);
            for (int by = by0; by <= by1; by++)
            for (int bx = bx0; bx <= bx1; bx++)
            {
                float sum = 0f;
                for (int y = by * Block; y < (by + 1) * Block; y++)
                for (int x = bx * Block; x < (bx + 1) * Block; x++)
                    sum += TerrainInfo.ForageCapacity[(int)_w.Terrain[y * _w.W + x]];
                int i = by * BW + bx;
                _cap[i] = sum;
                if (_forage[i] > sum) _forage[i] = sum;
            }
        }

        public void MonthlyStep(Season season)
        {
            float regen;
            switch (season)
            {
                case Season.Xuan: regen = 0.35f; break;
                case Season.Ha: regen = 0.3f; break;
                case Season.Thu: regen = 0.15f; break;
                default: regen = 0.08f; break;
            }
            for (int i = 0; i < _forage.Length; i++) _forage[i] += (_cap[i] - _forage[i]) * regen;
        }

        int BlockAt(float x, float y)
        {
            int bx = Mathf.Clamp((int)x / Block, 0, BW - 1), by = Mathf.Clamp((int)y / Block, 0, BH - 1);
            return by * BW + bx;
        }

        public float At(float x, float y) => _forage[BlockAt(x, y)];
        public float CapAt(float x, float y) => _cap[BlockAt(x, y)];

        public float Eat(float x, float y, float amount)
        {
            int i = BlockAt(x, y);
            float eaten = Mathf.Min(amount, _forage[i]);
            _forage[i] -= eaten;
            return eaten;
        }

        public void HashInto(ref ulong h)
        {
            foreach (float f in _forage) StateHash.Add(ref h, BitConverter.SingleToInt32Bits(f));
        }
    }
}
