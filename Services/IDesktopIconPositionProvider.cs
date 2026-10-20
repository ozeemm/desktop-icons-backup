using DesktopIconsBackup.Models;

namespace DesktopIconsBackup.Services;

/// <summary>
/// Определяет операции чтения и восстановления расположения значков рабочего стола.
/// </summary>
internal interface IDesktopIconPositionProvider
{
    /// <summary>
    /// Считывает текущие координаты значков, используя их имена как ключи.
    /// </summary>
    Dictionary<string, IconPosition> CapturePositions();

    /// <summary>
    /// Применяет координаты сохраненных значков и возвращает сведения о несовпадениях.
    /// </summary>
    LayoutRestoreResult RestorePositions(IReadOnlyDictionary<string, IconPosition> positions);
}
