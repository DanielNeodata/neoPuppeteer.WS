using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

namespace neoPuppeteerWS
{
    public class neoCapturedValues
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public int Id_robot { get; set; }
        public int Id_step { get; set; }
        public string Raw_data { get; set; }
        public Boolean ValidateId_robot(string sValue)
        {
            int iValue = 0;
            return int.TryParse(sValue, out iValue);
        }
        public Boolean ValidateId_step(string sValue)
        {
            int iValue = 0;
            return int.TryParse(sValue, out iValue);
        }
    }
}
