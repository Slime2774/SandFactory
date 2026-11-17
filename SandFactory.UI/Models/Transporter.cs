using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SandFactory.UI.Models;

public class Transporter : INotifyPropertyChanged, IDisposable
{
    private readonly SandContainer _container;
    private readonly BackgroundWorker _worker;
    private readonly object _powerLock = new();

    public int Id { get; }
    public double MaxSpeed { get; set; } = 3.0; // Скорость при 100% мощности

    private int _power = 50;
    public int Power
    {
        get { lock (_powerLock) return _power; }
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            lock (_powerLock) { _power = clamped; }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCarryingSand));
        }
    }

    private double _currentLoad;
    public double CurrentLoad
    {
        get => _currentLoad;
        set
        {
            _currentLoad = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCarryingSand));
        }
    }

    public bool IsCarryingSand => _currentLoad > 0.05 && Power > 0 && !IsTruckLeaving;

    private double _truckCapacity;
    public double TruckCapacity
    {
        get => _truckCapacity;
        set
        {
            _truckCapacity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TruckFillHeight));
        }
    }

    private double _currentTruckSand;
    public double CurrentTruckSand
    {
        get => _currentTruckSand;
        set
        {
            _currentTruckSand = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TruckFillHeight));
        }
    }

    // Высота песка в кузове (от 0 до 20 пикселей)
    public double TruckFillHeight => TruckCapacity > 0
        ? Math.Clamp((_currentTruckSand / TruckCapacity) * 20.0, 0, 20.0)
        : 0;

    private bool _isTruckLeaving;
    public bool IsTruckLeaving
    {
        get => _isTruckLeaving;
        set
        {
            _isTruckLeaving = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCarryingSand));
        }
    }

    private int _tripsCompleted;
    public int TripsCompleted
    {
        get => _tripsCompleted;
        set { _tripsCompleted = value; OnPropertyChanged(); }
    }

    private double _totalSandShipped;
    public double TotalSandShipped
    {
        get => _totalSandShipped;
        set { _totalSandShipped = value; OnPropertyChanged(); }
    }

    private string _status = "Ожидание";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public Transporter(int id, SandContainer container)
    {
        Id = id;
        _container = container;
        _truckCapacity = GenerateRandomCapacity();

        _worker = new BackgroundWorker
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };

        _worker.DoWork += Worker_DoWork;
        _worker.ProgressChanged += Worker_ProgressChanged;
        _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
    }

    private static double GenerateRandomCapacity()
    {
        // Рандомная грузоподъемность от 10 до 30 тонн (кратно 5 или целым числам)
        return Random.Shared.Next(10, 31);
    }

    public void Start()
    {
        if (!_worker.IsBusy)
        {
            Status = "Работает";
            _worker.RunWorkerAsync();
        }
    }

    public void Stop()
    {
        if (_worker.IsBusy)
        {
            _worker.CancelAsync();
        }
    }

    private void Worker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender!;
        double localTruckSand = 0;
        double localCapacity = TruckCapacity;

        while (!worker.CancellationPending)
        {
            // Если грузовик заполнился — отправляем в рейс и спавним новый с рандомной вместимостью
            if (localTruckSand >= localCapacity)
            {
                worker.ReportProgress(1, localTruckSand);
                Thread.Sleep(1050);

                localTruckSand = 0;
                localCapacity = GenerateRandomCapacity();
                worker.ReportProgress(2, localCapacity);
                continue;
            }

            int currentPower = Power;
            double effectiveSpeed = MaxSpeed * (currentPower / 100.0);

            if (effectiveSpeed <= 0.001)
            {
                worker.ReportProgress(0, (0.0, 0.0, localTruckSand, false));
                Thread.Sleep(250);
                continue;
            }

            double spaceLeft = localCapacity - localTruckSand;
            double request = Math.Min(effectiveSpeed, spaceLeft);
            double taken = _container.TakeSand(request);

            localTruckSand += taken;
            double loadRatio = effectiveSpeed > 0 ? taken / effectiveSpeed : 0;

            worker.ReportProgress(0, (loadRatio, taken, localTruckSand, true));
            Thread.Sleep(250);
        }

        e.Cancel = true;
    }

    private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (e.ProgressPercentage == 1)
        {
            CurrentLoad = 0;
            IsTruckLeaving = true;
            TripsCompleted++;
            Status = "Уезжает в рейс ➔";
        }
        else if (e.ProgressPercentage == 2 && e.UserState is double newCapacity)
        {
            CurrentTruckSand = 0;
            TruckCapacity = newCapacity;
            IsTruckLeaving = false;
            Status = $"Новый грузовик ({newCapacity:F0} т)";
        }
        else if (e.UserState is ValueTuple<double, double, double, bool> state)
        {
            CurrentLoad = state.Item1;
            TotalSandShipped += state.Item2;
            CurrentTruckSand = state.Item3;

            if (!state.Item4)
                Status = "Мощность 0% (Стоп)";
            else
                Status = state.Item2 > 0 ? "Грузит грузовик" : "Ждет песок из бака";
        }
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        CurrentLoad = 0;
        IsTruckLeaving = false;
        Status = "Остановлен";
    }

    public void Dispose()
    {
        Stop();
        _worker.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}