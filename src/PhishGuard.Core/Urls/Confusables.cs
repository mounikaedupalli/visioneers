using System.Globalization;
using System.Text;

namespace PhishGuard.Core.Urls;

/// <summary>
/// Maps visually confusable characters to the Latin letter they imitate (a "skeleton", in the spirit of
/// Unicode TR39), so "раураl" written with Cyrillic letters becomes "paypal" and can be compared with brands.
/// Deliberately broad and independent of the data generator: it covers Cyrillic, Greek, Armenian, Latin
/// look-alikes (dotless i, script g, small capitals), fullwidth forms, digits and accent-stripped letters.
/// </summary>
public static class Confusables
{
    private static readonly Dictionary<char, char> Map = Build();

    /// <summary>Lower-cased, accent-stripped, confusables folded to Latin. Leaves unknown characters as they are.</summary>
    public static string Skeleton(string label)
    {
        var sb = new StringBuilder(label.Length);
        foreach (var raw in label.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark) continue; // é → e
            var c = char.ToLowerInvariant(raw);
            if (c is >= '！' and <= '～') c = (char)(c - 0xFEE0);                            // fullwidth → ASCII
            sb.Append(Map.TryGetValue(c, out var latin) ? latin : c);
        }
        return sb.ToString();
    }

    /// <summary>True when one label mixes Latin letters with letters from another script (classic homoglyph sign).</summary>
    public static bool IsMixedScript(string label)
    {
        bool latin = false, other = false;
        foreach (var c in label)
        {
            if (!char.IsLetter(c)) continue;
            if (c < 'ɐ') latin = true;   // Basic Latin + Latin-1 + Latin Extended-A/B
            else other = true;
            if (latin && other) return true;
        }
        return false;
    }

    /// <summary>True when the label contains a letter outside Basic Latin that folds to a Latin letter.</summary>
    public static bool HasConfusable(string label) => label.Any(c => c > '\u007F' && Map.ContainsKey(char.ToLowerInvariant(c)));

    private static Dictionary<char, char> Build()
    {
        var m = new Dictionary<char, char>();
        void Add(char latin, string lookalikes) { foreach (var c in lookalikes) m[c] = latin; }

        // Cyrillic
        Add('a', "аӑӓ"); Add('b', "ьъв"); Add('c', "сҫ"); Add('d', "ԁ"); Add('e', "еёэє"); Add('g', "ԍ");
        Add('h', "һн"); Add('i', "іїӏ"); Add('j', "ј"); Add('k', "кқ"); Add('l', "ӏ"); Add('m', "м");
        Add('n', "п"); Add('o', "оӧө"); Add('p', "р"); Add('q', "ԛ"); Add('r', "г"); Add('s', "ѕ");
        Add('t', "т"); Add('u', "џ"); Add('w', "ԝш"); Add('x', "хҳ"); Add('y', "уүў");
        // Greek
        Add('a', "αά"); Add('b', "β"); Add('e', "εέ"); Add('h', "η"); Add('i', "ιίϊ"); Add('k', "κ");
        Add('n', "ηπ"); Add('o', "οόσ"); Add('p', "ρ"); Add('t', "τ"); Add('u', "υύ"); Add('v', "ν");
        Add('w', "ω"); Add('x', "χ"); Add('y', "γ");
        // Armenian
        Add('d', "ժ"); Add('g', "ց"); Add('h', "հ"); Add('n', "ոռ"); Add('o', "օ"); Add('q', "զ");
        Add('s', "ѕ"); Add('u', "ս"); Add('w', "ա");
        // Latin look-alikes
        Add('a', "ɑ"); Add('d', "ɗ"); Add('g', "ɡɢ"); Add('h', "ʜ"); Add('i', "ıɪ"); Add('l', "ʟ");
        Add('n', "ɴ"); Add('o', "ᴏ"); Add('r', "ʀ"); Add('u', "ᴜ"); Add('v', "ᴠ"); Add('w', "ᴡ"); Add('z', "ᴢ");
        // Digits and symbols used in leetspeak domains
        Add('o', "0"); Add('l', "1|"); Add('e', "3"); Add('a', "4@"); Add('s', "5$"); Add('t', "7"); Add('b', "8");
        return m;
    }
}
