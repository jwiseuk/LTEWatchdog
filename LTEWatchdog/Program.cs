using System.Diagnostics;
using System.ServiceProcess;

namespace LTEWatchdog
{
    public class Program : ServiceBase
    {
        private IHost _host;

        public static void Main(string[] args)
        {
            var isService = !(Debugger.IsAttached || args.Contains("--console"));
            if (isService)
            {
                Run(new Program());
            }
            else
            {
                CreateHostBuilder(args).Build().Run();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<InternetConnectionWorker>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });

        protected override void OnStart(string[] args)
        {
            _host = CreateHostBuilder(args).Build();
            _host.Start();
        }

        protected override void OnStop()
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
    }
}