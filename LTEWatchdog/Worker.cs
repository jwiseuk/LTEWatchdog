using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Management;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

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

                    if (connectionFailuresCount >= 5)
                    {
                        // Execute the method to power cycle the PCIe device
                        PowerCyclePcieDevice();

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

        private void PowerCyclePcieDevice()
        {

            string slotNumber = "1"; // PCIe slot number

            {

                // Get the device ID from the specified PCIe slot
                string deviceId = GetDeviceIdFromPciSlot(slotNumber);
                if (deviceId != null)
                {
                    LogMessage($"Device ID in Slot {slotNumber}: {deviceId}");
                }
                else
                {
                    LogMessage($"No device found in Slot {slotNumber}.");
                    return;
                }

                // Disable the device
                bool disableResult = DisableDevice(deviceId);
                if (disableResult)
                {
                    LogMessage("Device disabled successfully.");
                }
                else
                {
                    LogMessage("Failed to disable the device.");
                    return;
                }

                // Wait for a few seconds (adjust the delay as needed)
                System.Threading.Thread.Sleep(5000);

                // Enable the device
                bool enableResult = EnableDevice(deviceId);
                if (enableResult)
                {
                    LogMessage("Device enabled successfully.");
                }
                else
                {
                    LogMessage("Failed to enable the device.");
                    return;
                }

                Console.WriteLine("Power cycle complete.");
            }
        }

        static string GetDeviceIdFromPciSlot(string slotNumber)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PCIControllerDevice WHERE Antecedent LIKE '%PCI\\\\BUS_0%Device_0%Function_" + slotNumber + "%'"))
            {
                foreach (ManagementObject controllerDevice in searcher.Get())
                {
                    foreach (ManagementObject device in controllerDevice.GetRelated("Win32_PnPEntity"))
                    {
                        return device["DeviceID"]?.ToString();
                    }
                }
            }

            return null;
        }

        static bool DisableDevice(string deviceId)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID = '" + deviceId + "'"))
            {
                foreach (ManagementObject device in searcher.Get())
                {
                    try
                    {
                        device.InvokeMethod("Disable", default(object[]));
                        return true;
                    }
                    catch (ManagementException ex)
                    {
                        Console.WriteLine("Failed to disable device: " + ex.Message);
                    }
                }
            }

            return false;
        }

        static bool EnableDevice(string deviceId)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE DeviceID = '" + deviceId + "'"))
            {
                foreach (ManagementObject device in searcher.Get())
                {
                    try
                    {
                        device.InvokeMethod("Enable", default(object[]));
                        return true;
                    }
                    catch (ManagementException ex)
                    {
                        Console.WriteLine("Failed to enable device: " + ex.Message);
                    }
                }
            }

            return false;
        }
    }
}
