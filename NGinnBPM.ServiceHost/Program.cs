using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace NGinnBPM.ServiceHost
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine("Installer parameters:");
                Console.WriteLine(" /servicename=<service name> - name of the windows service");
                Console.WriteLine(" /account=[LocalService|LocalSystem|User|NetworkService] - windows account type");
                Console.WriteLine("\nCommand line parameters:");
                Console.WriteLine(" -debug - run in console mode");

                if (args[0] == "-debug")
                {
                    Debug(args);
                }
                return 0;
            }
            
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new Service1() 
			};
            ServiceBase.Run(ServicesToRun);
            return 0;
        }

        static void Debug(string[] args)
        {
            try
            {
                
                Console.WriteLine("Enter to start....");
                Console.ReadLine();
                var s = new Service1();
                s.Start(args);
                Console.WriteLine("Enter to exit....");
                Console.ReadLine();
                s.Stop();
            }
            catch (Exception ex)
            {
                NLog.LogManager.GetCurrentClassLogger().Error("Error: {0}", ex);
                throw;
            }
        }
    }
}
