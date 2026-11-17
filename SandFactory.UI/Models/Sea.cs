using System.Collections.Generic;
using System.Windows.Media;

namespace SandFactory.UI.Models;

public class Sea
{
    public static IReadOnlyList<SandType> AvailableSandTypes { get; } = new List<SandType>
    {
        new()
        {
            Name = "Морской песок",
            GenerationRate = 10.0,
            PrimaryBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)),   // #D97706
            SecondaryBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36)) // #FBBF24
        },
        new()
        {
            Name = "Кварцевый песок",
            GenerationRate = 8.0,
            PrimaryBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), // #CBD5E1
            SecondaryBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252)) // #F8FAFC
        },
        new()
        {
            Name = "Речной крупный песок",
            GenerationRate = 12.0,
            PrimaryBrush = new SolidColorBrush(Color.FromRgb(194, 65, 12)),   // #C2410C
            SecondaryBrush = new SolidColorBrush(Color.FromRgb(251, 146, 60)) // #FB923C
        }
    };

    public SandType CurrentSand { get; set; } = AvailableSandTypes[0];

    public double BaseGenerationRate => CurrentSand.GenerationRate;
}