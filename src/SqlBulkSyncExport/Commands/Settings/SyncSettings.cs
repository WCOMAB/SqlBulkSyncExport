using System.ComponentModel;

namespace SqlBulkSyncExport.Commands.Settings;

public sealed class SyncSettings : CommandSettings
{
    [CommandArgument(0, "<config>")]
    [Description("Path to the export configuration YAML file.")]
    public string ConfigPath { get; init; } = string.Empty;

    [CommandArgument(1, "<state>")]
    [Description("Path to the sync state YAML file.")]
    public string StatePath { get; init; } = string.Empty;

    [CommandArgument(2, "<output>")]
    [Description("Output folder for CSV files.")]
    public string OutputFolder { get; init; } = string.Empty;

    [CommandOption("--seed")]
    [Description("Force a full snapshot export for change-tracking jobs.")]
    public bool Seed { get; init; }

    [CommandOption("--include-table")]
    [Description("Include only the specified table key (repeatable).")]
    public string[]? IncludeTables { get; init; }

    [CommandOption("--exclude-table")]
    [Description("Exclude the specified table key (repeatable).")]
    public string[]? ExcludeTables { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            return ValidationResult.Error("Config path is required.");
        }

        if (string.IsNullOrWhiteSpace(StatePath))
        {
            return ValidationResult.Error("State path is required.");
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            return ValidationResult.Error("Output folder is required.");
        }

        return ValidationResult.Success();
    }
}
