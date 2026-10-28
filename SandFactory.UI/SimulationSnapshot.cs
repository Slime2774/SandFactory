using System;
using System.Collections.Generic;
using System.Text;

namespace SandFactory.UI
{
    // DTO: передаем из DoWork в ProgressChanged
    public class SimulationSnapshot
    {
        public long Ticks { get; set; }       // Сколько тактов прошло
        public int CurrentPower { get; set; } // Текущая мощность (читается из UI)
        public double ContainerVolume { get; set; } // Заглушка: объем песка (пока 0)
        public long Money { get; set; }       // Заглушка: деньги (пока 0)
    }
}
