namespace SandFactory.UI.Models;

public class Transporter
{
    public int Id { get; set; }

    // Скорость: сколько песка транспортер забирает из контейнера за 1 такт.
    // 1.0 = 1 единица песка за такт.
    public double Speed { get; set; } = 1.0;

    public int Level { get; set; } = 1;

    // Для UI: текущая нагрузка (от 0.0 до 1.0) для отрисовки прогресс-бара
    public double CurrentLoad { get; set; } = 0;
}