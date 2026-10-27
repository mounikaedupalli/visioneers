using PhishGuard.Core.Domain;

namespace PhishGuard.Ml;

/// <summary>
/// The two thresholds that split scores into Clear / Gray / Confirmed, chosen on a validation slice:
///  - Clear:     below this, we still keep <c>TargetRecall</c> of phishing above it (we rarely clear real phish).
///  - Confirmed: at or above this, precision is at least <c>TargetPrecision</c> (safe to auto-confirm).
/// Everything in between is Gray: grouped into campaigns and sent to the agents/analysts.
/// </summary>
public sealed record BandPolicy(float ClearThreshold, float ConfirmThreshold, double TargetRecall, double TargetPrecision)
{
    public ScoreBand Band(float probability) =>
        probability >= ConfirmThreshold ? ScoreBand.Confirmed
        : probability < ClearThreshold ? ScoreBand.Clear
        : ScoreBand.Gray;

    /// <param name="maxClearThreshold">
    /// Safety cap: never auto-clear a URL the model rates at or above this. Without it, a validation slice the model finds
    /// easy (e.g. repeat campaigns) yields a near-1.0 Clear threshold that silently drops new attacks.
    /// </param>
    /// <param name="minConfirmThreshold">
    /// Safety floor: never auto-confirm below this probability. On an easy validation slice precision is 100% at every
    /// score, which would put Confirmed right at the Clear threshold and leave no Gray band for agents and analysts.
    /// </param>
    public static BandPolicy Choose(IReadOnlyList<(float Probability, bool IsPhishing)> validation, double targetRecall, double targetPrecision,
        float maxClearThreshold = 0.5f, float minConfirmThreshold = 0.9f)
    {
        var phish = validation.Where(v => v.IsPhishing).Select(v => v.Probability).OrderBy(p => p).ToArray();
        if (phish.Length == 0) throw new InvalidOperationException("Validation set has no phishing examples.");

        // Clear threshold: the score that leaves at most (1 - targetRecall) of phishing below it.
        var missAllowed = (int)Math.Floor((1 - targetRecall) * phish.Length);
        var clear = Math.Min(phish[Math.Min(missAllowed, phish.Length - 1)], maxClearThreshold);

        // Confirm threshold: the lowest score whose top-k slice still has precision >= target.
        var sorted = validation.OrderByDescending(v => v.Probability).ToArray();
        var confirm = 1.01f;
        int tp = 0;
        for (var k = 0; k < sorted.Length; k++)
        {
            if (sorted[k].IsPhishing) tp++;
            var precision = (double)tp / (k + 1);
            var nextDiffers = k == sorted.Length - 1 || sorted[k + 1].Probability < sorted[k].Probability;
            if (precision >= targetPrecision && nextDiffers) confirm = sorted[k].Probability;
        }

        return new BandPolicy(clear, Math.Max(Math.Max(confirm, clear), minConfirmThreshold), targetRecall, targetPrecision);
    }
}
