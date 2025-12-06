using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

namespace neoPuppeteerWS
{
    public class neoRequestPuppeteer
    {
        public int Id { get; set; }
        public int Id_profile { get; set; }
        public bool HideNavigator { get; set; }
        public neoDataThread[] Data { get; set; }
        public IConfiguration Configuration{ get; set; }
        public CancellationToken Token { get; set; }
        public Thread Thread { get; set; }
        public Boolean ValidateId(string sValue)
        {
            int iValue = 0;
            return int.TryParse(sValue, out iValue);
        }
        public Boolean ValidateId_profile(string sValue)
        {
            int iValue = 0;
            return int.TryParse(sValue, out iValue);
        }
        public Boolean ValidateHideNavigator(string sValue)
        {
            return (sValue!="");
        }
    }
}
