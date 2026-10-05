using ThienDao.Core;
using UnityEngine;

namespace ThienDao.World
{
    // Seeded gradient noise; Mathf.PerlinNoise has no seed and loses precision at large offsets.
    public static class Noise
    {
        static readonly float[] GX = { 1f, -1f, 0f, 0f, 0.70710678f, -0.70710678f, 0.70710678f, -0.70710678f };
        static readonly float[] GY = { 0f, 0f, 1f, -1f, 0.70710678f, 0.70710678f, -0.70710678f, -0.70710678f };

        static float G(uint seed, int ix, int iy, float dx, float dy)
        {
            int k = (int)(Hash.U32(seed, ix, iy) & 7u);
            return GX[k] * dx + GY[k] * dy;
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        public static float Perlin(uint seed, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            float u = Fade(fx);
            float v = Fade(fy);
            float n00 = G(seed, x0, y0, fx, fy);
            float n10 = G(seed, x0 + 1, y0, fx - 1f, fy);
            float n01 = G(seed, x0, y0 + 1, fx, fy - 1f);
            float n11 = G(seed, x0 + 1, y0 + 1, fx - 1f, fy - 1f);
            float a = n00 + (n10 - n00) * u;
            float b = n01 + (n11 - n01) * u;
            return (a + (b - a) * v) * 1.4142f;
        }

        public static float Fbm(uint seed, float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Perlin(seed + (uint)i * 1013u, x * freq, y * freq);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / norm;
        }

        public static float Ridged(uint seed, float x, float y, int octaves)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(seed + (uint)i * 7919u, x * freq, y * freq));
                sum += amp * n * n;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
