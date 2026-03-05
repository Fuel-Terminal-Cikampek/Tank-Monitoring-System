using System;
using System.IO;
using System.Text;

namespace TMS.ATGService.Service
{
    /// <summary>
    /// File Logger untuk ATG Service
    /// - Menulis log ke file harian
    /// - Reset otomatis setiap jam 00:00
    /// - Folder Logs dibuat otomatis relatif ke direktori aplikasi
    /// </summary>
    public class FileLogger : IDisposable
    {
        private readonly string _logDirectory;
        private StreamWriter _writer;
        private string _currentLogDate;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public FileLogger()
        {
            // ✅ Path relatif ke direktori aplikasi: .\Logs\
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _logDirectory = Path.Combine(appDirectory, "Logs");

            // Buat folder Logs jika belum ada
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                Console.WriteLine($"[FileLogger] Created log directory: {_logDirectory}");
            }

            // Initialize log file untuk hari ini
            InitializeLogFile();
        }

        /// <summary>
        /// Initialize atau rotate log file berdasarkan tanggal
        /// </summary>
        private void InitializeLogFile()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            // Jika tanggal berubah (lewat jam 00:00), buat file baru
            if (_currentLogDate != today)
            {
                lock (_lock)
                {
                    // Close existing writer
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Close();
                        _writer.Dispose();
                    }

                    _currentLogDate = today;
                    string logFileName = $"ATGService_{today}.log";
                    string logFilePath = Path.Combine(_logDirectory, logFileName);

                    // Create new writer dengan append mode
                    _writer = new StreamWriter(logFilePath, append: true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    // Write header untuk hari baru
                    _writer.WriteLine("");
                    _writer.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
                    _writer.WriteLine($"  ATG SERVICE LOG - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    _writer.WriteLine($"  Log Directory: {_logDirectory}");
                    _writer.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
                    _writer.WriteLine("");

                    Console.WriteLine($"[FileLogger] Log file initialized: {logFilePath}");
                }
            }
        }

        /// <summary>
        /// Write log message dengan timestamp
        /// </summary>
        public void Log(string message)
        {
            if (_disposed) return;

            // Check jika perlu rotate (tanggal berubah)
            InitializeLogFile();

            lock (_lock)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    _writer?.WriteLine($"[{timestamp}] {message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileLogger] Error writing to log: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Write log tanpa timestamp (untuk box/table output)
        /// </summary>
        public void LogRaw(string message)
        {
            if (_disposed) return;

            // Check jika perlu rotate
            InitializeLogFile();

            lock (_lock)
            {
                try
                {
                    _writer?.WriteLine(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileLogger] Error writing to log: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Write ke Console DAN Log file sekaligus
        /// </summary>
        public void WriteLineAndLog(string message)
        {
            Console.WriteLine(message);
            LogRaw(message);
        }

        /// <summary>
        /// Get current log file path
        /// </summary>
        public string GetCurrentLogPath()
        {
            return Path.Combine(_logDirectory, $"ATGService_{_currentLogDate}.log");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                lock (_lock)
                {
                    if (_writer != null)
                    {
                        _writer.Flush();
                        _writer.Close();
                        _writer.Dispose();
                        _writer = null;
                    }
                }
            }
        }
    }
}
