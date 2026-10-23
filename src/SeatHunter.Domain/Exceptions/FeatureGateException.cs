namespace SeatHunter.Domain.Exceptions;

public class FeatureGateException : Exception
{
    public string? FeatureName { get; }
    public string? RequiredTier { get; }

    public FeatureGateException(string message) : base(message)
    {
    }

    public FeatureGateException(string featureName, string requiredTier, string message) : base(message)
    {
        FeatureName = featureName;
        RequiredTier = requiredTier;
    }
}
