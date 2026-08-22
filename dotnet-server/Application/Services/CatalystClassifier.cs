using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Enums;
using TradingScanner.Domain.Models;

namespace TradingScanner.Application.Services;

/// <summary>Ordered, side-effect-free rules make classifications repeatable and auditable.</summary>
public sealed class CatalystClassifier : ICatalystClassifier
{
    private static readonly Rule[] Rules =
    [
        new(CatalystType.Acquisition, 5, ["acquisition", "acquire", "merger agreement"]),
        new(CatalystType.Buyout, 5, ["buyout", "takeover"]),
        new(CatalystType.Fda, 5, ["fda approval", "fda clearance", "food and drug administration"]),
        new(CatalystType.ClinicalTrial, 5, ["phase 3", "phase iii", "pivotal trial", "primary endpoint met"]),
        new(CatalystType.GovernmentContract, 5, ["government contract", "department of defense", "federal contract"]),
        new(CatalystType.MajorContract, 4, ["major contract", "multi-year contract", "purchase order"]),
        new(CatalystType.Earnings, 4, ["earnings", "quarterly results", "revenue guidance", "financial results"]),
        new(CatalystType.Partnership, 4, ["partnership", "strategic collaboration", "joint venture"]),
        new(CatalystType.Patent, 3, ["patent granted", "receives patent"]),
        new(CatalystType.AnalystAction, 2, ["price target", "upgraded", "downgraded", "initiates coverage"]),
        new(CatalystType.Offering, 1, ["public offering", "registered direct", "securities offering"]),
        new(CatalystType.Dilution, 1, ["at-the-market offering", "warrant exercise", "dilution"]),
        new(CatalystType.ReverseSplit, 1, ["reverse stock split"]),
        new(CatalystType.CorporateUpdate, 2, ["corporate update", "business update"]),
    ];

    public CatalystClassification Classify(string headline, string? summary = null)
    {
        var text = $"{headline} {summary}";
        foreach (var rule in Rules)
            if (rule.Terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
                return new(rule.Type, rule.Quality);
        return string.IsNullOrWhiteSpace(headline) ? new(CatalystType.None, 0) : new(CatalystType.Unknown, 1);
    }

    private sealed record Rule(CatalystType Type, int Quality, string[] Terms);
}
