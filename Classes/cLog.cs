using System;
using System.IO;
using System.Reflection;

namespace neoPuppeteerWS.Classes
{
    public class cLog
    {
        private bool _toDisk = true;

        public bool toDisk
        {
            get => _toDisk;
            set { _toDisk = value; }
        }

        public void LogWriter(string logMessage, string sDirLog)
        {
            LogWrite(logMessage, sDirLog);
        }
        public void LogWrite(string logMessage, string sDirLog)
        {
            try
            {
                using (StreamWriter w = File.AppendText(sDirLog + "\\LOG-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt"))
                {
                    Log(logMessage, w);
                }
            }
            catch (Exception ex)
            {
            }
        }
        public void Log(string logMessage, TextWriter txtWriter)
        {
            try
            {
                logMessage += ("," + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString());
                if (this._toDisk) { txtWriter.WriteLine(logMessage); }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
