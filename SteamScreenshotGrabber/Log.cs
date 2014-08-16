using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamScreenshotGrabber
{
    public delegate void StringDelegate(string str);

    public class Log
    {
        // Singleton implementation from http://msdn.microsoft.com/en-us/library/ff650316.aspx
        private static Log instance = new Log();
        private Log() { }
        public static Log Instance { get { return instance; } }

        public event StringDelegate LogEvent;

        public void LogMsg(string msg)
        {
            if (LogEvent != null)
            {
                LogEvent(msg);
            }
        }
    }
}
