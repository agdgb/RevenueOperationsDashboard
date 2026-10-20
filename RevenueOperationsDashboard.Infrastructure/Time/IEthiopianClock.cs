namespace RevenueOperationsDashboard.Infrastructure.Time;

public interface IEthiopianClock
{
    /// <summary>
    /// Current Ethiopian date (EC) based on "now" in the app.
    /// </summary>
    EthiopianDateDto Today();

    /// <summary>
    /// Convert a Gregorian date to Ethiopian date.
    /// </summary>
    EthiopianDateDto FromGregorian(DateTime gregorian);

    /// <summary>
    /// Convert an Ethiopian date to Gregorian date.
    /// </summary>
    DateTime ToGregorian(int year, int month, int day);

    EthiopianDateDto Today(DateTime gregorianLocalDate);
}

public readonly record struct EthiopianDateDto(int Year, int Month, int Day)
{
    public bool IsFirstDayOfMonth => Day == 1;
    public bool IsMonthPagume => Month == 13;
}
