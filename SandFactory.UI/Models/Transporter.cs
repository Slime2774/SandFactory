namespace SandFactory.UI.Models;

public class Transporter
{
    public int Id { get; set; }
    public int State { get; set; } = 0;
    public double CurrentLoad { get; set; } = 0;
    public double MaxLoad { get; set; } = 100.0;
    public double RoadProgress { get; set; } = 0;
    public bool IsVisible { get; set; } = true; // НОВОЕ
}