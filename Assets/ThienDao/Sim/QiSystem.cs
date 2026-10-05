using System;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    // Current qi lives on a coarse grid (8×8 cells per block) so diffusion stays cheap even when
    // fast-forwarding centuries. Per-cell detail comes from QiCap; a cell's current qi is its cap plus
    // the bilinear-interpolated surplus/deficit of the surrounding blocks.
    public sealed class QiSystem
    {
        public const int Block = 8;
        public const float Diffusion = 0.18f;    // share pulled toward the neighbour average per month
        public const float RegenPerMonth = 0.06f; // share of the gap to the cap closed per month
        public const float MaxStored = WorldData.MaxQi * 2f;

        readonly WorldData _w;
        public readonly int BW, BH;
        readonly float[] _cap;
        float[] _qi;
        float[] _tmp;

        public event Action Changed;

        public QiSystem(WorldData w)
        {
            _w = w;
            BW = w.W / Block;
            BH = w.H / Block;
            _cap = new float[BW * BH];
            _qi = new float[BW * BH];
            _tmp = new float[BW * BH];
            RebuildCapBlocks(0, 0, w.W - 1, w.H - 1);
            Array.Copy(_cap, _qi, _cap.Length);
        }

        public float BlockQi(int bx, int by) => _qi[by * BW + bx];
        public float BlockCap(int bx, int by) => _cap[by * BW + bx];

        public float TotalQi()
        {
            double s = 0;
            foreach (float q in _qi) s += q;
            return (float)s;
        }

        public void RebuildCapBlocks(int x0, int y0, int x1, int y1)
        {
            int bx0 = Mathf.Clamp(x0 / Block, 0, BW - 1), bx1 = Mathf.Clamp(x1 / Block, 0, BW - 1);
            int by0 = Mathf.Clamp(y0 / Block, 0, BH - 1), by1 = Mathf.Clamp(y1 / Block, 0, BH - 1);
            for (int by = by0; by <= by1; by++)
            for (int bx = bx0; bx <= bx1; bx++)
            {
                int sum = 0;
                for (int y = by * Block; y < (by + 1) * Block; y++)
                for (int x = bx * Block; x < (bx + 1) * Block; x++)
                    sum += _w.QiCap[y * _w.W + x];
                _cap[by * BW + bx] = sum / (float)(Block * Block);
            }
        }

        public void MonthlyStep(float regenMultiplier)
        {
            float regen = RegenPerMonth * regenMultiplier;
            for (int by = 0; by < BH; by++)
            for (int bx = 0; bx < BW; bx++)
            {
                int i = by * BW + bx;
                float q = _qi[i];
                float l = bx > 0 ? _qi[i - 1] : q;
                float r = bx < BW - 1 ? _qi[i + 1] : q;
                float d = by > 0 ? _qi[i - BW] : q;
                float u = by < BH - 1 ? _qi[i + BW] : q;
                q += Diffusion * ((l + r + d + u) * 0.25f - q);
                q += (_cap[i] - q) * regen; // surplus decays and deficit refills toward the cap
                _tmp[i] = Mathf.Clamp(q, 0f, MaxStored);
            }
            var swap = _qi;
            _qi = _tmp;
            _tmp = swap;
            Changed?.Invoke();
        }

        // Adds (or with a negative amount drains) qi around a cell, with linear falloff.
        public void AddQi(int cx, int cy, int radiusCells, float amount)
        {
            float rb = Mathf.Max(1f, radiusCells / (float)Block);
            float bcx = (cx + 0.5f) / Block, bcy = (cy + 0.5f) / Block;
            int bx0 = Mathf.Clamp(Mathf.FloorToInt(bcx - rb), 0, BW - 1), bx1 = Mathf.Clamp(Mathf.CeilToInt(bcx + rb), 0, BW - 1);
            int by0 = Mathf.Clamp(Mathf.FloorToInt(bcy - rb), 0, BH - 1), by1 = Mathf.Clamp(Mathf.CeilToInt(bcy + rb), 0, BH - 1);
            for (int by = by0; by <= by1; by++)
            for (int bx = bx0; bx <= bx1; bx++)
            {
                float dx = bx + 0.5f - bcx, dy = by + 0.5f - bcy;
                float k = 1f - Mathf.Sqrt(dx * dx + dy * dy) / (rb + 0.5f);
                if (k <= 0f) continue;
                int i = by * BW + bx;
                _qi[i] = Mathf.Clamp(_qi[i] + amount * k, 0f, MaxStored);
            }
            Changed?.Invoke();
        }

        public float SampleQi(int x, int y)
        {
            float fx = (x + 0.5f) / Block - 0.5f, fy = (y + 0.5f) / Block - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            int ax = Mathf.Clamp(x0, 0, BW - 1), bx = Mathf.Clamp(x0 + 1, 0, BW - 1);
            int ay = Mathf.Clamp(y0, 0, BH - 1), by = Mathf.Clamp(y0 + 1, 0, BH - 1);
            float d00 = _qi[ay * BW + ax] - _cap[ay * BW + ax];
            float d10 = _qi[ay * BW + bx] - _cap[ay * BW + bx];
            float d01 = _qi[by * BW + ax] - _cap[by * BW + ax];
            float d11 = _qi[by * BW + bx] - _cap[by * BW + bx];
            float delta = Mathf.Lerp(Mathf.Lerp(d00, d10, tx), Mathf.Lerp(d01, d11, tx), ty);
            return Mathf.Clamp(_w.QiCap[y * _w.W + x] + delta, 0f, MaxStored);
        }

        public void HashInto(ref ulong h)
        {
            foreach (float q in _qi) StateHash.Add(ref h, BitConverter.SingleToInt32Bits(q));
        }
    }
}
