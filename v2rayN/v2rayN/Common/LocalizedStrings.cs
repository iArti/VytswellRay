using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace v2rayN.Common;

/// <summary>
/// Singleton holding every UI string in both Russian and English.
/// XAML binds to {Binding Source={x:Static loc:LocalizedStrings.Instance}, Path=...}
/// which auto-refreshes when ToggleLanguage() raises PropertyChanged for all properties.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    public static LocalizedStrings Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _ru = true;
    public bool IsRussian => _ru;
    public string LanguageCode => _ru ? "RU" : "EN";

    public void SetLanguage(bool russian)
    {
        if (_ru == russian) return;
        _ru = russian;
        // Refresh every binding by raising PropertyChanged for all properties.
        foreach (var prop in typeof(LocalizedStrings).GetProperties())
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop.Name));
    }

    public void ToggleLanguage() => SetLanguage(!_ru);

    private string T(string ru, string en) => _ru ? ru : en;

    // Window
    public string AppTitle              => "VytswellRay";

    // Status
    public string StatusDisconnected    => T("Не подключено", "Not connected");
    public string StatusConnecting      => T("Подключение...", "Connecting...");
    public string StatusConnected       => T("Подключено", "Connected");
    public string StatusDisconnecting   => T("Отключение...", "Disconnecting...");
    public string StatusError           => T("Ошибка", "Error");
    public string StatusPrefix          => T("Статус", "Status");
    public string Ping                  => T("Пинг", "Ping");
    public string PingTimeout           => T("тайм-аут", "timeout");
    public string ActivePrefix          => T("Активно", "Active");
    public string NoActiveProfile       => T("нет соединения", "no profile");

    // Buttons
    public string Connect               => T("Подключить", "Connect");
    public string Disconnect            => T("Отключить", "Disconnect");
    public string Connections           => T("Подключения", "Connections");
    public string Settings              => T("Настройки", "Settings");
    public string Close                 => T("Закрыть", "Close");
    public string Cancel                => T("Отмена", "Cancel");
    public string Add                   => T("Добавить", "Add");
    public string Delete                => T("Удалить", "Delete");
    public string SetActive             => T("Сделать активным", "Set active");

    // Tray
    public string TrayStart             => T("Запустить", "Start");
    public string TrayStop              => T("Остановить", "Stop");
    public string TrayExit              => T("Выход", "Exit");

    // Connections dialog
    public string PasteFromClipboard    => T("Вставить URL", "Paste URL");
    public string ScanQrFromScreen      => T("Сканировать QR", "Scan QR");
    public string NoConnections         => T("Нет сохранённых подключений. Добавьте первое.", "No saved connections. Add your first one.");
    public string DeleteConfirm         => T("Удалить это подключение?", "Delete this connection?");
    public string ClipboardEmpty        => T("Буфер обмена пуст", "Clipboard is empty");
    public string ClipboardInvalid      => T("Не удалось распознать URL в буфере обмена", "Could not parse URL from clipboard");
    public string QrNotFound            => T("QR-код на экране не найден", "No QR code found on screen");
    public string QrInvalid             => T("QR-код найден, но не содержит корректную ссылку", "QR code found but does not contain a valid link");
    public string ImportSuccess         => T("Подключение добавлено", "Connection added");

    // Settings dialog
    public string SettingsAutoStart     => T("Запускать с Windows", "Start with Windows");
    public string SettingsAutoConnect   => T("Автоподключение при запуске", "Auto-connect on launch");
    public string SettingsTheme         => T("Тема", "Theme");
    public string ThemeSystem           => T("Система", "System");
    public string ThemeLight            => T("Светлая", "Light");
    public string ThemeDark             => T("Тёмная", "Dark");
    public string SettingsHotkey        => T("Использовать горячую клавишу", "Use global hotkey");
    public string SettingsHotkeyHint    => T("Нажмите комбинацию клавиш...", "Press a key combination...");
    public string SettingsGeoSection    => T("Списки маршрутизации", "Routing lists");
    public string SettingsGeoUpdated    => T("Обновлено", "Updated");
    public string SettingsGeoUpdateNow  => T("Обновить сейчас", "Update now");
    public string SettingsGeoNever      => T("никогда", "never");
    public string SettingsGeoUpdating   => T("Обновление...", "Updating...");
    public string SettingsExportLogs    => T("Экспортировать логи", "Export logs");
    public string SettingsOpenLogFolder => T("Открыть папку логов", "Open log folder");
    public string SettingsVersion       => T("Версия", "Version");

    // Hidden (BT)
    public string SecretToggle          => T("Направлять торрент через прокси", "Route torrent via proxy");
    public string SecretUnlocked        => T("Расширенные настройки разблокированы", "Advanced settings unlocked");

    // Hotkey errors
    public string HotkeyConflict        => T("Не удалось зарегистрировать горячую клавишу", "Could not register hotkey");

    // Logs export
    public string LogsExportTitle       => T("Сохранить логи", "Save logs");
    public string LogsExportFilter      => T("ZIP-архив (*.zip)|*.zip", "ZIP archive (*.zip)|*.zip");
    public string LogsExportDone        => T("Логи сохранены", "Logs exported");
    public string LogsExportFailed      => T("Не удалось сохранить логи", "Failed to export logs");
}
