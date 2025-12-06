using Microsoft.Extensions.Configuration;
using PuppeteerSharp;
using System;
using System.Threading;

namespace neoPuppeteerWS
{
    public class neoValues
    {
        public neoValues(string name, string raw_data)
        {
            Name= name;
            Raw_data = raw_data;
        }

        public string Name { get; set; }
        public string Raw_data { get; set; }
    }
}
