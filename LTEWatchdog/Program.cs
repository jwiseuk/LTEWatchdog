using LTEWatchdog;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<InternetConnectionWorker>();
    })
    .Build();

await host.RunAsync();
