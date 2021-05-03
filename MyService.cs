using System;
using System.ServiceProcess;

namespace pdf2png
{
    public class Service : ServiceBase
    {
        public static readonly string SERVICE_ID = Guid.NewGuid().ToString();
        public Service()
        {
            ServiceName = SERVICE_ID;
        }

        protected override void OnStart(string[] args)
        {
            Program.StartOnWindowService(args);
        }

        protected override void OnStop()
        {
            Program.Stop();
        }
    }
}
