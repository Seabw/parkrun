using System;
using System.Collections.Generic;
using System.Linq;
using ParkrunScraper.Models;
using ScottPlot;

namespace ParkrunScraper.Services;

public class ParkrunChartGenerator
{
    public static byte[]? GenerateWeeklyTrendChart(List<WeeklyClubSnapshot> history, int width = 520, int height = 170)
    {
        if (history == null || history.Count < 2)
        {
            return null; // Not enough data points to render a meaningful trend chart
        }

        try
        {
            var plot = new Plot();

            // Background & Layout
            plot.FigureBackground.Color = Color.FromHex("#FFFFFF");
            plot.DataBackground.Color = Color.FromHex("#F8F9FA");

            double[] xs = Enumerable.Range(0, history.Count).Select(i => (double)i).ToArray();
            double[] runnerCounts = history.Select(h => (double)h.TotalRunners).ToArray();
            double[] eventCounts = history.Select(h => (double)h.DistinctEvents).ToArray();

            // Line 1: Runners
            var runnerLine = plot.Add.Scatter(xs, runnerCounts);
            runnerLine.Color = Color.FromHex("#283593"); // Deep Indigo
            runnerLine.LineWidth = 2.4f;
            runnerLine.MarkerSize = 6.5f;
            runnerLine.LegendText = "Runners";

            // Line 2: Events
            var eventLine = plot.Add.Scatter(xs, eventCounts);
            eventLine.Color = Color.FromHex("#00796B"); // Deep Teal
            eventLine.LineWidth = 2.4f;
            eventLine.MarkerSize = 6.5f;
            eventLine.LegendText = "Events";

            // Calculate range and generous headroom so numbers never touch or clip the top border
            double maxVal = Math.Max(runnerCounts.Max(), eventCounts.Max());
            double minVal = Math.Min(runnerCounts.Min(), eventCounts.Min());
            double ySpan = Math.Max(12, maxVal - minVal);

            // Generous headroom rounded up to clean multiple
            double rawMaxY = maxVal + Math.Max(12, ySpan * 0.35);
            double maxY = Math.Ceiling(rawMaxY / 5.0) * 5.0;
            if (maxY - maxVal < 10) maxY += 5;

            double rawMinY = Math.Max(0, minVal - Math.Max(4, ySpan * 0.15));
            double minY = Math.Floor(rawMinY / 5.0) * 5.0;

            // Add value markers on data points
            for (int i = 0; i < history.Count; i++)
            {
                double rVal = runnerCounts[i];
                double eVal = eventCounts[i];
                double labelOffset = Math.Max(0.8, ySpan * 0.035);

                var rText = plot.Add.Text($"{history[i].TotalRunners}", xs[i], rVal + labelOffset);
                rText.LabelFontColor = Color.FromHex("#283593");
                rText.LabelBold = true;
                rText.LabelFontSize = 8.5f;
                rText.LabelAlignment = Alignment.LowerCenter;

                // If runner and event counts are very close, position event label below to prevent collision
                bool tooClose = Math.Abs(rVal - eVal) < 3.0;
                double eY = tooClose ? (eVal - labelOffset) : (eVal + labelOffset);
                var eAlign = tooClose ? Alignment.UpperCenter : Alignment.LowerCenter;

                var eText = plot.Add.Text($"{history[i].DistinctEvents}", xs[i], eY);
                eText.LabelFontColor = Color.FromHex("#00796B");
                eText.LabelBold = true;
                eText.LabelFontSize = 8.5f;
                eText.LabelAlignment = eAlign;
            }

            // Format X Axis ticks
            Tick[] ticks = new Tick[history.Count];
            for (int i = 0; i < history.Count; i++)
            {
                string label = history[i].EventDate;
                if (DateTime.TryParse(history[i].EventDate, out var dt))
                {
                    label = dt.ToString("dd MMM");
                }
                ticks[i] = new Tick(i, label);
            }
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
            plot.Axes.Bottom.TickLabelStyle.FontSize = 7.5f;
            plot.Axes.Bottom.TickLabelStyle.ForeColor = Color.FromHex("#424242");

            plot.Axes.Left.TickLabelStyle.FontSize = 7.5f;
            plot.Axes.Left.TickLabelStyle.ForeColor = Color.FromHex("#757575");

            // Expand Y limits with generous top headroom
            plot.Axes.SetLimitsY(minY, maxY);
            plot.Axes.SetLimitsX(-0.5, history.Count - 0.5);

            // Hide in-plot legend so it does not obstruct any lines/numbers
            plot.HideLegend();

            return plot.GetImageBytes(width, height, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to generate trend chart: {ex.Message}");
            return null;
        }
    }
}
