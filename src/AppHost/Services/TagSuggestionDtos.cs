namespace AppHost.Services;

public sealed record DetectedTagSuggestion(
    Guid? TagId,
    string TagName,
    string Type,
    string Source,
    double Confidence,
    string Reason,
    string Fingerprint,
    DateTimeOffset CreatedAt
);

public sealed record TagSuggestionDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? TagId,
    string TagName,
    string Type,
    string Source,
    double Confidence,
    string Reason,
    DateTimeOffset CreatedAt,
    string Status
);

public sealed record TagSuggestionRegressionProjectReport(
    int BaselineAcceptedCount,
    int BaselineRejectedCount,
    int AcceptedMissingCount,
    int RejectedMissingCount,
    int AddedCount
);
