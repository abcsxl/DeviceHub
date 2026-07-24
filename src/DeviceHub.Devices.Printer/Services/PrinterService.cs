using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.Printer.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.Printer.Services;

#pragma warning disable CA1416

public class PrinterService : IPrinterService, IHardwareEndpointRegistrar
{
    private readonly ILogger<PrinterService> _logger;
    private readonly object _statusLock = new();
    private HardwareStatus _status = HardwareStatus.Stopped;
    private int _jobCounter;

    public string Name => "Printer";
    public HardwareStatus Status { get { lock (_statusLock) { return _status; } } }

    public event EventHandler<PrinterJobEventArgs>? JobCompleted;
    public event EventHandler<PrinterJobEventArgs>? JobError;

    public PrinterService(ILogger<PrinterService> logger) => _logger = logger;

    public Task InitAsync(CancellationToken ct = default)
    {
        lock (_statusLock) { _status = HardwareStatus.Running; }
        _logger.LogInformation("Printer service started");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        lock (_statusLock) { _status = HardwareStatus.Stopped; }
        _logger.LogInformation("Printer service stopped");
        return Task.CompletedTask;
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => PrinterEndpoint.MapEndpoints(app);

    public Task<List<PrinterInfo>> GetPrintersAsync(CancellationToken ct = default)
    {
        var printers = new List<PrinterInfo>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnumerateWindowsPrinters(printers);
        else
            EnumerateLinuxPrinters(printers);

        return Task.FromResult(printers);
    }

    public async Task<bool> PrintTextAsync(string text, string? printerName = null, CancellationToken ct = default)
    {
        var name = ResolvePrinterName(printerName);
        if (string.IsNullOrEmpty(name))
            return false;

        var jobId = $"print-{Interlocked.Increment(ref _jobCounter)}";
        try
        {
            bool success;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                success = WindowsPrintText(text, name);
            else
                success = await LinuxPrintText(text, name, ct);

            if (success)
                JobCompleted?.Invoke(this, new PrinterJobEventArgs(jobId, name, "completed"));
            else
                JobError?.Invoke(this, new PrinterJobEventArgs(jobId, name, "error", "Print failed"));
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print text to {Printer}", name);
            JobError?.Invoke(this, new PrinterJobEventArgs(jobId, name, "error", ex.Message));
            return false;
        }
    }

    public async Task<bool> PrintRawAsync(byte[] data, string? printerName = null, CancellationToken ct = default)
    {
        var name = ResolvePrinterName(printerName);
        if (string.IsNullOrEmpty(name))
            return false;

        var jobId = $"print-{Interlocked.Increment(ref _jobCounter)}";
        try
        {
            bool success;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                success = WindowsRawPrint(name, data);
            else
                success = await LinuxRawPrint(name, data, ct);

            if (success)
                JobCompleted?.Invoke(this, new PrinterJobEventArgs(jobId, name, "completed"));
            else
                JobError?.Invoke(this, new PrinterJobEventArgs(jobId, name, "error", "Print failed"));
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print raw data to {Printer}", name);
            JobError?.Invoke(this, new PrinterJobEventArgs(jobId, name, "error", ex.Message));
            return false;
        }
    }

    private string ResolvePrinterName(string? printerName)
    {
        if (!string.IsNullOrEmpty(printerName))
            return printerName;

        foreach (string name in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
        {
            var settings = new System.Drawing.Printing.PrinterSettings { PrinterName = name };
            if (settings.IsDefaultPrinter)
                return name;
        }

        var first = System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().FirstOrDefault();
        if (first != null)
            return first;

        throw new InvalidOperationException("PRINTER_NOT_FOUND");
    }

    private static void EnumerateWindowsPrinters(List<PrinterInfo> printers)
    {
        foreach (string name in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
        {
            try
            {
                var settings = new System.Drawing.Printing.PrinterSettings { PrinterName = name };
                printers.Add(new PrinterInfo(name, "ready", settings.IsDefaultPrinter));
            }
            catch
            {
                printers.Add(new PrinterInfo(name, "error", false));
            }
        }
    }

    private void EnumerateLinuxPrinters(List<PrinterInfo> printers)
    {
        try
        {
            var psi = new ProcessStartInfo("lpstat", "-a")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            if (process == null) return;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    printers.Add(new PrinterInfo(parts[0], "ready", parts.Length > 1 && parts[1] == "accepting"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate printers via lpstat");
        }
    }

    private static bool WindowsPrintText(string text, string printerName)
    {
        var complete = false;
        using var pd = new System.Drawing.Printing.PrintDocument();
        pd.PrinterSettings.PrinterName = printerName;
        pd.DocumentName = "DeviceHub Print";

        pd.PrintPage += (sender, e) =>
        {
            if (e.Graphics == null) return;

            var font = new System.Drawing.Font("Microsoft YaHei", 10);
            var printRect = e.MarginBounds;
            var sf = new System.Drawing.StringFormat(System.Drawing.StringFormatFlags.LineLimit);
            var charsFitted = 0;
            var linesFilled = 0;

            e.Graphics.MeasureString(text, font, printRect.Size, sf, out charsFitted, out linesFilled);

            if (charsFitted < text.Length)
            {
                e.HasMorePages = true;
                e.Graphics.DrawString(text[..charsFitted], font, System.Drawing.Brushes.Black, printRect, sf);
                text = text[charsFitted..];
            }
            else
            {
                e.HasMorePages = false;
                e.Graphics.DrawString(text, font, System.Drawing.Brushes.Black, printRect, sf);
                complete = true;
            }
        };

        pd.Print();
        return complete;
    }

    private static async Task<bool> LinuxPrintText(string text, string printerName, CancellationToken ct)
    {
        var data = Encoding.UTF8.GetBytes(text);
        return await LinuxRawPrint(printerName, data, ct);
    }

    private static bool WindowsRawPrint(string printerName, byte[] data)
    {
        if (OpenPrinter(printerName, out var hPrinter, nint.Zero) == 0)
            return false;

        try
        {
            var docName = Marshal.StringToHGlobalUni("DeviceHub Print Job");
            var dataType = Marshal.StringToHGlobalUni("RAW");

            try
            {
                var docInfo = new DOC_INFO_1
                {
                    pDocName = docName,
                    pOutputFile = nint.Zero,
                    pDatatype = dataType
                };

                if (StartDocPrinter(hPrinter, 1, ref docInfo) == 0)
                    return false;

                try
                {
                    if (StartPagePrinter(hPrinter) == 0)
                        return false;

                    try
                    {
                        if (WritePrinter(hPrinter, data, data.Length, out _) == 0)
                            return false;
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(docName);
                Marshal.FreeHGlobal(dataType);
            }

            return true;
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    private static async Task<bool> LinuxRawPrint(string printerName, byte[] data, CancellationToken ct)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, data, ct);
            var psi = new ProcessStartInfo("lp", $"-d \"{printerName}\" -o raw \"{tempFile}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                ArgumentList = { "-d", printerName, "-o", "raw", tempFile }
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    // Win32 Print Spooler API
    private const string WinSpool = "winspool.drv";

    [DllImport(WinSpool, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int OpenPrinter(string pPrinterName, out nint phPrinter, nint pDefault);

    [DllImport(WinSpool, SetLastError = true)]
    private static extern int StartDocPrinter(nint hPrinter, int level, ref DOC_INFO_1 pDocInfo);

    [DllImport(WinSpool, SetLastError = true)]
    private static extern int StartPagePrinter(nint hPrinter);

    [DllImport(WinSpool, SetLastError = true)]
    private static extern int WritePrinter(nint hPrinter, byte[] pBuf, int cbBuf, out int pcWritten);

    [DllImport(WinSpool, SetLastError = true)]
    private static extern int EndPagePrinter(nint hPrinter);

    [DllImport(WinSpool, SetLastError = true)]
    private static extern int EndDocPrinter(nint hPrinter);

    [DllImport(WinSpool, SetLastError = true)]
    private static extern int ClosePrinter(nint hPrinter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        public nint pDocName;
        public nint pOutputFile;
        public nint pDatatype;
    }
}
