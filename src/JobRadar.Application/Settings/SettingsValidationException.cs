namespace JobRadar.Application.Settings;

/// <summary>A configuration section that cannot be saved: forbidden key, invalid JSON or rules that would not build.</summary>
public sealed class SettingsValidationException(string message) : Exception(message);
