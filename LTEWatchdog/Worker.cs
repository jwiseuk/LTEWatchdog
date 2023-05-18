using System.Net.NetworkInformation;
using System.Diagnostics;

namespace LTEWatchdog
{
    public class InternetConnectionWorker : BackgroundService
    {
        // Configure Script Variables
        static int PingFrequency = 1; //Minutes
        static int ResetWindow = 10; //Length of time until ConnectionFailureLimit is reset (minutes)
        static int ConnectionFailureLimit = 5;
        static string TestAddress = "8.8.8.8";

        // Create Fields
        private readonly ILogger<InternetConnectionWorker> _logger;
        private string logFolderPath = @"C:\LTEWatchdog_logs";
        private string? logFilePath;
        private int connectionFailuresCount;
        private Timer? resetCountTimer;
        private Timer? executeTimer;

        // Constructor
        public InternetConnectionWorker(ILogger<InternetConnectionWorker> logger)
        {
            _logger = logger;
        }

        // Initialize Logic
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                InitializeLogger();
                StartTimers();

                // Wait for the cancellation token to be triggered
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during execution: {ex.Message}");
            }
            finally
            {
                // Stop and dispose the timers
                executeTimer?.Dispose();
                resetCountTimer?.Dispose();
            }
        }

        // Start timers
        private void StartTimers()
        {
            try
            {
                // Set the interval for the executeTimer in minutes
                int executeIntervalMinutes = PingFrequency;
                int executeIntervalMilliseconds = executeIntervalMinutes * 60 * 1000;

                // Start the executeTimer
                executeTimer = new Timer(ExecuteTimerLogic, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(executeIntervalMilliseconds));

                // Set the interval for the resetCountTimer in minutes
                int resetCountIntervalMinutes = ResetWindow;
                int resetCountIntervalMilliseconds = resetCountIntervalMinutes * 60 * 1000;

                // Start the resetCountTimer
                resetCountTimer = new Timer(ResetCount, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(resetCountIntervalMilliseconds));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while starting timers: {ex.Message}");
            }
        }


        // Check for active internet connection and force adapter restart on consecutive failed attempts
        private void ExecuteTimerLogic(object? state)
        {
            try
            {
                bool isConnected = CheckNetwork();
                if (isConnected)
                {
                    LogMessage("Internet connection is active.");
                    connectionFailuresCount = 0; // Reset the count when the connection is active
                }
                else
                {
                    connectionFailuresCount++;
                    LogMessage($"Internet connection is down. Connection failures count: {connectionFailuresCount}");

                    if (connectionFailuresCount >= ConnectionFailureLimit)
                    {
                        PowerCycleLTE();
                        connectionFailuresCount = 0; // Reset the count after power cycling the device
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during timer logic: {ex.Message}");
            }
        }

        // Reset Connection Failures Count
        private void ResetCount(object? state)
        {
            try
            {
                connectionFailuresCount = 0; // Reset the count
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while resetting count: {ex.Message}");
            }
        }

        // Create Log dir+file, clean up logs older than one month
        private void InitializeLogger()
        {
            try
            {
                Directory.CreateDirectory(logFolderPath);
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                logFilePath = Path.Combine(logFolderPath, $"log_{timestamp}.txt");

                // Delete logs older than one month
                string[] logFiles = Directory.GetFiles(logFolderPath, "log_*.txt");
                DateTime oneMonthAgo = DateTime.Now.AddMonths(-1);
                int deletedLogsCount = 0; // Count of deleted logs

                foreach (string file in logFiles)
                {
                    DateTime fileCreationTime = File.GetCreationTime(file);
                    if (fileCreationTime < oneMonthAgo)
                    {
                        File.Delete(file);
                        LogMessage($"Deleted log files: {deletedLogsCount}");
                        deletedLogsCount++;
                    }
                }

                if (deletedLogsCount == 0)
                {
                    LogMessage("No logs to be deleted.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error while initializing logger: {ex.Message}");
            }
        }

        // Ping specified address and log result
        private bool CheckNetwork()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(TestAddress, 1000);

                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error while pinging: {ex.Message}");
            }

            return false;
        }

        // Log Message with timestamps
        private void LogMessage(string message)
        {
            try
            {
                if (logFilePath != null)
                {
                    using (StreamWriter writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine($"[{DateTime.Now}] {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while logging: {ex.Message}");
            }
        }

        // Disable then Enable network adapters containing specified name
        private void PowerCycleLTE()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "Disable-NetAdapter -InterfaceDescription '*quectel*' -Confirm:$false; Start-Sleep -Seconds 5; Enable-NetAdapter -InterfaceDescription '*quectel*' -Confirm:$false",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                    {
                        LogMessage("Network adapters with 'quectel' in their name were disabled and re-enabled successfully.");
                    }
                    else
                    {
                        LogMessage($"Error while power cycling network adapters. Exit code: {p.ExitCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error while power cycling network adapters: {ex.Message}");
            }
        }
    }
}
