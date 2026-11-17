namespace SandFactory.UI.Models;

public class SandContainer
{
    private readonly object _syncLock = new();
    private double _currentVolume;

    public double MaxCapacity { get; } = 100.0;

    public double CurrentVolume
    {
        get { lock (_syncLock) return _currentVolume; }
    }

    public bool TryAddSand(double amount)
    {
        lock (_syncLock)
        {
            _currentVolume += amount;
            if (_currentVolume > MaxCapacity)
            {
                _currentVolume = MaxCapacity;
                return false;
            }
            return true;
        }
    }

    public double TakeSand(double requestedAmount)
    {
        lock (_syncLock)
        {
            double taken = requestedAmount > _currentVolume ? _currentVolume : requestedAmount;
            _currentVolume -= taken;
            return taken;
        }
    }
}