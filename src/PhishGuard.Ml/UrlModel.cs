using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using PhishGuard.Core.Urls;

namespace PhishGuard.Ml;

/// <summary>One row fed to ML.NET. Vector sizes must match <see cref="UrlFeatureExtractor"/>.</summary>
public class UrlModelInput
{
    public const int LexicalSize = UrlFeatureExtractor.LexicalCount; // 29
    public const int BrandSize = 11;                                  // Count - LexicalCount

    public string Url { get; set; } = "";
    [VectorType(LexicalSize)] public float[] Lexical { get; set; } = [];
    [VectorType(BrandSize)] public float[] Brand { get; set; } = [];
    public bool Label { get; set; }

    public static UrlModelInput Create(UrlFeatureExtractor extractor, string url, bool label = false)
    {
        var f = extractor.Extract(url);
        return new UrlModelInput
        {
            Url = url,
            Lexical = f[..LexicalSize],
            Brand = f[LexicalSize..],
            Label = label
        };
    }

    static UrlModelInput()
    {
        if (LexicalSize + BrandSize != UrlFeatureExtractor.Count)
            throw new InvalidOperationException($"UrlModelInput vector sizes ({LexicalSize}+{BrandSize}) don't match UrlFeatureExtractor.Count ({UrlFeatureExtractor.Count}).");
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
    /// (catches look-alike spellings and kit paths the hand-made features miss).
    /// </summary>
    public static IEstimator<ITransformer> Full(MLContext ml, int iterations = 300) =>
        ml.Transforms.Text.TokenizeIntoCharactersAsKeys("UrlChars", nameof(UrlModelInput.Url))
            .Append(ml.Transforms.Text.ProduceHashedNgrams("UrlGrams", "UrlChars", numberOfBits: 15, ngramLength: 4, useAllLengths: true))
            .Append(ml.Transforms.Concatenate("Features", nameof(UrlModelInput.Lexical), nameof(UrlModelInput.Brand), "UrlGrams"))
            .Append(ml.BinaryClassification.Trainers.LightGbm(Options(iterations)));

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
        Seed = 42
    };
}
