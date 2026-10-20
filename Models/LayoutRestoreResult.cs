namespace DesktopIconsBackup.Models;

/// <summary>
/// Содержит результат восстановления профиля и списки несовпадающих значков.
/// </summary>
/// <param name="RestoredCount">Количество значков, для которых применены сохраненные координаты.</param>
/// <param name="MissingIcons">Сохраненные значки, которые не найдены на текущем рабочем столе.</param>
/// <param name="UnSavedIcons">Текущие значки, для которых нет сохраненных координат.</param>
internal sealed record LayoutRestoreResult(
    int RestoredCount,
    IReadOnlyList<string> MissingIcons,
    IReadOnlyList<string> UnSavedIcons
);
