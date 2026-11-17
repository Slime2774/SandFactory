using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using SandFactory.UI.Models;

namespace SandFactory.UI;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly Sea _sea = new();
    private readonly Conveyor _conveyor = new();
    private readonly SandContainer _container = new();
    private readonly BackgroundWorker _conveyorWorker;
    private readonly object _powerLock = new();

    public ObservableCollection<Transporter> Transporters { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();

    public IReadOnlyList<SandType> SandTypes => Sea.AvailableSandTypes;

    public SandType SelectedSandType
    {
        get => _sea.CurrentSand;
        set
        {
            if (value == null || _sea.CurrentSand == value) return;
            _sea.CurrentSand = value;
            OnPropertyChanged();
            AddLog($"Смена сырья на карьере: теперь добывается «{value.Name}» ({value.GenerationRate:F0} т/такт).");
        }
    }

    private bool _isAutoBalanceEnabled;
    public bool IsAutoBalanceEnabled
    {
        get => _isAutoBalanceEnabled;
        set
        {
            if (_isAutoBalanceEnabled == value) return;
            _isAutoBalanceEnabled = value;
            OnPropertyChanged();
            AddLog(value
                ? "[Автоматика] Включен авто-баланс бункера (удержание уровня ~60%)."
                : "[Автоматика] Авто-баланс отключен (ручное управление).");
        }
    }

    private int _conveyorPower = 50;
    public int ConveyorPower
    {
        get { lock (_powerLock) return _conveyorPower; }
        set
        {
            int clamped = Math.Clamp(value, 0, _conveyor.MaxPower);
            lock (_powerLock) { _conveyorPower = clamped; }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMainConveyorMoving));
        }
    }

    public int MaxPower => _conveyor.MaxPower;
    public bool IsMainConveyorMoving => IsRunning && ConveyorPower > 0;

    private double _containerVolume;
    public double ContainerVolume
    {
        get => _containerVolume;
        set { _containerVolume = value; OnPropertyChanged(); }
    }

    private double _containerFillPercentage;
    public double ContainerFillPercentage
    {
        get => _containerFillPercentage;
        set { _containerFillPercentage = value; OnPropertyChanged(); }
    }

    public double ContainerMax { get; private set; }

    private int _totalTrips;
    public int TotalTrips
    {
        get => _totalTrips;
        set { _totalTrips = value; OnPropertyChanged(); }
    }

    private double _totalShippedTons;
    public double TotalShippedTons
    {
        get => _totalShippedTons;
        set { _totalShippedTons = value; OnPropertyChanged(); }
    }

    // Реальная пропускная способность всех транспортеров в тоннах в секунду (такт транспортера = 250мс -> 4 такта/сек)
    private double _totalOutflowPerSec;
    public double TotalOutflowPerSec
    {
        get => _totalOutflowPerSec;
        set { _totalOutflowPerSec = value; OnPropertyChanged(); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMainConveyorMoving));
            UpdateFactoryStats();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _statusText = "Система готова к запуску";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand AddTransporterCommand { get; }
    public ICommand RemoveTransporterCommand { get; }

    public MainViewModel()
    {
        ContainerMax = _container.MaxCapacity;
        Transporters.CollectionChanged += Transporters_CollectionChanged;

        for (int i = 1; i <= 5; i++)
        {
            Transporters.Add(new Transporter(i, _container));
        }

        _conveyorWorker = new BackgroundWorker
        {
            WorkerReportsProgress = true,
            WorkerSupportsCancellation = true
        };
        _conveyorWorker.DoWork += ConveyorWorker_DoWork;
        _conveyorWorker.ProgressChanged += ConveyorWorker_ProgressChanged;
        _conveyorWorker.RunWorkerCompleted += (_, _) =>
        {
            IsRunning = false;
            UpdateFactoryStats();
        };

        StartCommand = new RelayCommand(StartAll, () => !IsRunning);
        StopCommand = new RelayCommand(StopAll, () => IsRunning);
        AddTransporterCommand = new RelayCommand(AddTransporter);
        RemoveTransporterCommand = new RelayCommand(RemoveTransporter, () => Transporters.Count > 1);
    }

    private void Transporters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Transporter t in e.NewItems)
                t.PropertyChanged += Transporter_PropertyChanged;
        }
        if (e.OldItems != null)
        {
            foreach (Transporter t in e.OldItems)
                t.PropertyChanged -= Transporter_PropertyChanged;
        }
        UpdateFactoryStats();
    }

    private void Transporter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Transporter.TripsCompleted)
            or nameof(Transporter.TotalSandShipped)
            or nameof(Transporter.Power)
            or nameof(Transporter.IsTruckLeaving))
        {
            UpdateFactoryStats();
        }
    }

    private void UpdateFactoryStats()
    {
        TotalTrips = Transporters.Sum(t => t.TripsCompleted);
        TotalShippedTons = Transporters.Sum(t => t.TotalSandShipped);

        if (!IsRunning)
        {
            TotalOutflowPerSec = 0;
            return;
        }

        // Считаем сколько тонн в секунду забирают активные транспортеры (4 тика в секунду)
        TotalOutflowPerSec = Transporters
            .Where(t => !t.IsTruckLeaving)
            .Sum(t => t.MaxSpeed * (t.Power / 100.0) * 4.0);
    }

    private void StartAll()
    {
        if (_conveyorWorker.IsBusy) return;

        IsRunning = true;
        StatusText = "Конвейер и транспортеры запущены";
        AddLog($"Запуск линии: добыча сырья «{SelectedSandType.Name}».");

        _conveyorWorker.RunWorkerAsync();
        foreach (var transporter in Transporters)
            transporter.Start();
    }

    public void StopAll()
    {
        if (!_conveyorWorker.IsBusy) return;

        StatusText = "Остановка...";
        _conveyorWorker.CancelAsync();

        foreach (var transporter in Transporters)
            transporter.Stop();

        AddLog("Работа всех потоков остановлена.");
    }

    private void AddTransporter()
    {
        int nextId = Transporters.Count + 1;
        var newTransporter = new Transporter(nextId, _container);
        Transporters.Add(newTransporter);

        if (IsRunning)
            newTransporter.Start();

        AddLog($"Добавлен Транспортер #{nextId} и грузовик #{nextId} ({newTransporter.TruckCapacity:F0} т).");
        CommandManager.InvalidateRequerySuggested();
    }

    private void RemoveTransporter()
    {
        if (Transporters.Count <= 1) return;

        var last = Transporters[^1];
        last.Dispose();
        Transporters.RemoveAt(Transporters.Count - 1);

        AddLog($"Удален Транспортер #{last.Id}.");
        CommandManager.InvalidateRequerySuggested();
    }

    private int CalculateAutoBalancedPower(double currentVolume)
    {
        double fillPercent = ContainerMax > 0 ? (currentVolume / ContainerMax) * 100.0 : 0;

        if (fillPercent >= 90.0) return 0;
        if (fillPercent <= 30.0) return 100;

        double error = 60.0 - fillPercent;
        int targetPower = (int)Math.Round(55.0 + error * 1.5);
        return Math.Clamp(targetPower, 5, 100);
    }

    private void ConveyorWorker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender!;
        int overflowLogCooldown = 0;

        while (!worker.CancellationPending)
        {
            double currentVolBefore = _container.CurrentVolume;
            int? autoAdjustedPower = null;

            if (IsAutoBalanceEnabled)
            {
                int calculated = CalculateAutoBalancedPower(currentVolBefore);
                if (calculated != ConveyorPower)
                {
                    lock (_powerLock) { _conveyorPower = calculated; }
                    autoAdjustedPower = calculated;
                }
            }

            int power = ConveyorPower;
            double generatedSand = _sea.BaseGenerationRate * (power / 100.0);

            bool addedWithoutOverflow = _container.TryAddSand(generatedSand);
            double currentVol = _container.CurrentVolume;

            bool shouldLogOverflow = false;
            if (!addedWithoutOverflow)
            {
                if (overflowLogCooldown <= 0)
                {
                    shouldLogOverflow = true;
                    overflowLogCooldown = 15;
                }
                else
                {
                    overflowLogCooldown--;
                }
            }
            else
            {
                overflowLogCooldown = 0;
            }

            worker.ReportProgress(0, (currentVol, power, shouldLogOverflow, autoAdjustedPower));
            Thread.Sleep(200);
        }

        e.Cancel = true;
    }

    private void ConveyorWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (e.UserState is ValueTuple<double, int, bool, int?> state)
        {
            if (state.Item4.HasValue)
            {
                OnPropertyChanged(nameof(ConveyorPower));
                OnPropertyChanged(nameof(IsMainConveyorMoving));
            }

            ContainerVolume = state.Item1;
            ContainerFillPercentage = ContainerMax > 0 ? (state.Item1 / ContainerMax) * 100.0 : 0;

            string modeTag = IsAutoBalanceEnabled ? " [АВТО]" : "";
            StatusText = $"Конвейер{modeTag}: {state.Item2}% | Бункер: {state.Item1:F1} / {ContainerMax:F0}";

            if (state.Item3)
            {
                AddLog("[Внимание] Бункер заполнен на 100%! Включите «Авто-баланс» или добавьте транспортер.");
            }
        }
    }

    private void AddLog(string message)
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        if (Logs.Count > 50) Logs.RemoveAt(Logs.Count - 1);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}