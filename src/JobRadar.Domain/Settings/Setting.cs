namespace JobRadar.Domain.Settings;

/// <summary>One configuration section kept in the database: its key (<c>Matching:Profiles</c>), the JSON value and when it last changed.</summary>
public sealed record Setting(string Key, string Json, DateTimeOffset UpdatedAt);
