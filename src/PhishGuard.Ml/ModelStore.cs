using System.Text.Json;
using Microsoft.ML;

namespace PhishGuard.Ml;

/// <summary>Everything Scoring needs to know about a trained model, saved next to model.zip as model.json.</summary>
/// <param name="FeatureCount">URL features the model was trained with; Scoring refuses a model whose count differs.</param>
/// <param name="UsesEvidence">Whether the model also expects domain-evidence features.</param>
public sealed record ModelMetadata(
    string Version,
    DateTime TrainedAtUtc,
    BandPolicy Bands,
    int TrainRows,
    int TestRows,
    Dictionary<string, double> Metrics,
    int FeatureCount = 0,
    bool UsesEvidence = false);

public static class ModelStore
{
    public const string ModelFile = "model.zip";
    public const string MetadataFile = "model.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The repo's <c>models</c> folder: walks up from the working directory (then the binary folder) to PhishGuard.sln.</summary>
    public static string DefaultDirectory()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "PhishGuard.sln")))
                    return Path.Combine(dir.FullName, "models");
        return Path.Combine(Directory.GetCurrentDirectory(), "models");
    }

    public static void Save(MLContext ml, ITransformer model, DataViewSchema inputSchema, ModelMetadata metadata, string directory)
    {
        Directory.CreateDirectory(directory);
        ml.Model.Save(model, inputSchema, Path.Combine(directory, ModelFile));
        File.WriteAllText(Path.Combine(directory, MetadataFile), JsonSerializer.Serialize(metadata, Json));
    }

    public static bool Exists(string directory) =>
        File.Exists(Path.Combine(directory, ModelFile)) && File.Exists(Path.Combine(directory, MetadataFile));

    public static (ITransformer Model, ModelMetadata Metadata) Load(MLContext ml, string directory)
    {
        var model = ml.Model.Load(Path.Combine(directory, ModelFile), out _);
        var metadata = JsonSerializer.Deserialize<ModelMetadata>(File.ReadAllText(Path.Combine(directory, MetadataFile)))
            ?? throw new InvalidDataException($"{MetadataFile} is empty.");
        return (model, metadata);
    }
}
