using System.Runtime.InteropServices;

namespace DesktopIconsBackup.Interop;

/// <summary>
/// Содержит Win32-константы, структуры и импорты для управления списком значков Explorer.
/// </summary>
internal static class DesktopIconWinApi
{
    /// <summary>Сообщение ListView для получения числа элементов.</summary>
    internal const uint LVM_GETITEMCOUNT = 0x1004;

    /// <summary>Сообщение ListView для получения координат элемента.</summary>
    internal const uint LVM_GETITEMPOSITION = 0x1010;

    /// <summary>Сообщение ListView для установки координат элемента.</summary>
    internal const uint LVM_SETITEMPOSITION = 0x100F;

    /// <summary>Сообщение ListView для получения данных элемента в Unicode.</summary>
    internal const uint LVM_GETITEMW = 0x104B;

    /// <summary>Флаг элемента ListView, указывающий на запрос текста.</summary>
    internal const uint LVIF_TEXT = 0x0001;

    /// <summary>Право на выделение и изменение памяти целевого процесса.</summary>
    internal const uint PROCESS_VM_OPERATION = 0x0008;

    /// <summary>Право на чтение памяти целевого процесса.</summary>
    internal const uint PROCESS_VM_READ = 0x0010;

    /// <summary>Право на запись в память целевого процесса.</summary>
    internal const uint PROCESS_VM_WRITE = 0x0020;

    /// <summary>Флаг фиксации выделенной страницы памяти.</summary>
    internal const uint MEM_COMMIT = 0x1000;

    /// <summary>Флаг резервирования диапазона памяти.</summary>
    internal const uint MEM_RESERVE = 0x2000;

    /// <summary>Флаг освобождения всего зарезервированного диапазона памяти.</summary>
    internal const uint MEM_RELEASE = 0x8000;

    /// <summary>Защита выделенной памяти: чтение и запись.</summary>
    internal const uint PAGE_READWRITE = 0x04;

    /// <summary>
    /// Совместимое с POINT представление двухмерных целочисленных координат Win32.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        /// <summary>Координата по горизонтали.</summary>
        internal int X;

        /// <summary>Координата по вертикали.</summary>
        internal int Y;
    }

    /// <summary>
    /// Совместимое с LVITEM представление параметров элемента системного списка Explorer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ListViewItem
    {
        /// <summary>Флаги полей элемента, которые запрашиваются или изменяются.</summary>
        internal uint Mask;

        /// <summary>Индекс элемента в списке.</summary>
        internal int ItemIndex;

        /// <summary>Индекс подэлемента.</summary>
        internal int SubItemIndex;

        /// <summary>Текущее состояние элемента.</summary>
        internal uint State;

        /// <summary>Маска состояний, используемых при чтении или изменении состояния.</summary>
        internal uint StateMask;

        /// <summary>Указатель на буфер текста в памяти процесса Explorer.</summary>
        internal IntPtr TextPointer;

        /// <summary>Максимальное количество символов в буфере текста.</summary>
        internal int TextCapacity;

        /// <summary>Индекс изображения элемента.</summary>
        internal int ImageIndex;

        /// <summary>Дополнительное значение, связанное с элементом.</summary>
        internal IntPtr Parameter;

        /// <summary>Уровень отступа элемента.</summary>
        internal int Indent;
    }

    /// <summary>
    /// Находит верхнеуровневое окно по имени класса и заголовку.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindWindow(string? className, string? windowName);

    /// <summary>
    /// Находит дочернее окно, соответствующее имени класса и заголовку.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName
    );

    /// <summary>
    /// Отправляет оконное сообщение указанному окну.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SendMessage(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam
    );

    /// <summary>
    /// Возвращает идентификатор процесса и потока, создавших окно.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    /// <summary>
    /// Открывает существующий процесс с указанными правами доступа.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    /// <summary>
    /// Резервирует и фиксирует память в адресном пространстве другого процесса.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr VirtualAllocEx(
        IntPtr process,
        IntPtr address,
        uint size,
        uint allocationType,
        uint protection
    );

    /// <summary>
    /// Освобождает память, выделенную в другом процессе.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFreeEx(
        IntPtr process,
        IntPtr address,
        uint size,
        uint freeType
    );

    /// <summary>
    /// Записывает структуру элемента в память другого процесса.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteProcessMemory(
        IntPtr process,
        IntPtr address,
        ref ListViewItem buffer,
        uint size,
        out IntPtr bytesWritten
    );

    /// <summary>
    /// Читает координаты POINT из памяти другого процесса.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        out Point buffer,
        uint size,
        out IntPtr bytesRead
    );

    /// <summary>
    /// Читает последовательность байтов из памяти другого процесса.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr address,
        byte[] buffer,
        uint size,
        out IntPtr bytesRead
    );

    /// <summary>
    /// Закрывает открытый дескриптор системного объекта.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);
}
