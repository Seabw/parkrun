using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using ParkrunScraper.Models;

namespace ParkrunScraper.Services;

public class ParkrunMilestoneService
{
    private readonly HttpClient _httpClient;
    private readonly string _milestonesStorePath;

    public ParkrunMilestoneService(HttpClient? httpClient = null, string? milestonesStorePath = null)
    {
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        });

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        if (!string.IsNullOrEmpty(milestonesStorePath))
        {
            _milestonesStorePath = milestonesStorePath;
        }
        else
        {
            string projectDataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
            if (!Directory.Exists(projectDataDir))
            {
                Directory.CreateDirectory(projectDataDir);
            }
            _milestonesStorePath = Path.Combine(projectDataDir, "milestones.json");
        }
    }

    public async Task<List<ParkrunMilestone>> ProcessAndTrackMilestonesAsync(
        ConsolidatedReportMetadata meta,
        List<ParkrunRecord> records,
        string targetClubName,
        string eventDate,
        List<ParkrunVolunteerProfile>? volunteerProfiles = null)
    {
        string cacheBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "data", $"events_{eventDate}");
        if (!Directory.Exists(cacheBaseDir))
        {
            Directory.CreateDirectory(cacheBaseDir);
        }

        // 1. Gather all event result HTMLs and extract runner row attributes
        var runnerEventMap = new Dictionary<string, (int Runs, int Vols, string AgeGroup, string Achievement, bool IsPb, bool IsFirstTimer)>();

        foreach (var kvp in meta.EventResultUrls)
        {
            string eventName = kvp.Key;
            string eventUrl = kvp.Value;
            if (!eventUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !eventUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                eventUrl = "https://www.parkrun.org.uk" + (eventUrl.StartsWith("/") ? "" : "/") + eventUrl;
            }

            var mUrl = Regex.Match(eventUrl, @"parkrun\.[a-z\.]+/([^/]+)/results/(\d+)");
            string slug = mUrl.Success ? mUrl.Groups[1].Value : Regex.Replace(eventName, @"[^a-zA-Z0-9]", "_").ToLowerInvariant();
            string num = mUrl.Success ? mUrl.Groups[2].Value : "latest";
            string cachePath = Path.Combine(cacheBaseDir, $"{slug}_{num}.html");

            string eventHtml = "";
            if (File.Exists(cachePath))
            {
                eventHtml = await File.ReadAllTextAsync(cachePath);
            }
            else
            {
                try
                {
                    eventHtml = await _httpClient.GetStringAsync(eventUrl);
                    if (!string.IsNullOrEmpty(eventHtml) && !eventHtml.Contains("Amazon WAF", StringComparison.OrdinalIgnoreCase))
                    {
                        await File.WriteAllTextAsync(cachePath, eventHtml);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed to fetch event results for {eventName}: {ex.Message}");
                    continue;
                }
            }

            if (string.IsNullOrEmpty(eventHtml)) continue;

            // Parse Results-table-row
            var rowMatches = Regex.Matches(eventHtml, @"<tr class=[\x27\x22]Results-table-row[\x27\x22](.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match row in rowMatches)
            {
                string rContent = row.Groups[1].Value;
                var idMatch = Regex.Match(rContent, @"parkrunner/(\d+)", RegexOptions.IgnoreCase);
                if (!idMatch.Success) continue;
                string pId = idMatch.Groups[1].Value.Trim();

                var runsMatch = Regex.Match(rContent, @"data-runs=[\x27\x22](\d+)[\x27\x22]", RegexOptions.IgnoreCase);
                var volsMatch = Regex.Match(rContent, @"data-vols=[\x27\x22](\d+)[\x27\x22]", RegexOptions.IgnoreCase);
                var ageMatch = Regex.Match(rContent, @"data-agegroup=[\x27\x22]([^\x27\x22]*)[\x27\x22]", RegexOptions.IgnoreCase);
                var achMatch = Regex.Match(rContent, @"data-achievement=[\x27\x22]([^\x27\x22]*)[\x27\x22]", RegexOptions.IgnoreCase);

                int runs = runsMatch.Success && int.TryParse(runsMatch.Groups[1].Value, out int rVal) ? rVal : 0;
                int vols = volsMatch.Success && int.TryParse(volsMatch.Groups[1].Value, out int vVal) ? vVal : 0;
                string ageGroup = ageMatch.Success ? ageMatch.Groups[1].Value.Trim() : "";
                string achievement = achMatch.Success ? achMatch.Groups[1].Value.Trim() : "";

                bool isPb = achievement.Contains("PB", StringComparison.OrdinalIgnoreCase) ||
                            rContent.Contains("Results-table-td--pb", StringComparison.OrdinalIgnoreCase);

                bool isFirstTimer = achievement.Contains("First Timer", StringComparison.OrdinalIgnoreCase);

                runnerEventMap[pId] = (runs, vols, ageGroup, achievement, isPb, isFirstTimer);
            }
        }

        // 2. Enrich records & identify running milestones
        var milestones = new List<ParkrunMilestone>();

        foreach (var record in records)
        {
            if (string.IsNullOrEmpty(record.ParkrunnerId)) continue;

            if (runnerEventMap.TryGetValue(record.ParkrunnerId, out var stats))
            {
                record.TotalRuns = stats.Runs;
                record.TotalVols = stats.Vols;
                record.AgeGroup = stats.AgeGroup;
                record.Achievement = stats.Achievement;
                record.IsPb = stats.IsPb;
                record.IsFirstTimer = stats.IsFirstTimer;

                var mInfo = EvaluateRunMilestone(record.TotalRuns, record.AgeGroup);
                if (mInfo != null)
                {
                    record.Milestone = mInfo.Value.Title;
                    record.MilestoneBadgeBg = mInfo.Value.BgColor;
                    record.MilestoneBadgeText = mInfo.Value.TextColor;

                    milestones.Add(new ParkrunMilestone
                    {
                        ParkrunnerName = record.Parkrunner,
                        ParkrunnerId = record.ParkrunnerId,
                        EventName = record.EventName,
                        MilestoneType = "Run",
                        MilestoneCount = record.TotalRuns,
                        MilestoneTitle = mInfo.Value.Title,
                        MilestoneClub = mInfo.Value.Club,
                        BadgeBgColor = mInfo.Value.BgColor,
                        BadgeTextColor = mInfo.Value.TextColor,
                        FinishTime = record.Time,
                        Achievement = record.Achievement,
                        ProfileUrl = record.ProfileUrl,
                        EventDate = eventDate
                    });
                }
            }
        }

        // 3. Track volunteer milestones (if any volunteer reached 25, 50, 100, 200, 250, 500 this week)
        if (volunteerProfiles != null)
        {
            foreach (var vp in volunteerProfiles)
            {
                if (vp.DeltaCredits > 0 && IsVolunteerMilestone(vp.TotalCredits))
                {
                    var vInfo = EvaluateVolunteerMilestone(vp.TotalCredits);
                    if (vInfo != null)
                    {
                        // Check if not already added
                        if (!milestones.Any(m => m.ParkrunnerId == vp.ParkrunnerId && m.MilestoneType == "Volunteer"))
                        {
                            milestones.Add(new ParkrunMilestone
                            {
                                ParkrunnerName = vp.ParkrunnerName,
                                ParkrunnerId = vp.ParkrunnerId,
                                EventName = vp.EventName,
                                MilestoneType = "Volunteer",
                                MilestoneCount = vp.TotalCredits,
                                MilestoneTitle = vInfo.Value.Title,
                                MilestoneClub = vInfo.Value.Club,
                                BadgeBgColor = vInfo.Value.BgColor,
                                BadgeTextColor = vInfo.Value.TextColor,
                                FinishTime = "",
                                Achievement = vp.RoleThisWeek,
                                ProfileUrl = $"https://www.parkrun.org.uk/parkrunner/{vp.ParkrunnerId}/",
                                EventDate = eventDate
                            });
                        }
                    }
                }
            }
        }

        // Sort milestones: highest count descending, then by name
        milestones = milestones
            .OrderByDescending(m => m.MilestoneCount)
            .ThenBy(m => m.ParkrunnerName)
            .ToList();

        // 4. Save to persistent milestones store
        SaveToMilestonesStore(milestones);

        return milestones;
    }

    public static (string Title, string Club, string BgColor, string TextColor)? EvaluateRunMilestone(int runCount, string ageGroup)
    {
        return runCount switch
        {
            10 when ageGroup.StartsWith("J", StringComparison.OrdinalIgnoreCase) =>
                ("10th parkrun", "Junior 10 Club", "#424242", "#FFFFFF"),
            25 =>
                ("25th parkrun", "Club 25", "#7B1FA2", "#FFFFFF"), // Purple (Official 25 Club)
            50 =>
                ("50th parkrun", "Club 50", "#C62828", "#FFFFFF"), // Red (Official 50 Club)
            100 =>
                ("100th parkrun", "Club 100", "#212121", "#FFFFFF"), // Black (Official 100 Club)
            150 =>
                ("150th parkrun", "150 Club", "#283593", "#FFFFFF"), // Deep Indigo
            200 =>
                ("200th parkrun", "200 Club", "#1A237E", "#FFFFFF"), // Deep Indigo / Amber
            250 =>
                ("250th parkrun", "Club 250", "#2E7D32", "#FFFFFF"), // Green (Official 250 Club)
            300 =>
                ("300th parkrun", "300 Club", "#283593", "#FFFFFF"),
            350 =>
                ("350th parkrun", "350 Club", "#283593", "#FFFFFF"),
            400 =>
                ("400th parkrun", "400 Club", "#283593", "#FFFFFF"),
            450 =>
                ("450th parkrun", "450 Club", "#283593", "#FFFFFF"),
            500 =>
                ("500th parkrun", "Club 500", "#1565C0", "#FFFFFF"), // Royal Blue (Official 500 Club)
            1000 =>
                ("1000th parkrun", "Club 1000", "#F57F17", "#FFFFFF"), // Gold (Official 1000 Club)
            _ => null
        };
    }

    public static bool IsVolunteerMilestone(int credits)
    {
        return credits is 25 or 50 or 100 or 200 or 250 or 500 or 1000;
    }

    public static (string Title, string Club, string BgColor, string TextColor)? EvaluateVolunteerMilestone(int credits)
    {
        return credits switch
        {
            25 => ("25th Volunteer Credit", "V25 Club", "#7B1FA2", "#FFFFFF"),
            50 => ("50th Volunteer Credit", "V50 Club", "#C62828", "#FFFFFF"),
            100 => ("100th Volunteer Credit", "V100 Club", "#212121", "#FFFFFF"),
            200 => ("200th Volunteer Credit", "V200 Club", "#1A237E", "#FFFFFF"),
            250 => ("250th Volunteer Credit", "V250 Club", "#2E7D32", "#FFFFFF"),
            500 => ("500th Volunteer Credit", "V500 Club", "#1565C0", "#FFFFFF"),
            1000 => ("1000th Volunteer Credit", "V1000 Club", "#F57F17", "#FFFFFF"),
            _ => null
        };
    }

    private void SaveToMilestonesStore(List<ParkrunMilestone> newMilestones)
    {
        if (newMilestones == null || newMilestones.Count == 0) return;

        try
        {
            var existing = new List<ParkrunMilestone>();
            if (File.Exists(_milestonesStorePath))
            {
                string json = File.ReadAllText(_milestonesStorePath);
                existing = JsonSerializer.Deserialize<List<ParkrunMilestone>>(json) ?? new List<ParkrunMilestone>();
            }

            bool addedAny = false;
            foreach (var m in newMilestones)
            {
                // Check if already in store (same date, runner id, count, and type)
                if (!existing.Any(e => e.EventDate == m.EventDate && e.ParkrunnerId == m.ParkrunnerId && e.MilestoneCount == m.MilestoneCount && e.MilestoneType == m.MilestoneType))
                {
                    existing.Add(m);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                string? dir = Path.GetDirectoryName(_milestonesStorePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string outJson = JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_milestonesStorePath, outJson);

                // Synchronize with project directory if running from bin
                string projectDataDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "milestones.json");
                if (File.Exists(projectDataDir) && !string.Equals(Path.GetFullPath(projectDataDir), Path.GetFullPath(_milestonesStorePath), StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(projectDataDir, outJson);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to save milestones to store: {ex.Message}");
        }
    }
}
