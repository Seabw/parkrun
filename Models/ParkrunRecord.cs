namespace ParkrunScraper.Models;

public class ParkrunRecord
{
    public string EventDate { get; set; } = string.Empty;
    public string ClubName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string EventNumber { get; set; } = string.Empty;
    public string OverallPosition { get; set; } = string.Empty;
    public string GenderPosition { get; set; } = string.Empty;
    public string Parkrunner { get; set; } = string.Empty;
    public string ParkrunnerId { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string EventTotalParticipants { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;

    // Enriched runner fields from event results
    public int TotalRuns { get; set; }
    public int TotalVols { get; set; }
    public string AgeGroup { get; set; } = string.Empty;
    public string Achievement { get; set; } = string.Empty;
    public string Milestone { get; set; } = string.Empty;
    public string MilestoneBadgeBg { get; set; } = string.Empty;
    public string MilestoneBadgeText { get; set; } = string.Empty;
    public bool IsPb { get; set; }
    public bool IsFirstTimer { get; set; }
}

public class ConsolidatedReportMetadata
{
    public string ClubName { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string TotalMembers { get; set; } = string.Empty;
    public string TotalParticipants { get; set; } = string.Empty;
    public Dictionary<string, string> EventResultUrls { get; set; } = new();
}

public class ParkrunMilestone
{
    public string ParkrunnerName { get; set; } = string.Empty;
    public string ParkrunnerId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string MilestoneType { get; set; } = "Run"; // "Run" or "Volunteer"
    public int MilestoneCount { get; set; }
    public string MilestoneTitle { get; set; } = string.Empty;
    public string MilestoneClub { get; set; } = string.Empty;
    public string BadgeBgColor { get; set; } = "#283593";
    public string BadgeTextColor { get; set; } = "#FFFFFF";
    public string FinishTime { get; set; } = string.Empty;
    public string Achievement { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
}
