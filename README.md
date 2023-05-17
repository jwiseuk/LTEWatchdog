# LTE Watchdog

LTE Watchdog is a background service written in C# that monitors the internet connection and performs a power cycle of a specific network adapter when the connection fails a certain number of times within a specified time window. This is useful for automatically resetting the network connection in case of persistent connectivity issues.

## Installation

To install the LTE Watchdog as a Windows service, follow the steps below:

1. Download the latest release from the [Releases](https://github.com/your-username/your-repository/releases) page.

2. Extract the downloaded archive to a directory of your choice.

3. Open a command prompt with administrator privileges and navigate to the extracted directory.

4. Run the following command to install the service:

```
sc create LTEWatchdogService binPath= "{full path to the extracted executable}"
```

Replace `LTEWatchdogService` with the desired name for your service and `{full path to the extracted executable}` with the full path to the extracted `LTEWatchdog.exe` file.

5. Start the service using the following command:

```
sc start LTEWatchdogService
```


The service should now be running and monitoring the internet connection.

## Configuration

By default, the LTE Watchdog pings the Google DNS server (8.8.8.8) every minute to check the internet connection. You can modify this behavior by editing the `CheckNetwork` method in the `InternetConnectionWorker` class. Additionally, the number of connection failures required to trigger a power cycle can be adjusted by modifying the `connectionFailuresCount` threshold in the `InternetConnectionWorker` class.

## Uninstalling the Service

To uninstall the LTE Watchdog service, open a command prompt with administrator privileges and run the following command:

```
sc delete LTEWatchdogService
```

Replace `LTEWatchdogService` with the name of the service you used during installation.
