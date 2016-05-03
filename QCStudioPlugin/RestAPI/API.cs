﻿/*
* Mark Babayev (https://github.com/mirik123) - REST API
* C# Wrapper for Restful API for Managing QuantConnect Connection
* Inspired by the QuantConnect Rest API.
*/

/**********************************************************
* USING NAMESPACES
**********************************************************/
using Newtonsoft.Json;
using QuantConnect.RestAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuantConnect.RestAPI
{
    /// <summary>
    /// Primary API Class
    /// </summary>
    public class API
    {

        /******************************************************** 
        * CLASS VARIABLES
        *********************************************************/
        /// HTTPS Endpoint:
        private string baseUrl = "https://www.quantconnect.com/api/v1/";
        /// Encoded token username password:
        private string _accessToken = "";

        const string message = "Error retrieving response.  Check inner details for more info.";

        /******************************************************** 
        * CLASS CONSTRUCTOR
        *********************************************************/
        /// <summary>
        /// Initialise API Manager:
        /// </summary>
        public API()
        {
        }

        /// <summary>
        /// Execute a authenticated call:
        /// </summary>
        private async Task<T> Execute<T>(string relativeUrl, object data = null) where T : PacketBase, new()
        {
            if (!IsAuthenticated)
            {                
                throw new ApplicationException(message, new Exception("Authentication token doesn't exist."));
            }

            T res = null;
            HttpWebRequest request = WebRequest.Create(baseUrl + relativeUrl) as HttpWebRequest;
            request.Headers.Add(HttpRequestHeader.Authorization, "Basic " + _accessToken);
            request.Accept = "application/json; charset=utf-8";
            request.ContentType = "application/json; charset=utf-8";            
            request.Method = "POST";
            request.KeepAlive = true;
            request.AllowAutoRedirect = true;

            if (data != null)
            {
                string jsonreq = JsonConvert.SerializeObject(data);
                var buffer = Encoding.UTF8.GetBytes(jsonreq);

                request.ContentLength = buffer.Length;
                using (Stream strinput = request.GetRequestStream())
                    strinput.Write(buffer, 0, buffer.Length);
            }

            using (var response = await request.GetResponseAsync())
                using(Stream stroutput = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stroutput, Encoding.UTF8))
                    {
                        string jsonresp = reader.ReadToEnd();
                        res = JsonConvert.DeserializeObject<T>(jsonresp);
                        if (res.Errors == null || res.Errors.Count > 0)
                        {
                            var ex = res.Errors.Aggregate("", (x, y) => x + Environment.NewLine + y);
                            throw new Exception(ex);
                        }
                    }

            return res;
        }

        public bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(_accessToken); }
        }

        public void RemoveAuthentication()
        {
            lock (_accessToken)
            {
                _accessToken = "";
            }
        }

        /// <summary>
        /// Test these authentication details against the server:
        /// </summary>
        /// <param name="email">User email from quantconnect account</param>
        /// <param name="password">Quantconnect user password</param>
        public async Task Authenticate(string email, string password)
        {
            if(string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                lock (_accessToken)
                {
                    _accessToken = "";
                }

                throw new Exception("Username or Password are empty");
            }

            try
            {
                lock (_accessToken)
                {
                    _accessToken = Base64Encode(email + ":" + password);
                }

                await Execute<PacketProject>("projects/read");
            }
            catch(Exception ex)
            {
                lock (_accessToken)
                {
                    _accessToken = "";
                }

                throw ex;
            }
        }

        /// <summary>
        /// Create a new project in your QC account.
        /// </summary>
        /// <param name="projectName">Name of the new project</param>
        /// <returns>Project Id.</returns>
        public Task<PacketCreateProject> ProjectCreate(string name)
        {
            return Execute<PacketCreateProject>("projects/create", new { projectName = name });
        }


        /// <summary>
        /// Update a project with a list of C# files:
        /// </summary>
        public Task<PacketBase> ProjectUpdate(int id, List<QCFile> filesData)
        {
            return Execute<PacketBase>("projects/update", new { projectId = id, files = filesData });
        }

        /// <summary>
        /// Return a list of QuantConnect Projects
        /// </summary>
        /// <returns></returns>
        public Task<PacketProject> ProjectList()
        {
            return Execute<PacketProject>("projects/read");
        }


        /// <summary>
        /// Get a list of project files in this project
        /// </summary>
        public Task<PacketProjectFiles> ProjectFiles(int id)
        {
            return Execute<PacketProjectFiles>("projects/read", new { projectId = id });
        }


        /// <summary>
        /// Delete a project by id:
        /// </summary>
        public Task<PacketBase> ProjectDelete(int id)
        {
            return Execute<PacketBase>("projects/delete", new { projectId = id });
        }


        /// <summary>
        /// Send a compile request:
        /// </summary>
        public Task<PacketCompile> Compile(int id)
        {
            return Execute<PacketCompile>("compiler/create", new { projectId = id });
        }


        /// <summary>
        /// Submit a compile and project id for backtesting.
        /// </summary>
        public Task<PacketBacktest> Backtest(int projectId, string compileId, string backtestName)
        {
            return Execute<PacketBacktest>("backtests/create", new
            {
                projectId = projectId,
                compileId = compileId,
                backtestName = backtestName
            });
        }


        /// <summary>
        /// Read this backtest result back:
        /// </summary>
        public Task<PacketBacktestResult> BacktestResults(string backtestId)
        {
            return Execute<PacketBacktestResult>("backtests/read", new { backtestId = backtestId });
        }


        /// <summary>
        /// Delete the given backtest Id
        /// </summary>
        /// <param name="backtestId">Id we want to delete</param>
        /// <returns>Packet success, fail or errors</returns>
        public Task<PacketBase> BacktestDelete(string backtestId)
        {
            return Execute<PacketBase>("backtests/delete", new { backtestId = backtestId });
        }


        /// <summary>
        /// Get a list of backtest results for this project:
        /// </summary>
        public Task<PacketBacktestList> BacktestList(int projectId)
        {
            return Execute<PacketBacktestList>("backtests/list", new { projectId = projectId });
        }


        /// <summary>
        /// B64 Encoder 
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        private string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// B64 Decoder:
        /// </summary>
        private string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}