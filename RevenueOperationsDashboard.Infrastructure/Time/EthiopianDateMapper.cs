namespace RevenueOperationsDashboard.Infrastructure.Time;

public sealed class EthiopianDateMapper : IEthiopianDateMapper
{
    private readonly IEthiopianClock _clock;

    public EthiopianDateMapper(IEthiopianClock clock)
        => _clock = clock;

    public DateOnly FromEthiopian(string etDate)
    {
        if (string.IsNullOrWhiteSpace(etDate))
            throw new ArgumentException("Date is required.");

        //format: yyyy-MM-dd
        var parts = etDate.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3)
            throw new FormatException("Invalid Ethiopian date format.");

        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);
        var day = int.Parse(parts[2]);

        var gregorian = _clock.ToGregorian(year, month, day);
        return DateOnly.FromDateTime(gregorian);
    }

    public string ToEthiopian(DateOnly date)
    {
        var g = date.ToDateTime(TimeOnly.MinValue);
        var et = _clock.FromGregorian(g);

        return $"{et.Year:D4}-{et.Month:D2}-{et.Day:D2}";
    }

    public EthiopianDateDto ToEthiopianDate(DateOnly date)
    {
        var g = date.ToDateTime(TimeOnly.MinValue);
        var et = _clock.FromGregorian(g);

        return new EthiopianDateDto(et.Year, et.Month, et.Day);
    }
}
