using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using DesktopIconsBackup.Interop;
using DesktopIconsBackup.Models;
using static DesktopIconsBackup.Interop.DesktopIconWinApi;

namespace DesktopIconsBackup.Services;

/// <summary>
/// Получает и применяет координаты значков рабочего стола через Win32 API.
/// </summary>
internal sealed class Win32DesktopIconPositionProvider : IDesktopIconPositionProvider
{
    /// <summary>Размер буфера для чтения имени значка в байтах.</summary>
    private const int IconNameBufferSize = 512;

    /// <summary>
    /// Считывает имена и координаты значков из системного списка рабочего стола.
    /// </summary>
    public Dictionary<string, IconPosition> CapturePositions()
    {
        IntPtr listView = GetDesktopListViewHandle();
        int itemCount = GetItemCount(listView);
        using var process = OpenDesktopProcess(listView);
        using var memory = new RemoteMemory(process.Handle);

        IntPtr itemAddress = memory.Allocate((uint)Marshal.SizeOf<ListViewItem>());
        IntPtr pointAddress = memory.Allocate((uint)Marshal.SizeOf<Point>());
        IntPtr textAddress = memory.Allocate(IconNameBufferSize);
        var positions = new Dictionary<string, IconPosition>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < itemCount; index++)
        {
            string name = ReadItemName(process.Handle, listView, itemAddress, textAddress, index);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            IntPtr positionResult = SendMessage(
                listView,
                LVM_GETITEMPOSITION,
                (IntPtr)index,
                pointAddress
            );
            if (positionResult == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось получить координаты иконки.");

            if (!ReadProcessMemory(
                process.Handle,
                pointAddress,
                out Point point,
                (uint)Marshal.SizeOf<Point>(),
                out _
            ))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось прочитать координаты иконки.");

            positions.TryAdd(name, new IconPosition(point.X, point.Y));
        }

        return positions;
    }

