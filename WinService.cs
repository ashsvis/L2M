using System;
using System.ServiceProcess;

namespace L2M
{
    partial class WinService : ServiceBase
    {
        private WcfEventService _wcf;

        public WinService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _wcf = WcfEventService.EventService;
                _wcf.Start();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ex.Message,
                    System.Diagnostics.EventLogEntryType.Information);
            }
        }

        protected override void OnStop()
        {
            if (_wcf != null)
            {
                _wcf.Stop();
            }
        }
    }
}
