namespace SandFactory.UI;

public class SimulationSnapshot
{
    public long Ticks { get; set; }
    public int CurrentPower { get; set; }
    public double ContainerVolume { get; set; }
    public double ContainerMax { get; set; }
    public long MoneyEarnedThisTick { get; set; }
    public double[] TransporterLoads { get; set; } = new double[5];

    // Флаг для лога переполнения
    public bool IsOverflowing { get; set; }
}