    /// <summary>
    /// Перемещает совпавшие значки и возвращает списки отсутствующих и новых значков.
    /// </summary>
    public LayoutRestoreResult RestorePositions(IReadOnlyDictionary<string, IconPosition> positions)
    {
        IntPtr listView = GetDesktopListViewHandle();
        int itemCount = GetItemCount(listView);
        using var process = OpenDesktopProcess(listView);
        using var memory = new RemoteMemory(process.Handle);

        IntPtr itemAddress = memory.Allocate((uint)Marshal.SizeOf<ListViewItem>());
        IntPtr textAddress = memory.Allocate(IconNameBufferSize);
        var currentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int restoredCount = 0;

        for (int index = 0; index < itemCount; index++)
        {
            string name = ReadItemName(process.Handle, listView, itemAddress, textAddress, index);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            currentNames.Add(name);
            if (!positions.TryGetValue(name, out IconPosition? position))
                continue;

            int packedCoordinates = (position.Y << 16) | (position.X & 0xFFFF);
            SendMessage(listView, LVM_SETITEMPOSITION, (IntPtr)index, (IntPtr)packedCoordinates);
            restoredCount++;
        }

        string[] missingIcons = positions.Keys
            .Where(name => !currentNames.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] unsavedIcons = currentNames
            .Where(name => !positions.ContainsKey(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LayoutRestoreResult(restoredCount, missingIcons, unsavedIcons);
    }

    /// <summary>
    /// Находит системный ListView, в котором Explorer отображает значки рабочего стола.
    /// </summary>
    private static IntPtr GetDesktopListViewHandle()
    {
        IntPtr programManager = FindWindow("Progman", "Program Manager");
        IntPtr shellView = FindWindowEx(programManager, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellView == IntPtr.Zero)
        {
            IntPtr workerWindow = IntPtr.Zero;
            while ((workerWindow = FindWindowEx(
                IntPtr.Zero,
                workerWindow,
                "WorkerW",
                null
            )) != IntPtr.Zero)
            {
                shellView = FindWindowEx(workerWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                    break;
            }
        }

        IntPtr listView = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
        if (listView == IntPtr.Zero)
            throw new InvalidOperationException("Не удалось найти список иконок рабочего стола.");

        return listView;
    }

    /// <summary>
    /// Возвращает число элементов в системном списке значков.
    /// </summary>
    private static int GetItemCount(IntPtr listView)
    {
        int count = (int)SendMessage(listView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (count < 0)
            throw new InvalidOperationException("Не удалось получить количество иконок рабочего стола.");
        return count;
    }

    /// <summary>
    /// Открывает процесс, которому принадлежит окно списка значков.
    /// </summary>
    private static RemoteProcess OpenDesktopProcess(IntPtr listView)
    {
        GetWindowThreadProcessId(listView, out uint processId);
        if (processId == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось определить процесс рабочего стола.");

        IntPtr handle = OpenProcess(
            PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE,
            false,
            processId
        );
        if (handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось открыть процесс рабочего стола.");

        return new RemoteProcess(handle);
    }

    /// <summary>
    /// Читает имя элемента списка через буфер в памяти процесса Explorer.
    /// </summary>
    private static string ReadItemName(
        IntPtr process,
        IntPtr listView,
        IntPtr itemAddress,
        IntPtr textAddress,
        int index
    )
    {
        var item = new ListViewItem
        {
            Mask = LVIF_TEXT,
            ItemIndex = index,
            TextPointer = textAddress,
            TextCapacity = IconNameBufferSize / sizeof(char),
        };
        if (!WriteProcessMemory(
            process,
            itemAddress,
            ref item,
            (uint)Marshal.SizeOf<ListViewItem>(),
            out _
        ))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось передать запрос к списку иконок.");

        if (SendMessage(listView, LVM_GETITEMW, (IntPtr)index, itemAddress) == IntPtr.Zero)
            throw new InvalidOperationException($"Не удалось прочитать имя иконки с индексом {index}.");

        byte[] textBuffer = new byte[IconNameBufferSize];
        if (!ReadProcessMemory(process, textAddress, textBuffer, (uint)textBuffer.Length, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось прочитать имя иконки.");

        return Encoding.Unicode.GetString(textBuffer).Split('\0')[0].Trim();
    }

    /// <summary>
    /// Владеет дескриптором процесса Explorer и закрывает его при освобождении.
    /// </summary>
    private sealed class RemoteProcess : IDisposable
    {
        /// <summary>Дескриптор открытого процесса рабочего стола.</summary>
        internal IntPtr Handle { get; }

        /// <summary>
        /// Создает владельца для переданного дескриптора процесса.
        /// </summary>
        internal RemoteProcess(IntPtr handle) => Handle = handle;

        /// <summary>
        /// Закрывает принадлежащий объекту дескриптор процесса.
        /// </summary>
        public void Dispose()
        {
            if (!CloseHandle(Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось закрыть дескриптор процесса рабочего стола.");
        }
    }

    /// <summary>
    /// Управляет выделенными областями памяти внутри процесса Explorer.
    /// </summary>
    private sealed class RemoteMemory : IDisposable
    {
        /// <summary>Дескриптор процесса, в памяти которого выделяются буферы.</summary>
        private readonly IntPtr process;

        /// <summary>Адреса областей памяти, которые нужно освободить при завершении.</summary>
        private readonly List<IntPtr> allocations = new();

        /// <summary>
        /// Создает менеджер удаленной памяти для указанного процесса.
        /// </summary>
        internal RemoteMemory(IntPtr process) => this.process = process;

        /// <summary>
        /// Выделяет область памяти в процессе Explorer и запоминает ее для освобождения.
        /// </summary>
        internal IntPtr Allocate(uint size)
        {
            IntPtr address = VirtualAllocEx(
                process,
                IntPtr.Zero,
                size,
                MEM_COMMIT | MEM_RESERVE,
                PAGE_READWRITE
            );
            if (address == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось выделить память в процессе рабочего стола.");

            allocations.Add(address);
            return address;
        }

        /// <summary>
        /// Освобождает все области, выделенные этим менеджером.
        /// </summary>
        public void Dispose()
        {
            foreach (IntPtr address in allocations)
            {
                if (!VirtualFreeEx(process, address, 0, MEM_RELEASE))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось освободить память процесса рабочего стола.");
            }
        }
    }
}
