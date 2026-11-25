namespace SandFactory.UI;

public class SimulationSnapshot
{
    public long Ticks { get; set; }
    public int CurrentPower { get; set; }
    public double ContainerVolume { get; set; }
    public double ContainerMax { get; set; }
    public long MoneyEarnedThisTick { get; set; }
    public int ActiveTrucks { get; set; }

    // Данные для отрисовки
    public double[] TruckRoadProgress { get; set; } = new double[5];
    public double[] TruckLoads { get; set; } = new double[5];
    public bool[] TruckVisibility { get; set; } = new bool[5];
    public bool[] TruckFacingRight { get; set; } = new bool[5]; // Кабина смотрит вправо?
}