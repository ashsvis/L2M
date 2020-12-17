using System;
using System.ServiceProcess;

namespace L2M
{
    partial class WinService : ServiceBase
    {

        public WinService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {


            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ex.Message,
                    System.Diagnostics.EventLogEntryType.Information);
            }
        }

        protected override void OnStop()
        {

        }
    }
}
