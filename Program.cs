using System.ComponentModel;
using System.Text.Json;
using DesktopIconsBackup.Services;

namespace DesktopIconsBackup;

/// <summary>
/// Запускает консольный интерфейс для сохранения и восстановления расположения значков.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Проверяет платформу, создает сервисы приложения и обрабатывает аргументы командной строки.
    /// </summary>
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Это приложение работает только в Windows.");
            return 1;
        }

        var service = new DesktopIconLayoutService(
            Path.Combine(AppContext.BaseDirectory, "Layouts"),
            new Win32DesktopIconPositionProvider()
        );

        try
        {
            return args.Length == 0 ? RunInteractive(service) : RunCommand(service, args);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidOperationException
                or ArgumentException
                or Win32Exception
        )
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Выполняет переданную пользователем CLI-команду.
    /// </summary>
    private static int RunCommand(DesktopIconLayoutService service, string[] args)
    {
        string command = args[0].ToLowerInvariant();
        if (command is "help" or "--help" or "-h")
        {
            PrintUsage();
            return 0;
        }

        if (command is "list" or "--list")
        {
            if (args.Length != 1)
                return PrintUsageAndFail();

            PrintProfiles(service);
            return 0;
        }

        if (args.Length != 2)
            return PrintUsageAndFail();

        switch (command)
        {
            case "save":
            case "--save":
                SaveProfile(service, args[1]);
                return 0;
            case "restore":
            case "--restore":
                RestoreProfile(service, args[1]);
                return 0;
            default:
                return PrintUsageAndFail();
        }
    }

    /// <summary>
    /// Запускает интерактивное меню управления профилями.
    /// </summary>
    private static int RunInteractive(DesktopIconLayoutService service)
    {
        Console.WriteLine("=== Резервное копирование расположения иконок рабочего стола ===");
        Console.WriteLine($"Папка профилей: {service.LayoutsDirectory}");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1. Сохранить текущее расположение");
            Console.WriteLine("2. Восстановить сохраненное расположение");
            Console.WriteLine("3. Показать сохраненные профили");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите действие: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    RunInteractiveOperation(() => SaveProfileInteractively(service));
                    break;
                case "2":
                    RunInteractiveOperation(() => RestoreProfileInteractively(service));
                    break;
                case "3":
                    RunInteractiveOperation(() => PrintProfiles(service));
                    break;
                case "0":
                case null:
                    return 0;
                default:
                    Console.WriteLine("Неизвестная команда. Введите 1, 2, 3 или 0.");
                    break;
            }
        }
    }

    /// <summary>
    /// Запрашивает имя профиля и при необходимости подтверждение перезаписи.
    /// </summary>
    private static void SaveProfileInteractively(DesktopIconLayoutService service)
    {
        Console.Write("Название профиля: ");
        string? name = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Название профиля не может быть пустым.");
            return;
        }

        if (service.ListProfiles().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Console.Write($"Профиль «{name}» уже существует. Перезаписать? (д/н): ");
            string? answer = Console.ReadLine()?.Trim();
            if (answer?.ToLowerInvariant() is not ("д" or "да" or "y" or "yes"))
            {
                Console.WriteLine("Сохранение отменено.");
                return;
            }
        }

        SaveProfile(service, name);
    }

    /// <summary>
    /// Выполняет действие меню и отображает ожидаемую ошибку, не закрывая приложение.
    /// </summary>
    private static void RunInteractiveOperation(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidOperationException
                or ArgumentException
                or Win32Exception
        )
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
        }
    }

    /// <summary>
    /// Предлагает выбрать профиль из списка и применяет его расположение.
    /// </summary>
    private static void RestoreProfileInteractively(DesktopIconLayoutService service)
    {
        IReadOnlyList<string> profiles = service.ListProfiles();
        if (profiles.Count == 0)
        {
            Console.WriteLine("Сохраненных профилей нет. Сначала сохраните расположение.");
            return;
        }

        Console.WriteLine("Выберите профиль для восстановления:");
        for (int i = 0; i < profiles.Count; i++)
            Console.WriteLine($"{i + 1}. {profiles[i]}");

        Console.Write("Номер профиля: ");
        if (!int.TryParse(Console.ReadLine(), out int index) || index < 1 || index > profiles.Count)
        {
            Console.WriteLine("Некорректный номер профиля.");
            return;
        }

        RestoreProfile(service, profiles[index - 1]);
    }

    /// <summary>
    /// Сохраняет профиль и сообщает количество сохраненных значков.
    /// </summary>
    private static void SaveProfile(DesktopIconLayoutService service, string name)
    {
        int count = service.SaveProfile(name);
        Console.WriteLine($"Профиль «{name}» сохранен. Иконок: {count}.");
    }

    /// <summary>
    /// Восстанавливает указанный профиль и отображает различия в наборах значков.
    /// </summary>
    private static void RestoreProfile(DesktopIconLayoutService service, string name)
    {
        var result = service.RestoreProfile(name);
        Console.WriteLine($"Профиль «{name}»: восстановлено иконок {result.RestoredCount}.");
        PrintIconDifferences("Сохраненные иконки, которых сейчас нет на рабочем столе", result.MissingIcons);
        PrintIconDifferences(
            "Иконки на рабочем столе, отсутствующие в сохранении (оставлены без изменений)",
            result.UnSavedIcons
        );
    }

    /// <summary>
    /// Выводит список сохраненных профилей или сообщение об их отсутствии.
    /// </summary>
    private static void PrintProfiles(DesktopIconLayoutService service)
    {
        IReadOnlyList<string> profiles = service.ListProfiles();
        if (profiles.Count == 0)
        {
            Console.WriteLine("Сохраненных профилей пока нет.");
            return;
        }

        Console.WriteLine("Сохраненные профили:");
        foreach (string profile in profiles)
            Console.WriteLine($"- {profile}");
    }

    /// <summary>
    /// Выводит заголовок и имена значков, если список различий не пуст.
    /// </summary>
    private static void PrintIconDifferences(string heading, IReadOnlyList<string> iconNames)
    {
        if (iconNames.Count == 0)
            return;

        Console.WriteLine($"{heading}: {iconNames.Count}");
        foreach (string name in iconNames)
            Console.WriteLine($"- {name}");
    }

    /// <summary>
    /// Показывает справку и возвращает код ошибки для некорректной команды.
    /// </summary>
    private static int PrintUsageAndFail()
    {
        PrintUsage();
        return 1;
    }

    /// <summary>
    /// Показывает синтаксис командной строки приложения.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Использование:");
        Console.WriteLine("  DesktopIconsBackup save <название>");
        Console.WriteLine("  DesktopIconsBackup restore <название>");
        Console.WriteLine("  DesktopIconsBackup list");
        Console.WriteLine("Без аргументов запускается интерактивное меню.");
        Console.WriteLine("Профили хранятся в папке Layouts рядом с приложением.");
    }
}
