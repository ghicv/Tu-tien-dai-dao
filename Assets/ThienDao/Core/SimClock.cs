using UnityEngine;

namespace ThienDao.Core
{
    public enum Season { Xuan, Ha, Thu, Dong }

    // 1 tick = 1 day. Calendar is fixed (30-day months, 360-day years) so system cadences divide evenly.
    public sealed class SimClock
    {
        public const int DaysPerMonth = 30;
        public const int MonthsPerYear = 12;
        public const int DaysPerSeason = 90;
        public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

        public static readonly string[] SeasonNames = { "Xuân", "Hạ", "Thu", "Đông" };

        public long Tick { get; private set; }

        public int Year => (int)(Tick / DaysPerYear) + 1;
        public int Month => (int)(Tick % DaysPerYear / DaysPerMonth) + 1;
        public int Day => (int)(Tick % DaysPerMonth) + 1;
        public Season Season => (Season)(Tick % DaysPerYear / DaysPerSeason);
        public float YearFraction => Tick % DaysPerYear / (float)DaysPerYear;

        public bool IsMonthStart => Tick % DaysPerMonth == 0;
        public bool IsSeasonStart => Tick % DaysPerSeason == 0;
        public bool IsYearStart => Tick % DaysPerYear == 0;

        public void Advance() => Tick++;

        // Offset added to the yearly-mean temperature (0..1 scale): warmest mid-Hạ, coldest mid-Đông.
        public float SeasonalTemperatureOffset => Mathf.Cos(2f * Mathf.PI * (YearFraction - 0.375f)) * 0.12f;

        public string DateText => $"Năm {Year} · Tháng {Month} · Ngày {Day} · {SeasonNames[(int)Season]}";
    }
}
