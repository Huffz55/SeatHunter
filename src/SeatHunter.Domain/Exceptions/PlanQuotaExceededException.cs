namespace SeatHunter.Domain.Exceptions;

public class PlanQuotaExceededException : Exception
{
    public string? Tier { get; }
    public int? MaxAllowed { get; }

    public PlanQuotaExceededException(string message) : base(message)
    {
    }

    public PlanQuotaExceededException(string tier, int maxAllowed, string message) : base(message)
    {
        Tier = tier;
        MaxAllowed = maxAllowed;
    }
}
