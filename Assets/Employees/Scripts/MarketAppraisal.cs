using System;
using System.Collections.Generic;

namespace TakeOver.Employees
{
    public static class MarketAppraisal
    {
        public enum Direction { Unknown, Up, Down }

        public readonly struct Result
        {
            public readonly Direction performance;
            public readonly Direction market;

            public Result(Direction performance, Direction market)
            {
                this.performance = performance;
                this.market = market;
            }
        }

        public static int getCompanyLimit(int legwork) =>
            1 + (int)Math.Round(legwork / 5d, MidpointRounding.AwayFromZero);

        public static int getIndicatorCount(int analysis) =>
            2 + (int)Math.Round(analysis / 5d, MidpointRounding.AwayFromZero);

        public static Result evaluate(Company company, int analysis, Random random)
        {
            CompanyState state = company.state;
            float[] directions =
            {
                state.getDerivedStat(CompanyDerivedStat.REVENUE_GROWTH),
                state.getDerivedStat(CompanyDerivedStat.NET_PROFIT_GROWTH),
                state.getDerivedStat(CompanyDerivedStat.NET_MARGIN_TREND),
                state.getStat(CompanyStat.REPUTATION_B2C) - 50f,
                state.getStat(CompanyStat.REPUTATION_B2B) - 50f,
                state.getStat(CompanyStat.MARKET_POSITION) - 50f
            };
            return evaluate(directions, getIndicatorCount(analysis), random);
        }

        // 0〜2는 실적 추세, 3〜5는 시장 평가 재료
        public static Result evaluate(IReadOnlyList<float> directions, int indicatorCount, Random random)
        {
            int[] indices = { 0, 1, 2, 3, 4, 5 };
            int performance = 0;
            int market = 0;
            for (int i = 0; i < indicatorCount; i++)
            {
                int selected = random.Next(i, indices.Length);
                int index = indices[selected];
                indices[selected] = indices[i];
                indices[i] = index;
                int vote = Math.Sign(directions[index]);
                if (index < 3) performance += vote;
                else market += vote;
            }
            return new Result(toDirection(performance), toDirection(market));
        }

        private static Direction toDirection(int vote) =>
            vote > 0 ? Direction.Up : vote < 0 ? Direction.Down : Direction.Unknown;

        public static string describe(Direction direction) =>
            direction == Direction.Up ? "오를 쪽" : direction == Direction.Down ? "내릴 쪽" : "알 수 없음";
    }
}
