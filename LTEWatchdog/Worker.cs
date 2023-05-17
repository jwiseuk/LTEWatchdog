using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Management;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace LTEWatchdog
{
    public class InternetConnectionWorker : BackgroundService
    {
        private readonly ILogger<InternetConnectionWorker> _logger;
        private string logFolderPath = @"C:\LTEWatchdog_logs";
        private string? logFilePath;
        private int connectionFailuresCount;
        private Timer? resetCountTimer;

        public InternetConnectionWorker(ILogger<InternetConnectionWorker> logger)
        {
            _logger = logger;
            connectionFailuresCount = 0;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            InitializeLogger();

            // Schedule the timer to reset the count every 10 minutes
            resetCountTimer = new Timer(ResetCount, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));

            while (!stoppingToken.IsCancellationRequested)
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

                    if (connectionFailuresCount >= 1)  // How many times ping should fail in 10 minute window before executing restart. (default =5. Set to 1 for testing)
                    {
                        // Execute the method to power cycle the PCIe device
                        PowerCycleLTE();

                        connectionFailuresCount = 0; // Reset the count after power cycling the device
                    }
                }

                await Task.Delay(1 * 60 * 1000, stoppingToken); // 1 minutes interval for pings
            }
        }

        private void ResetCount(object? state)
        {
            connectionFailuresCount = 0; // Reset the count
        }

        private void InitializeLogger()
        {
            Directory.CreateDirectory(logFolderPath);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            logFilePath = Path.Combine(logFolderPath, $"log_{timestamp}.txt");
        }

        private bool CheckNetwork()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send("8.8.8.8", 1000);

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

        private static void PowerCycleLTE()
        {
         //USB device so unable to reboot via Windows. Will either need to telnet/serial connect and send an AT command. Waiting on info from supplier  
        }
    }
}
