using Microsoft.Extensions.Configuration;
using neoPuppeteerWS.Classes;
using Newtonsoft.Json;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

namespace neoPuppeteerWS
{
    public class neoPuppetter
    {
        private cLog _LOG = new cLog();
        private ViewPortOptions vOptions = new ViewPortOptions { Width = 1024, Height = 768 };
        private ScreenshotOptions sOptions = new ScreenshotOptions { FullPage = true };
        private TypeOptions tOptions = new TypeOptions { Delay = 50 };
        private PressOptions pOptions = new PressOptions { Delay = 50 };
        private NavigationOptions nOptions = new NavigationOptions { Timeout = 30000 };
        private WaitForSelectorOptions wOptions = new WaitForSelectorOptions { Timeout = 30000, };
        private SqlConnection _sqlConnection = new SqlConnection();
        private neoTools Tools = new neoTools();
        private readonly int sqlCommandTimeout = 60000000;
        private readonly int iSecondsTimeoutWaitInput = 60;
        private string myExeDir = new FileInfo(Assembly.GetEntryAssembly().Location).Directory.ToString();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public async Task Robot(neoRequestPuppeteer parameters)
        {
            /*----------------------------------------------------------------------------------------*/
            /*Init Chromium instance*/
            /*----------------------------------------------------------------------------------------*/
            using var browserFetcher = new BrowserFetcher();
            var revisionInfo = await browserFetcher.DownloadAsync();
            var options = new LaunchOptions { ExecutablePath = revisionInfo.ExecutablePath, Headless = parameters.HideNavigator };
            await using var browser = await Puppeteer.LaunchAsync(options);
            await using var page = await browser.NewPageAsync();
            await page.SetViewportAsync(vOptions);

            int IdThread = 0;
            int IdStep = 0;
            int IdRobot = 0;
            try
            {
                /*----------------------------------------------------------------------------------------*/
                /*Open database connection*/
                /*----------------------------------------------------------------------------------------*/
                this._sqlConnection = new SqlConnection(parameters.Configuration.GetConnectionString("local.database"));
                this._sqlConnection.Open();
                /*----------------------------------------------------------------------------------------*/

                /*----------------------------------------------------------------------------------------*/
                /*Init Thread in database for trace and control*/
                /*----------------------------------------------------------------------------------------*/
                IdThread = InsertThreadToDatabase(parameters);
                /*----------------------------------------------------------------------------------------*/

                /*----------------------------------------------------------------------------------------*/
                /*Process array data for required_input related to Thread*/
                /*----------------------------------------------------------------------------------------*/
                InsertThreadParametersToDatabase(parameters, IdThread);
                /*----------------------------------------------------------------------------------------*/

                /*----------------------------------------------------------------------------------------*/
                /*Handler for popup systems messages!*/
                /*----------------------------------------------------------------------------------------*/
                page.Dialog += new EventHandler<DialogEventArgs>(async (sender, args) =>
                {
                    string dialogMessage = args.Dialog.Message;
                    if (dialogMessage.Contains("No se puede autenticar estas credenciales")) {
                        await args.Dialog.Accept();
                        throw new Exception(dialogMessage);
                    };
                    Thread.Sleep(500);
                });
                /*----------------------------------------------------------------------------------------*/

                /*----------------------------------------------------------------------------------------*/
                /*Robot's steps execution*/
                /*----------------------------------------------------------------------------------------*/
                DataTable dtSteps = RobotSteps(parameters);
                foreach (DataRow row in dtSteps.Rows)
                {
                    DataTable dtThread = checkThreadOnline(IdThread);
                    List<neoValues> _nValues = new List<neoValues>();
                    bool _ignore_error = (int.Parse(row["ignore_error"].ToString()) == 1);
                    bool _insert_captured_values = false;
                    string _captured_values = "";
                    string msgUnavailable = "El thread " + IdThread.ToString() + " no está activo.  Reinicie el proceso.";
                    if (dtThread.Rows.Count == 0)
                    {
                        throw new Exception(msgUnavailable);
                    }
                    else
                    {
                        if (dtThread.Rows[0]["offline"].ToString() != "") { throw new Exception(msgUnavailable); }
                    }

                    IdStep = int.Parse(row["id"].ToString());
                    IdRobot = int.Parse(row["id_robot"].ToString());
                    string Description = row["description"].ToString();
                    /*----------------------------------------------------------------------------------------*/
                    /*Init Thread in database for trace and control*/
                    /*----------------------------------------------------------------------------------------*/
                    UpdateThreadToDatabase(IdThread, IdStep, IdRobot);
                    /*----------------------------------------------------------------------------------------*/

                    /*----------------------------------------------------------------------------------------*/
                    /*Validate parameter value, required and waited ones*/
                    /*----------------------------------------------------------------------------------------*/
                    string sSelector = row["selector"].ToString();
                    string sValue = row["parameter"].ToString();

                    int iRequireInput = 0;
                    int iWaitInput = 0;
                    int.TryParse(row["require_input"].ToString(), out iRequireInput);
                    int.TryParse(row["wait_input"].ToString(), out iWaitInput);

                    if (iRequireInput == 1)
                    {
                        /*Retrieve fixed value for step*/
                        DataTable dtParameter = ParameterStep(IdThread, IdStep);
                        sValue = dtParameter.Rows[0]["parameter"].ToString();
                    }
                    else
                    {
                        /*Wait control for user data*/
                        if (iWaitInput == 1)
                        {
                            int iWaitCount = 0;
                            Boolean bExit = false;
                            while (!bExit)
                            {
                                DataTable dtInput = InputStep(IdThread, IdStep);
                                if (dtInput.Rows.Count != 0)
                                {
                                    sValue = dtInput.Rows[0]["parameter"].ToString();
                                    iWaitCount = 0;
                                    bExit = true;
                                }
                                else
                                {
                                    Thread.Sleep(1000);
                                    iWaitCount += 1;
                                    bExit = (iWaitCount >= iSecondsTimeoutWaitInput);
                                }
                            }
                            if (iWaitCount != 0) { throw new Exception("No se ha provisto valor por parte del usuario.  60 segundos de tiempo de espera excedidos."); }
                        }
                    }

                    /*Type control for each parameter resolved*/
                    switch (row["code_type_evaluable"].ToString())
                    {
                        case "selector":
                        case "string":
                            break;
                        case "url":
                            if (!Tools.IsUrlValid(sValue)) { throw new Exception("El valor [" + sValue + "] no puede ser utilizado como [Url]"); }
                            break;
                        case "int":
                            int iValue = 0;
                            bool result = int.TryParse(sValue, out iValue);
                            if (!result) { throw new Exception("El valor [" + sValue + "] no puede ser utilizado como [Integer]"); }
                            break;
                    }
                    /*----------------------------------------------------------------------------------------*/

                    /*----------------------------------------------------------------------------------------*/
                    /*Validate execution mode for given step*/
                    /*----------------------------------------------------------------------------------------*/
                    try
                    {
                        switch (row["code_type_step"].ToString())
                        {
                            case "page.GoToAsync":
                                await page.GoToAsync(sValue, nOptions);
                                break;
                            case "page.TypeAsync":
                                if (sSelector != "") { await page.TypeAsync(sSelector, sValue, tOptions); }
                                break;
                            case "page.Keyboard.PressAsync":
                                await page.Keyboard.PressAsync(sValue, pOptions);
                                break;
                            case "page.WaitForTimeoutAsync":
                                await page.WaitForTimeoutAsync(int.Parse(sValue));
                                break;
                            case "page.ClickAsync.Right":
                                if (sSelector != "") { await page.ClickAsync(sSelector, new ClickOptions() { Button = MouseButton.Right }); }
                                break;
                            case "page.ClickAsync.Left":
                                if (sSelector != "") { await page.ClickAsync(sSelector, new ClickOptions() { Button = MouseButton.Left }); }
                                break;
                            case "page.ClickAsync.Middle":
                                if (sSelector != "") { await page.ClickAsync(sSelector, new ClickOptions() { Button = MouseButton.Middle }); }
                                break;
                            case "page.WaitForSelectorAsync":
                                if (sSelector != "") { await page.WaitForSelectorAsync(sSelector, wOptions); }
                                break;
                            case "page.XPathAsync.Click.Left":
                                if (sSelector != "")
                                {
                                    var a_obj = await page.XPathAsync(sSelector);
                                    if (!a_obj.GetType().IsArray) { throw new Exception("El selector " + sSelector + " no ha devuelto referencia a un objeto"); }
                                    await a_obj[0].ClickAsync();
                                }
                                break;
                            case "page.XPathAsync.Array.NavigateAndClose":
                                if (sSelector != "")
                                {
                                    var b_obj = await page.XPathAsync(sSelector);
                                    if (!b_obj.GetType().IsArray) { throw new Exception("El selector " + sSelector + " no ha devuelto referencia a un objeto"); }
                                    foreach (var o in b_obj)
                                    {
                                        var _ref = await o.GetPropertyAsync("href");
                                        string _t = (_ref.ToString().Split(":")[1] + ":" + _ref.ToString().Split(":")[2]);
                                        await using var _blank = await browser.NewPageAsync();
                                        await _blank.GoToAsync(_t);
                                        await _blank.WaitForTimeoutAsync(2000);
                                        await _blank.CloseAsync();
                                    }
                                }
                                break;
                            case "page.XPathAsync.WaitFor":
                                if (sSelector != "")
                                {
                                    var c_obj = await page.XPathAsync(sSelector);
                                    if (!c_obj.GetType().IsArray) { throw new Exception("El selector " + sSelector + " no ha devuelto referencia a un objeto"); }
                                }
                                break;
                            case "page.QuerySelectorAsync.GetValue":
                                if (sSelector != "")
                                {
                                    _insert_captured_values = true;
                                    var element = await page.QuerySelectorAsync(sSelector);
                                    var _value = await element.EvaluateFunctionAsync<string>("e => e.getAttribute('value')");
                                    _nValues.Add(new neoValues("value", _value.ToString()));
                                }
                                break;
                            case "page.QuerySelectorAsync.GetInnerTEXT":
                                if (sSelector != "")
                                {
                                    _insert_captured_values = true;
                                    await page.WaitForTimeoutAsync(2000);
                                    var element = await page.QuerySelectorAsync(sSelector);
                                    var _value = await element.EvaluateFunctionAsync<string>("e => e.innerTEXT()");
                                    _nValues.Add(new neoValues("innerTEXT", _value.ToString()));
                                }
                                break;
                            case "page.XPathAsync.GetValue":
                                if (sSelector != "")
                                {
                                    _insert_captured_values = true;
                                    var d_obj = await page.XPathAsync(sSelector);
                                    if (!d_obj.GetType().IsArray) { throw new Exception("El selector " + sSelector + " no ha devuelto referencia a un objeto"); }
                                    foreach (var o in d_obj)
                                    {
                                        var _ref = await o.GetPropertyAsync("value");
                                        _nValues.Add(new neoValues("value", _ref.ToString()));
                                    }
                                    var jsonString = JsonConvert.SerializeObject(_nValues);
                                    _captured_values = jsonString.ToString();
                                }
                                break;
                            case "page.XPathAsync.GetInnerTEXT":
                                if (sSelector != "")
                                {
                                    _insert_captured_values = true;
                                    await page.WaitForTimeoutAsync(2000);
                                    var d_obj = await page.XPathAsync(sSelector);
                                    if (!d_obj.GetType().IsArray) { throw new Exception("El selector " + sSelector + " no ha devuelto referencia a un objeto"); }
                                    foreach (var o in d_obj)
                                    {
                                        var _ref = await o.EvaluateFunctionAsync<string>("e => e.innerText");
                                        _nValues.Add(new neoValues("innerTEXT", _ref.ToString()));
                                    }
                                    var jsonString = JsonConvert.SerializeObject(_nValues);
                                    _captured_values = jsonString.ToString();
                                }
                                break;
                        }
                        if (_insert_captured_values) {
                            neoCapturedValues setValues = new neoCapturedValues();
                            setValues.Code = sSelector;
                            setValues.Description = Description;
                            setValues.Id_robot = IdRobot;
                            setValues.Id_step = IdStep;
                            setValues.Raw_data = _captured_values;
                            this.InsertCapturedValuesToDatabase(setValues);
                        }
                    }
                    catch (WaitTaskTimeoutException rex) {
                        if (!_ignore_error) { 
                            throw new WaitTaskTimeoutException(("No se ha podido seleccionar el objeto " + sSelector), rex); 
                        }
                    }
                    catch (Exception rex)
                    {
                        if (!_ignore_error)
                        {
                            throw new Exception(("No se ha podido seleccionar el objeto " + sSelector), rex);
                        }
                    }
                    /*----------------------------------------------------------------------------------------*/

                    /*----------------------------------------------------------------------------------------*/
                    /*Evaluate if a screenshot is required*/
                    /*----------------------------------------------------------------------------------------*/
                    if (int.Parse(row["take_screenshot"].ToString()) != 0)
                    {
                        await page.WaitForTimeoutAsync(1000);
                        UpdateScreenshotToDatabase(IdThread, ("data:image/png;base64," + await page.ScreenshotBase64Async(sOptions).ConfigureAwait(false)), IdRobot);
                    }
                    /*----------------------------------------------------------------------------------------*/
                }
                /*----------------------------------------------------------------------------------------*/
            }
            catch (WaitTaskTimeoutException rex)
            {
                ErrorThreadToDatabase(IdThread, rex, IdRobot);
            }
            catch (Exception rex)
            {
                ErrorThreadToDatabase(IdThread, rex, IdRobot);
            }
            finally {
                await page.CloseAsync();
                await browser.CloseAsync();
                /*----------------------------------------------------------------------------------------*/
                /*End Thread in database for trace and control*/
                /*----------------------------------------------------------------------------------------*/
                EndThreadToDatabase(IdThread, IdRobot);
                /*----------------------------------------------------------------------------------------*/

                /*----------------------------------------------------------------------------------------*/
                /*Close database connection*/
                /*----------------------------------------------------------------------------------------*/
                if (_sqlConnection != null) { _sqlConnection.Close(); }
                /*----------------------------------------------------------------------------------------*/
            }
        }


