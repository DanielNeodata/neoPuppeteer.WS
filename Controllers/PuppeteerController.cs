using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;

namespace neoPuppeteerWS.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PuppeteerController : ControllerBase
    {
        private readonly IConfiguration configuration;
        public PuppeteerController(IConfiguration config)
        {
            configuration = config;
        }

        [HttpGet("ExecuteInfo")]
        public IActionResult GetExecuteInfo()
        {
            try
            {
                string _response = "";
                List<object> _rootNodes = new List<object>();
                _rootNodes.Add(new { Node = "Id", Type = "integer", Info = "Robot id for identify procedures", Constraints = "Allowed values: any integer value" });
                _rootNodes.Add(new { Node = "Id_user", Type = "integer", Info = "User id for identify thread ownership", Constraints = "Allowed values: any integer value" });
                object _complex = new { Nodes = _rootNodes };
                _response = JsonConvert.SerializeObject(_complex);
                neoResponse _ok = new neoResponse("OK", "ExecuteInfo", "Sin errores");
                return Ok(_ok);
            }
            catch (Exception ex)
            {
                neoResponse _error = new neoResponse("ERROR", "ExecuteInfo", ex.Message);
                return BadRequest(_error);
            }
        }

        [HttpPost("StartProcess")]
        public IActionResult PostStartProcess([FromBody] neoRequestPuppeteer payload)
        {
            neoResponse _rsp;
            try
            {
                string _msg_errors = "Sin errores";
                if (!payload.ValidateId(payload.Id.ToString())) { throw new Exception("Valor inválido provisto en [id], debe ser numérico"); }
                if (!payload.ValidateId_profile(payload.Id_profile.ToString())) { throw new Exception("Valor inválido provisto en [id_user], debe ser numérico"); }
                if (!payload.ValidateHideNavigator(payload.HideNavigator.ToString())) { throw new Exception("Valor inválido provisto en [HideNavigator], debe ser boolean"); }

                CancellationTokenSource cts = new CancellationTokenSource();
                payload.Thread = new Thread(new ParameterizedThreadStart(ThreadProc)); 
                payload.Token = cts.Token;
                payload.Configuration = configuration;
                payload.Thread.Start(payload);
                _rsp = new neoResponse("OK", payload.Id.ToString(), _msg_errors);
                payload = null;
                return Ok(_rsp);
            }
            catch (Exception ex)
            {
                _rsp = new neoResponse("ERROR", payload.Id.ToString(), ex.Message);
                payload = null;
                return BadRequest(_rsp);
            }
        }

        public static void ThreadProc(object parameters)
        {
            neoPuppetter OBJ = new neoPuppetter();
            _ = OBJ.Robot((neoRequestPuppeteer)parameters);
            parameters = null;
            OBJ = null;
        }
    }
}
