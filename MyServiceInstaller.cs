using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace pdf2png
{
    [RunInstaller(true)]
    public class MyServiceInstaller : Installer
    {
        public MyServiceInstaller()
        {
            var spi = new ServiceProcessInstaller();
            var si = new ServiceInstaller();

            spi.Account = ServiceAccount.LocalSystem;
            spi.Username = null;
            spi.Password = null;

            si.DisplayName = Service.SERVICE_ID;
            si.ServiceName = Service.SERVICE_ID;
            si.StartType = ServiceStartMode.Automatic;

            Installers.Add(spi);
            Installers.Add(si);
        }
    }
}
