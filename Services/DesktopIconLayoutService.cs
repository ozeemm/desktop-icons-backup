using System.Text.Json;
using DesktopIconsBackup.Models;

namespace DesktopIconsBackup.Services;

/// <summary>
/// Управляет сохраненными профилями расположения значков рабочего стола.
/// </summary>
internal sealed class DesktopIconLayoutService
{
    /// <summary>
    /// Настройки сериализации профилей в JSON с отступами для удобства чтения.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Каталог, в котором находятся файлы профилей.</summary>
    private readonly string layoutsDirectory;

    /// <summary>Провайдер для получения и применения координат значков.</summary>
    private readonly IDesktopIconPositionProvider positionProvider;

    /// <summary>
    /// Создает сервис профилей с каталогом хранения и провайдером расположения значков.
    /// </summary>
    internal DesktopIconLayoutService(string layoutsDirectory, IDesktopIconPositionProvider positionProvider)
    {
        this.layoutsDirectory = layoutsDirectory;
        this.positionProvider = positionProvider;
    }

    /// <summary>
    /// Возвращает путь к каталогу, в котором хранятся профили.
    /// </summary>
    internal string LayoutsDirectory => layoutsDirectory;

    /// <summary>
    /// Возвращает имена профилей, отсортированные без учета регистра.
    /// Если каталог еще не создан, возвращает пустой список.
    /// </summary>
    internal IReadOnlyList<string> ListProfiles()
    {
        try
        {
            return Directory
                .GetFiles(layoutsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Сохраняет текущее расположение значков в профиль с указанным именем.
    /// </summary>
    internal int SaveProfile(string profileName)
    {
        string path = GetProfilePath(profileName);
        Dictionary<string, IconPosition> positions = positionProvider.CapturePositions();
        if (positions.Count == 0)
            throw new InvalidOperationException("Не найдено иконок для сохранения.");

        Directory.CreateDirectory(layoutsDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(positions, JsonOptions));
        return positions.Count;
    }

    /// <summary>
    /// Загружает профиль и применяет его к рабочему столу, возвращая сведения о несовпадениях значков.
    /// </summary>
    internal LayoutRestoreResult RestoreProfile(string profileName)
    {
        string path = GetProfilePath(profileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Профиль «{profileName}» не найден.", path);

        Dictionary<string, IconPosition>? positions = JsonSerializer.Deserialize<Dictionary<string, IconPosition>>(
            File.ReadAllText(path),
            JsonOptions
        );
        if (positions is null || positions.Count == 0)
            throw new InvalidDataException($"В профиле «{profileName}» нет расположений иконок.");

        return positionProvider.RestorePositions(positions);
    }

    /// <summary>
    /// Проверяет имя профиля и строит путь к соответствующему JSON-файлу.
    /// </summary>
    private string GetProfilePath(string profileName)
    {
        string normalizedName = profileName.Trim();
        if (normalizedName.Length == 0
            || normalizedName is "." or ".."
            || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(Path.GetFileName(normalizedName), normalizedName, StringComparison.Ordinal))
            throw new ArgumentException("Недопустимое название профиля.", nameof(profileName));

        return Path.Combine(layoutsDirectory, normalizedName + ".json");
    }
}
