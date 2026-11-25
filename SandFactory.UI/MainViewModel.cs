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
    private readonly Sea _sea = new();
    private readonly SandContainer _container = new();
    private readonly Transporter[] _trucks = new Transporter[5];

    // --- Свойства для HUD ---
    private int _conveyorPower = 50;
    public int ConveyorPower
    {
        get => _conveyorPower;
        set { if (value >= 0 && value <= 100) _conveyorPower = value; OnPropertyChanged(); }
    }

    private double _containerVolume;
    public double ContainerVolume
    {
        get => _containerVolume;
        set { _containerVolume = value; OnPropertyChanged(); OnPropertyChanged(nameof(ContainerFillPercentage)); }
    }

    private double _containerMax = 100.0;
    public double ContainerMax => _containerMax;
    public double ContainerFillPercentage => (ContainerVolume / ContainerMax) * 100;

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
        set { _isRunning = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    private string _statusText = "Ожидание...";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    // --- Магазин ---
    private bool _isShopOpen;
    public bool IsShopOpen
    {
        get => _isShopOpen;
        set { _isShopOpen = value; OnPropertyChanged(); }
    }

    // --- Данные для отрисовки ---
    private int _activeTrucks = 1;
    public int ActiveTrucks
    {
        get => _activeTrucks;
        set { _activeTrucks = value; OnPropertyChanged(); }
    }

    private double[] _truckRoadProgress = new double[5];
    public double[] TruckRoadProgress
    {
        get => _truckRoadProgress;
        set { _truckRoadProgress = value; OnPropertyChanged(); }
    }

    private double[] _truckLoads = new double[5];
    public double[] TruckLoads
    {
        get => _truckLoads;
        set { _truckLoads = value; OnPropertyChanged(); }
    }

    // --- ПРОКАЧКА ---
    private int _sellPriceLevel = 1;
    public int SellPriceLevel
    {
        get => _sellPriceLevel;
        set { _sellPriceLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(SellPriceUpgradeCost)); OnPropertyChanged(nameof(CurrentSellPrice)); }
    }
    public double CurrentSellPrice => 1 + ((_sellPriceLevel - 1) * 1.8);
    public long SellPriceUpgradeCost => 150 * _sellPriceLevel * _sellPriceLevel;

    public long TruckUpgradeCost => 500 * _activeTrucks;
    public bool CanBuyTruck => _activeTrucks < 5;

    public ObservableCollection<string> Logs { get; } = new();
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand BuyTruckCommand { get; }
    public ICommand UpgradeSellPriceCommand { get; }
    public ICommand ToggleShopCommand { get; }

    private readonly BackgroundWorker _worker;
    private readonly object _powerLock = new object();

    public MainViewModel()
    {
        for (int i = 0; i < _trucks.Length; i++)
        {
            _trucks[i] = new Transporter { Id = i, MaxLoad = 100.0, State = 0, IsVisible = (i == 0) };
        }

        _worker = new BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
        _worker.DoWork += Worker_DoWork;
        _worker.ProgressChanged += Worker_ProgressChanged;
        _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

        StartCommand = new RelayCommand(StartSimulation, () => !IsRunning);
        StopCommand = new RelayCommand(StopSimulation, () => IsRunning);
        BuyTruckCommand = new RelayCommand(BuyTruck, () => CanBuyTruck);
        UpgradeSellPriceCommand = new RelayCommand(UpgradeSellPrice);
        ToggleShopCommand = new RelayCommand(() => IsShopOpen = !IsShopOpen);
    }

    private void StartSimulation()
    {
        if (!_worker.IsBusy)
        {
            IsRunning = true;
            StatusText = "В РАБОТЕ";
            _worker.RunWorkerAsync();
        }
    }

    private void StopSimulation()
    {
        if (_worker.IsBusy) { _worker.CancelAsync(); StatusText = "ПАУЗА"; }
    }

    private void BuyTruck()
    {
        if (Money >= TruckUpgradeCost && CanBuyTruck)
        {
            Money -= TruckUpgradeCost;
            ActiveTrucks++;
            _trucks[ActiveTrucks - 1].IsVisible = true;
            Logs.Add($"[Магазин] Куплен новый грузовик! Всего: {ActiveTrucks}");
            OnPropertyChanged(nameof(TruckUpgradeCost));
            OnPropertyChanged(nameof(CanBuyTruck));
        }
    }

    private void UpgradeSellPrice()
    {
        if (Money >= SellPriceUpgradeCost)
        {
            Money -= SellPriceUpgradeCost;
            SellPriceLevel++;
            Logs.Add($"[Магазин] Цена продажи выросла до {CurrentSellPrice:F1} монет за единицу.");
        }
    }

    private void Worker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender!;
        long tickCounter = 0;
        _container.CurrentVolume = 0;

        for (int i = 0; i < ActiveTrucks; i++)
        {
            _trucks[i].State = 0;
            _trucks[i].RoadProgress = 0;
            _trucks[i].CurrentLoad = 0;
        }

        while (!worker.CancellationPending)
        {
            int safePower;
            lock (_powerLock) { safePower = _conveyorPower; }
            tickCounter++;

            // Замедленная генерация песка
            double generatedSand = 1.5 * (safePower / 100.0);
            _container.CurrentVolume = Math.Min(_container.CurrentVolume + generatedSand, _container.MaxCapacity);

            long earnedMoneyThisTick = 0;

            for (int i = 0; i < ActiveTrucks; i++)
            {
                var truck = _trucks[i];
                switch (truck.State)
                {
                    case 0: // Едет к контейнеру
                        truck.RoadProgress += 1.5;
                        if (truck.RoadProgress >= 100) { truck.RoadProgress = 100; truck.State = 1; }
                        break;

                    case 1: // Загружается (стоит)
                        double spaceInTruck = truck.MaxLoad - truck.CurrentLoad;
                        double amountToLoad = Math.Min(_container.CurrentVolume, spaceInTruck);
                        amountToLoad = Math.Min(amountToLoad, 1.0); // Скорость погрузки

                        _container.CurrentVolume -= amountToLoad;
                        truck.CurrentLoad += amountToLoad;

                        if (truck.CurrentLoad >= truck.MaxLoad) { truck.State = 2; }
                        break;

                    case 2: // Уезжает с грузом
                        truck.RoadProgress += 1.5;
                        if (truck.RoadProgress >= 120)
                        {
                            earnedMoneyThisTick += (long)(truck.CurrentLoad * CurrentSellPrice);
                            truck.CurrentLoad = 0;
                            truck.IsVisible = false;
                            truck.State = 3;
                        }
                        break;

                    case 3: // Возвращается пустой
                        if (truck.RoadProgress > 0) truck.RoadProgress = -20; // Респавн слева
                        truck.RoadProgress += 1.5;

                        if (truck.RoadProgress >= 0) truck.IsVisible = true;
                        if (truck.RoadProgress >= 100) { truck.RoadProgress = 100; truck.State = 0; }
                        break;
                }
            }

            var snapshot = new SimulationSnapshot
            {
                Ticks = tickCounter,
                CurrentPower = safePower,
                ContainerVolume = _container.CurrentVolume,
                ContainerMax = _container.MaxCapacity,
                MoneyEarnedThisTick = earnedMoneyThisTick,
                ActiveTrucks = ActiveTrucks,
                TruckRoadProgress = new double[5],
                TruckLoads = new double[5]
            };

            for (int i = 0; i < 5; i++)
            {
                snapshot.TruckRoadProgress[i] = _trucks[i].RoadProgress;
                snapshot.TruckLoads[i] = (_trucks[i].CurrentLoad / _trucks[i].MaxLoad) * 100;
            }

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
            Money += snapshot.MoneyEarnedThisTick;
            ActiveTrucks = snapshot.ActiveTrucks;
            TruckRoadProgress = snapshot.TruckRoadProgress;
            TruckLoads = snapshot.TruckLoads;
        }
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        IsRunning = false;
        if (e.Cancelled) StatusText = "ОСТАНОВЛЕНО";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}