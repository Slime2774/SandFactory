using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using SandFactory.UI.Models;

namespace SandFactory.UI;

public class MainViewModel : INotifyPropertyChanged
{
    // --- Модели ---
    private readonly Sea _sea = new();
    private readonly Conveyor _conveyor = new();
    private readonly SandContainer _container = new();
    private readonly Transporter[] _transporters = new Transporter[5];

    // --- Свойства для UI ---
    private int _conveyorPower = 50;
    public int ConveyorPower
    {
        get => _conveyorPower;
        set
        {
            if (value < 0) value = 0;
            if (value > MaxPower) value = MaxPower;
            _conveyorPower = value;
            OnPropertyChanged();
        }
    }

    public int MaxPower
    {
        get => _conveyor.MaxPower;
        set
        {
            _conveyor.MaxPower = value;
            OnPropertyChanged();
        }
    }

    private double _containerVolume;
    public double ContainerVolume
    {
        get => _containerVolume;
        set { _containerVolume = value; OnPropertyChanged(); }
    }

    // НОВОЕ: Процент заполнения контейнера (0-100) для кастомного UI
    private double _containerFillPercentage;
    public double ContainerFillPercentage
    {
        get => _containerFillPercentage;
        set { _containerFillPercentage = value; OnPropertyChanged(); }
    }

    private double _containerMax = 100.0;
    public double ContainerMax
    {
        get => _containerMax;
        set { _containerMax = value; OnPropertyChanged(); }
    }

    private double[] _transporterLoads = new double[5];
    public double[] TransporterLoads
    {
        get => _transporterLoads;
        set { _transporterLoads = value; OnPropertyChanged(); }
    }

    private long _money;
    public long Money
    {
        get => _money;
        set { _money = value; OnPropertyChanged(); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _statusText = "Ожидание запуска...";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    // --- ПРОКАЧКА И ЭКОНОМИКА ---
    private int _conveyorLevel = 1;
    public int ConveyorLevel
    {
        get => _conveyorLevel;
        set { _conveyorLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConveyorUpgradeCost)); }
    }
    public long ConveyorUpgradeCost => 100 * _conveyorLevel * _conveyorLevel;

    private int _transporterLevel = 1;
    public int TransporterLevel
    {
        get => _transporterLevel;
        set { _transporterLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransporterUpgradeCost)); }
    }
    public long TransporterUpgradeCost => 50 * _transporterLevel * _transporterLevel;

    public ObservableCollection<string> Logs { get; } = new();

    // --- Команды ---
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand UpgradeConveyorCommand { get; }
    public ICommand UpgradeTransportersCommand { get; }

    private readonly BackgroundWorker _worker;
    private readonly object _powerLock = new object();

    public MainViewModel()
    {
        for (int i = 0; i < _transporters.Length; i++)
            _transporters[i] = new Transporter { Id = i + 1, Speed = 1.0 };

        ContainerMax = _container.MaxCapacity;

        _worker = new BackgroundWorker
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };

        _worker.DoWork += Worker_DoWork;
        _worker.ProgressChanged += Worker_ProgressChanged;
        _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

        StartCommand = new RelayCommand(StartSimulation, () => !IsRunning);
        StopCommand = new RelayCommand(StopSimulation, () => IsRunning);
        UpgradeConveyorCommand = new RelayCommand(UpgradeConveyor);
        UpgradeTransportersCommand = new RelayCommand(UpgradeTransporters);
    }

    private void StartSimulation()
    {
        if (!_worker.IsBusy)
        {
            IsRunning = true;
            StatusText = "Симуляция запущена...";
            _worker.RunWorkerAsync();
        }
    }

    private void StopSimulation()
    {
        if (_worker.IsBusy)
        {
            _worker.CancelAsync();
            StatusText = "Остановка...";
        }
    }

    private void UpgradeConveyor()
    {
        if (Money >= ConveyorUpgradeCost)
        {
            Money -= ConveyorUpgradeCost;
            ConveyorLevel++;
            MaxPower += 50;
            Logs.Add($"[Магазин] Конвейер улучшен до ур. {ConveyorLevel}. Лимит мощности: {MaxPower}");
        }
    }

    private void UpgradeTransporters()
    {
        if (Money >= TransporterUpgradeCost)
        {
            Money -= TransporterUpgradeCost;
            TransporterLevel++;
            foreach (var t in _transporters)
            {
                t.Speed += 0.5;
            }
            Logs.Add($"[Магазин] Транспортеры улучшены до ур. {TransporterLevel}. Скорость: {_transporters[0].Speed:F1}");
        }
    }

    private void Worker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender!;
        long tickCounter = 0;
        _container.CurrentVolume = 0;

        while (!worker.CancellationPending)
        {
            int safePower;
            lock (_powerLock) { safePower = _conveyorPower; }

            tickCounter++;

            double generatedSand = _sea.BaseGenerationRate * (safePower / 100.0);

            _container.CurrentVolume += generatedSand;
            bool isOverflowing = false;
            if (_container.CurrentVolume > _container.MaxCapacity)
            {
                _container.CurrentVolume = _container.MaxCapacity;
                isOverflowing = true;
            }

            double totalTakenByTransporters = 0;
            for (int i = 0; i < _transporters.Length; i++)
            {
                var t = _transporters[i];
                double amountToTake = t.Speed;
                if (amountToTake > _container.CurrentVolume) amountToTake = _container.CurrentVolume;

                _container.CurrentVolume -= amountToTake;
                totalTakenByTransporters += amountToTake;
                t.CurrentLoad = t.Speed > 0 ? (amountToTake / t.Speed) : 0;
            }

            long earnedMoney = (long)(totalTakenByTransporters * 10);

            var snapshot = new SimulationSnapshot
            {
                Ticks = tickCounter,
                CurrentPower = safePower,
                ContainerVolume = _container.CurrentVolume,
                ContainerMax = _container.MaxCapacity,
                MoneyEarnedThisTick = earnedMoney,
                IsOverflowing = isOverflowing,
                TransporterLoads = new double[5]
            };

            for (int i = 0; i < _transporters.Length; i++)
                snapshot.TransporterLoads[i] = _transporters[i].CurrentLoad * 100;

            worker.ReportProgress(0, snapshot);
            Thread.Sleep(100);
        }
        e.Cancel = true;
    }

    private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (e.UserState is SimulationSnapshot snapshot)
        {
            ContainerVolume = snapshot.ContainerVolume;
            ContainerMax = snapshot.ContainerMax;

            // ХИТРОСТЬ: Если контейнер переполнен, рисуем 100%. 
            // Иначе считаем реальный процент (но не больше 100).
            if (snapshot.IsOverflowing)
            {
                ContainerFillPercentage = 100.0;
            }
            else
            {
                ContainerFillPercentage = snapshot.ContainerMax > 0
                    ? (snapshot.ContainerVolume / snapshot.ContainerMax) * 100
                    : 0;
            }

            Money += snapshot.MoneyEarnedThisTick;
            TransporterLoads = snapshot.TransporterLoads;

            StatusText = $"Такт: {snapshot.Ticks} | Мощность: {snapshot.CurrentPower}%";

            if (snapshot.IsOverflowing && snapshot.Ticks % 20 == 0)
            {
                Logs.Add($"[Внимание] Контейнер переполнен! Песок просыпается.");
            }
        }
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        IsRunning = false;
        if (e.Cancelled) StatusText = "Остановлено.";
        else if (e.Error != null) StatusText = $"Ошибка: {e.Error.Message}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}