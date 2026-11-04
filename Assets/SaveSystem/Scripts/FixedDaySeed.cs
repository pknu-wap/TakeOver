using System;

namespace TakeOver.SaveSystem
{
    public static class FixedDaySeed
    {
        private static System.Random random;

        public static int seed { get; private set; }

        /// <summary>새 게임 시작 시 현재 시분초로 시드 생성</summary>
        public static int generateSeed()
        {
            DateTime time = DateTime.Now;
            int seed = time.Hour * 10000 + time.Minute * 100 + time.Second;
            changeSeed(seed);
            return seed;
        }

        /// <summary>지정한 시드 적용과 난수 순서 초기화</summary>
        public static void changeSeed(int seed)
        {
            FixedDaySeed.seed = seed;
            random = new System.Random(seed);
        }

        /// <summary>같은 시드의 난수 순서를 이어서 사용</summary>
        public static float range(int seed, float min, float max)
        {
            if (random == null || seed != FixedDaySeed.seed)
            {
                changeSeed(seed);
            }

            double ratio = random.NextDouble();
            double value = min + ratio * (max - min);
            return (float)value;
        }
    }
}
