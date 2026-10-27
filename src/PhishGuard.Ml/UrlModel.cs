using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;

namespace PhishGuard.Ml;

/// <summary>One row fed to ML.NET. Vector sizes must match <see cref="UrlFeatureExtractor"/> and <see cref="EvidenceFeatures"/>.</summary>
public class UrlModelInput
{
    public const int LexicalSize = UrlFeatureExtractor.LexicalCount; // 29
    public const int BrandSize = 13;                                  // Count - LexicalCount
    public const int EvidenceSize = EvidenceFeatures.Size;            // 8

    /// <summary>The URL the model judges: for a short link, its expanded destination.</summary>
    public string Url { get; set; } = "";
    [VectorType(LexicalSize)] public float[] Lexical { get; set; } = [];
    [VectorType(BrandSize)] public float[] Brand { get; set; } = [];
    [VectorType(EvidenceSize)] public float[] Evidence { get; set; } = [];
    public bool Label { get; set; }

    /// <param name="url">The effective URL (use <see cref="Effective"/> to resolve short links first).</param>
    /// <param name="evidence">Evidence for the URL's registered domain, or null if none is available.</param>
    public static UrlModelInput Create(UrlFeatureExtractor extractor, string url, bool label = false,
        DomainEvidence? evidence = null, DateTime? seenAtUtc = null)
    {
        var f = extractor.Extract(url);
        return new UrlModelInput
        {
            Url = url,
            Lexical = f[..LexicalSize],
            Brand = f[LexicalSize..],
            Evidence = EvidenceFeatures.From(evidence, seenAtUtc ?? DateTime.UtcNow),
            Label = label
        };
    }

    /// <summary>A short link's destination when its expansion is known, otherwise the URL itself.</summary>
    public static string Effective(string url, IReadOnlyDictionary<string, string> expansions) =>
        expansions.TryGetValue(url, out var expanded) ? expanded : url;

    static UrlModelInput()
    {
        if (LexicalSize + BrandSize != UrlFeatureExtractor.Count)
            throw new InvalidOperationException($"UrlModelInput vector sizes ({LexicalSize}+{BrandSize}) don't match UrlFeatureExtractor.Count ({UrlFeatureExtractor.Count}).");
        if (EvidenceSize != EvidenceFeatures.Names.Length)
            throw new InvalidOperationException($"EvidenceFeatures.Size ({EvidenceSize}) doesn't match its names ({EvidenceFeatures.Names.Length}).");
    }
}

public class UrlPrediction
{
    public bool PredictedLabel { get; set; }
    public float Score { get; set; }
    /// <summary>Calibrated probability of phishing (LightGBM + Platt calibration).</summary>
    public float Probability { get; set; }
}

public static class UrlModelPipelines
{
    /// <summary>
    /// PhishGuard model: lexical + brand-aware features + hashed character 1–4-grams of the raw URL
    /// (catches look-alike spellings and kit paths the hand-made features miss), plus domain evidence
    /// when <paramref name="useEvidence"/> is on.
    /// </summary>
    public static IEstimator<ITransformer> Full(MLContext ml, int iterations = 300, bool useEvidence = true)
    {
        string[] columns = useEvidence
            ? [nameof(UrlModelInput.Lexical), nameof(UrlModelInput.Brand), nameof(UrlModelInput.Evidence), "UrlGrams"]
            : [nameof(UrlModelInput.Lexical), nameof(UrlModelInput.Brand), "UrlGrams"];
        return ml.Transforms.Text.TokenizeIntoCharactersAsKeys("UrlChars", nameof(UrlModelInput.Url))
            .Append(ml.Transforms.Text.ProduceHashedNgrams("UrlGrams", "UrlChars", numberOfBits: 15, ngramLength: 4, useAllLengths: true))
            .Append(ml.Transforms.Concatenate("Features", columns))
            .Append(ml.BinaryClassification.Trainers.LightGbm(Options(iterations)));
    }

    /// <summary>Stand-in for the sponsor's current approach: engineered URL-structure features only.</summary>
    public static IEstimator<ITransformer> Baseline(MLContext ml, int iterations = 300) =>
        ml.Transforms.Concatenate("Features", nameof(UrlModelInput.Lexical))
            .Append(ml.BinaryClassification.Trainers.LightGbm(Options(iterations)));

    private static LightGbmBinaryTrainer.Options Options(int iterations) => new()
    {
        LabelColumnName = nameof(UrlModelInput.Label),
        FeatureColumnName = "Features",
        NumberOfIterations = iterations,
        NumberOfLeaves = 63,
        LearningRate = 0.1,
        MinimumExampleCountPerLeaf = 20,
        // Each tree sees 80% of features, so the model can't lean only on character n-grams and keeps using the
        // general structural / evidence features: generalises better to tactics that are absent from training.
        Booster = new GradientBooster.Options { FeatureFraction = 0.8 },
        Seed = 42
    };
}
