using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace neoPuppeteerWS
{
    public class neoResponse
    {
        public neoResponse(string _status, string _key, string _message)
        {
            this.status = _status;
            this.key = _key;
            this.message = _message;
        }
        public string status { get; set; }
        public string key { get; set; }
        public string message { get; set; }
    }
}
