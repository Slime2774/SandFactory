using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;

namespace SandFactory.UI;

public class MainViewModel : INotifyPropertyChanged
{
    // --- Свойства для UI (Binding) ---
    private int _conveyorPower = 50;
    public int ConveyorPower
    {
        get => _conveyorPower;
        set
        {
            if (value < 0) value = 0;
            if (value > 100) value = 100;

            _conveyorPower = value;
            OnPropertyChanged();
        }
    }

    private double _containerVolume;
    public double ContainerVolume
    {
        get => _containerVolume;
        set { _containerVolume = value; OnPropertyChanged(); }
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

    // --- Команды ---
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    // --- BackgroundWorker и синхронизация ---
    private readonly BackgroundWorker _worker;
    private readonly object _powerLock = new object();

    public MainViewModel()
    {
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

    // --- ФОНОВЫЙ ПОТОК ---
    private void Worker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var worker = (BackgroundWorker)sender!;
        long tickCounter = 0;

        while (!worker.CancellationPending)
        {
            int safePower;
            lock (_powerLock)
            {
                safePower = _conveyorPower;
            }

            tickCounter++;

            var snapshot = new SimulationSnapshot
            {
                Ticks = tickCounter,
                CurrentPower = safePower,
                ContainerVolume = (tickCounter % 100) * (safePower / 100.0),
                Money = tickCounter * 10
            };

            worker.ReportProgress(0, snapshot);
            Thread.Sleep(100);
        }

        e.Cancel = true;
    }

    // --- UI-ПОТОК ---
    private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        if (e.UserState is SimulationSnapshot snapshot)
        {
            ContainerVolume = snapshot.ContainerVolume;
            Money = snapshot.Money;
            StatusText = $"Такт: {snapshot.Ticks} | Мощность в фоне: {snapshot.CurrentPower}%";
        }
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        IsRunning = false;
        if (e.Cancelled)
            StatusText = "Остановлено пользователем.";
        else if (e.Error != null)
            StatusText = $"Ошибка: {e.Error.Message}";
        else
            StatusText = "Завершено.";
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}