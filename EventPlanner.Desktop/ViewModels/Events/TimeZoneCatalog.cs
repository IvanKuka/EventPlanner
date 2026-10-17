namespace EventPlanner.Desktop.ViewModels.Events;

/// <summary>
/// IANA time zone ids offered in the editor. Windows reports Windows ids for system zones, so
/// they are converted; only IANA ids are stored so projects open identically on macOS.
/// </summary>
internal static class TimeZoneCatalog
{
    public static IReadOnlyList<string> IanaIds { get; } = Build();

    public static string LocalIanaId { get; } = ToIana(TimeZoneInfo.Local) ?? "Europe/Bratislava";

    private static string[] Build() =>
        [.. TimeZoneInfo.GetSystemTimeZones()
            .Select(ToIana)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static string? ToIana(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
        {
            return zone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : null;
    }
}
