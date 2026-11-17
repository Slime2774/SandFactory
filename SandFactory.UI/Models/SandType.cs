using System.Windows.Media;

namespace SandFactory.UI.Models;

public class SandType
{
    public string Name { get; init; } = string.Empty;
    public double GenerationRate { get; init; }
    public Brush PrimaryBrush { get; init; } = Brushes.Orange;
    public Brush SecondaryBrush { get; init; } = Brushes.Gold;

    public override string ToString() => Name;
}