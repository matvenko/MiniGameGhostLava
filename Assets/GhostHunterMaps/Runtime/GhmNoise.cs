using UnityEngine;

namespace GhostHunterMaps
{
    // The exact hash and value noise the shipped surfaces use. Copied rather
    // than shared so the game's own scripts stay untouched, but bit-for-bit
    // identical: the preview only matches production because these agree.
    public static class GhmNoise
    {
        public static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        public static float Smooth(float x, float z)
        {
            int i = Mathf.FloorToInt(x), j = Mathf.FloorToInt(z);
            float fx = x - i, fz = z - j;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);

            float a = Hash01(i, j, 101), b = Hash01(i + 1, j, 101);
            float c = Hash01(i, j + 1, 101), d = Hash01(i + 1, j + 1, 101);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        public static float Fbm(float x, float z) =>
            Smooth(x, z) * 0.65f + Smooth(x * 2.4f + 11.3f, z * 2.4f + 5.7f) * 0.35f;
    }

    // Small deterministic PRNG. UnityEngine.Random would work, but it is global
    // state: generating a preview would then shift every other random draw in
    // the editor, and two runs of the same seed would stop matching.
    public struct GhmRandom
    {
        private uint _state;

        public GhmRandom(int seed)
        {
            unchecked { _state = (uint)(seed * 747796405 + 2891336453); }
            if (_state == 0) _state = 0x9E3779B9;
        }

        public uint NextUInt()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }

        public float Value => (NextUInt() & 0xFFFFFF) / (float)0x1000000;
        public float Range(float min, float max) => min + (max - min) * Value;
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
