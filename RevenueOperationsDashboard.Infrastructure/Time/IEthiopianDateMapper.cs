namespace RevenueOperationsDashboard.Infrastructure.Time;

public interface IEthiopianDateMapper
{
    DateOnly FromEthiopian(string etDate);
    string ToEthiopian(DateOnly date);
    EthiopianDateDto ToEthiopianDate(DateOnly date);
}
