using Agendador;
using System;
using System.ServiceProcess;
using System.Threading;

namespace Agendador
{
    static class Program
    {
        private static ManualResetEvent shutdownEvent = new ManualResetEvent(false);
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                // Modo console para testes.
                AgendadorService service = new AgendadorService();
                service.StartService(args);
                shutdownEvent.WaitOne();
                service.StopService();
            }
            else
            {
                // Modo Windows Service.
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new AgendadorService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
