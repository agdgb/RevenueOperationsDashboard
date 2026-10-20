using EthiopianCalendar;

namespace RevenueOperationsDashboard.Infrastructure.Time;

public sealed class EthiopianCalendarClock : IEthiopianClock
{
    private readonly TimeProvider _time;

    public EthiopianCalendarClock(TimeProvider time) => _time = time;

    public EthiopianDateDto Today()
    {
        return FromGregorian(_time.GetLocalNow().DateTime);
    }

    public EthiopianDateDto Today(DateTime gregorianLocalDate)
    {
        return FromGregorian(gregorianLocalDate);
    }

    public EthiopianDateDto FromGregorian(DateTime gregorian)
    {
        var et = new EthiopianDate(gregorian);
        return new EthiopianDateDto(et.Year, et.Month, et.Day);
    }

    public DateTime ToGregorian(int year, int month, int day)
    {
        var et = new EthiopianDate(year, month, day);
        return et.DateTime;
    }
}
