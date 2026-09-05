using System.Text.Json;

namespace VerificationHarness.Planning;

public interface IVerificationPlanWriter
{
    string Serialize(VerificationPlan plan);
}

public sealed class VerificationPlanWriter : IVerificationPlanWriter
{
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize(VerificationPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        return JsonSerializer.Serialize(plan, options);
    }
}