        private DataTable ParameterStep(int IdThread, int IdStep)
        {
            DataTable dtTable = new DataTable("parameters");
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "SELECT * FROM mod_rpa_threads_parameters WHERE id_thread=@id_thread AND id_step=@id_step";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id_thread", String.Format("{0}", IdThread));
                sqlCommand.Parameters.AddWithValue("@id_step", String.Format("{0}", IdStep));
                dtTable.Load(sqlCommand.ExecuteReader());
                return dtTable;
            }
            catch (Exception rex)
            {
                return dtTable;
            }
        }
        private DataTable InputStep(int IdThread, int IdStep)
        {
            DataTable dtTable = new DataTable("inputs");
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "SELECT * FROM mod_rpa_threads_inputs WHERE id_thread=@id_thread AND id_step=@id_step";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id_thread", String.Format("{0}", IdThread));
                sqlCommand.Parameters.AddWithValue("@id_step", String.Format("{0}", IdStep));
                dtTable.Load(sqlCommand.ExecuteReader());
                return dtTable;
            }
            catch (Exception rex)
            {
                return dtTable;
            }
        }
        private DataTable RobotSteps(neoRequestPuppeteer parameters)
        {
            DataTable dtTable = new DataTable("steps");
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "SELECT * FROM mod_rpa_vw_steps WHERE id_robot=@id_robot ORDER BY priority ASC";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id_robot", String.Format("{0}", parameters.Id));
                dtTable.Load(sqlCommand.ExecuteReader());
                return dtTable;
            }
            catch (Exception rex)
            {
                return dtTable;
            }
        }

        private int InsertCapturedValuesToDatabase(neoCapturedValues parameters)
        {
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "INSERT INTO mod_rpa_captured_values (code,description,created,verified,fum,id_robot,id_step,raw_data) ";
                sqlCommand.CommandText += " VALUES (@code,@description,getdate(),getdate(),getdate(),@id_robot,@id_step,@raw_data);SELECT SCOPE_IDENTITY() ";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@code", String.Format("{0}", parameters.Code));
                sqlCommand.Parameters.AddWithValue("@description", String.Format("{0}", parameters.Description));
                sqlCommand.Parameters.AddWithValue("@id_robot", String.Format("{0}", parameters.Id_robot));
                sqlCommand.Parameters.AddWithValue("@id_step", String.Format("{0}", parameters.Id_step));
                sqlCommand.Parameters.AddWithValue("@raw_data", String.Format("{0}", parameters.Raw_data));
                return Convert.ToInt32(sqlCommand.ExecuteScalar());
            }
            catch (Exception rex)
            {
                return 0;
            }
        }
        private Boolean InsertThreadParametersToDatabase(neoRequestPuppeteer parameters, int IdThread)
        {
            try
            {
                if (parameters.Data != null)
                {
                    SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                    sqlCommand.CommandTimeout = sqlCommandTimeout;
                    sqlCommand.CommandText = "INSERT INTO mod_rpa_threads_parameters (code,description,created,verified,fum,id_thread,id_step,parameter) ";
                    sqlCommand.CommandText += " VALUES (@code,@description,getdate(),getdate(),getdate(),@id_thread,@id_step,@parameter);";

                    foreach (neoDataThread item in parameters.Data)
                    {
                        sqlCommand.Parameters.Clear();
                        sqlCommand.Parameters.AddWithValue("@code", String.Format("{0}", "DPI"));
                        sqlCommand.Parameters.AddWithValue("@description", String.Format("{0}", item.Description));
                        sqlCommand.Parameters.AddWithValue("@id_thread", String.Format("{0}", IdThread));
                        sqlCommand.Parameters.AddWithValue("@id_step", String.Format("{0}", item.Id_step));
                        sqlCommand.Parameters.AddWithValue("@parameter", String.Format("{0}", item.Parameter));
                        sqlCommand.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception rex)
            {
                return false;
            }
        }
        private int InsertThreadToDatabase(neoRequestPuppeteer parameters)
        {
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "INSERT INTO mod_rpa_threads (code,description,created,verified,fum,init,id_robot,id_profile,wait_input) ";
                sqlCommand.CommandText += " VALUES (@code,@description,getdate(),getdate(),getdate(),getdate(),@id_robot,@id_profile,0);SELECT SCOPE_IDENTITY() ";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@code", String.Format("{0}", parameters.Thread.ManagedThreadId.ToString()));
                sqlCommand.Parameters.AddWithValue("@description", String.Format("{0}", parameters.Thread.Name));
                sqlCommand.Parameters.AddWithValue("@id_robot", String.Format("{0}", parameters.Id));
                sqlCommand.Parameters.AddWithValue("@id_profile", String.Format("{0}", parameters.Id_profile));
                int _id_thread = Convert.ToInt32(sqlCommand.ExecuteScalar());

                sqlCommand.CommandText = "UPDATE mod_rpa_robots SET init_last_thread=getdate(),end_last_thread=null,id_last_thread=@id_last_thread,last_error=null WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", parameters.Id));
                sqlCommand.Parameters.AddWithValue("@id_last_thread", String.Format("{0}", _id_thread));
                sqlCommand.ExecuteNonQuery();

                return _id_thread;
            }
            catch (Exception rex)
            {
                return 0;
            }
        }
        private Boolean UpdateThreadToDatabase(int IdThread, int IdStep, int IdRobot)
        {
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "UPDATE mod_rpa_threads SET fum=getdate(),id_step=@id_step,fum_step=getdate() WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", IdThread));
                sqlCommand.Parameters.AddWithValue("@id_step", String.Format("{0}", IdStep));
                sqlCommand.ExecuteNonQuery();
                return true;
            }
            catch (Exception rex)
            {
                return false;
            }
        }
        private Boolean UpdateScreenshotToDatabase(int IdThread, string Base64, int IdRobot)
        {
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "UPDATE mod_rpa_threads SET fum=getdate(),screenshot=@screenshot,fum_screenshot=getdate() WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", IdThread));
                sqlCommand.Parameters.AddWithValue("@screenshot", String.Format("{0}", Base64));
                sqlCommand.ExecuteNonQuery();
                return true;
            }
            catch (Exception rex)
            {
                return false;
            }
        }
        private DataTable checkThreadOnline(int IdThread)
        {
            DataTable dtTable = new DataTable("thread");
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "SELECT * FROM mod_rpa_threads WHERE id=@id_thread";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id_thread", String.Format("{0}", IdThread));
                dtTable.Load(sqlCommand.ExecuteReader());
                return dtTable;
            }
            catch (Exception rex)
            {
                return dtTable;
            }
        }

        private Boolean ErrorThreadToDatabase(int IdThread, Exception err, int IdRobot)
        {
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "UPDATE mod_rpa_threads SET fum=getdate(),fum_error=getdate(),error=@error WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", IdThread));
                sqlCommand.Parameters.AddWithValue("@error", String.Format("{0}", err.ToString()));
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "UPDATE mod_rpa_robots SET last_error=@error WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", IdRobot));
                sqlCommand.Parameters.AddWithValue("@error", String.Format("{0}", err.ToString()));
                sqlCommand.ExecuteNonQuery();
                return true;
            }
            catch (Exception rex)
            {
                return false;
            }
        }
        private Boolean EndThreadToDatabase(int IdThread, int IdRobot)
        {
            try
            {
                SqlCommand sqlCommand = new SqlCommand("", _sqlConnection);
                sqlCommand.CommandTimeout = sqlCommandTimeout;
                sqlCommand.CommandText = "UPDATE mod_rpa_threads SET fum=getdate(),offline=getdate(),[end]=getdate(),screenshot=null,error=null,fum_error=null WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", IdThread));
                sqlCommand.ExecuteNonQuery();

                sqlCommand.CommandText = "UPDATE mod_rpa_robots SET end_last_thread=getdate() WHERE id=@id";
                sqlCommand.Parameters.Clear();
                sqlCommand.Parameters.AddWithValue("@id", String.Format("{0}", IdRobot));
                sqlCommand.ExecuteNonQuery();

                return true;
            }
            catch (Exception rex)
            {
                return false;
            }
        }
    }
}
