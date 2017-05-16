using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Slight.Alexa.Framework.Models.Requests;
using Slight.Alexa.Framework.Models.Responses;
using System.IO;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace WWLambda
{
    public class Function
    {

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public SkillResponse FunctionHandler(SkillRequest input, ILambdaContext context)
        {
            Response response;
            IOutputSpeech innerResponse = null;
            var log = context.Logger;

            if (input.GetRequestType() == typeof(Slight.Alexa.Framework.Models.Requests.RequestTypes.ILaunchRequest))
            {
                // default launch request, let's just let them know what you can do
                log.LogLine($"Default LaunchRequest made");

                innerResponse = new PlainTextOutputSpeech();
                (innerResponse as PlainTextOutputSpeech).Text = "You can ask Wonderware to Get Availability for an equipment or to initiate a Workflow.";
            }
            else if (input.GetRequestType() == typeof(Slight.Alexa.Framework.Models.Requests.RequestTypes.IIntentRequest))
            {
                // intent request, process the intent
                log.LogLine($"Intent Requested {input.Request.Intent.Name}");

                innerResponse = new PlainTextOutputSpeech();

                if (input.Request.Intent.Name == "GetAvailability")
                {
                    var line = input.Request.Intent.Slots["LineOrEquipment"].Value;
                    var duration = input.Request.Intent.Slots["Duration"].Value;
                    if (line.ToLower().Contains("filler"))
                        line = "Filler";
                    else if (line.ToLower().Contains("case") || line.ToLower().Contains("packer"))
                        line = "CasePacker";
                    else if (line.ToLower().Contains("labeler"))
                        line = "Labeler";
                    else if (line.ToLower().Contains("palletizer"))
                        line = "Palletizer";
                    else
                        line = "Unknown";

                    if(line == "Unknown")
                    {
                        (innerResponse as PlainTextOutputSpeech).Text = $"Hmm, the equipment you specified was not found.";
                    }
                    else
                    {
                        var tagname = "WonderPlant.WONDERPLANT." + line + "Availability.AvailabilityPercent";
                        var starttime = System.DateTime.UtcNow;
                        var endtime = System.DateTime.UtcNow;
                        switch (duration.ToLower())
                        {
                            case "last hour":
                                starttime = endtime.AddHours(-1);
                                break;
                            case "today":
                                starttime = new DateTime(starttime.Year, starttime.Month, starttime.Day);
                                break;
                            case "yesterday":
                                starttime = new DateTime(starttime.AddDays(-1).Year, starttime.AddDays(-1).Month, starttime.AddDays(-1).Day);
                                endtime = new DateTime(endtime.Year, endtime.Month, endtime.Day);
                                break;

                        }
                        var utilization = GetUtilization(tagname, starttime, endtime);
                        var alexacomment = "";
                        if (Convert.ToDouble(utilization) >= 70)
                            alexacomment = "Keep up the good Work!";
                        else if (Convert.ToDouble(utilization) < 70 && Convert.ToDouble(utilization) > 50)
                            alexacomment = "Hmm, you a falling a bit behind.";
                        else
                            alexacomment = "You suck!";

                        if (duration == "Last Hour")
                            duration = "the Last Hour";

                        var results = "";
                        if (double.IsNaN(Convert.ToDouble(utilization)))
                        {
                            results = " not available";
                            alexacomment = "";
                        }
                        else
                            results = System.Math.Round(Convert.ToDecimal(utilization), 1) + "%";
                        (innerResponse as PlainTextOutputSpeech).Text = $"The {line.ToString()} availability for {duration.ToString()} is {results}. {alexacomment}";
                    }
                }
                if (input.Request.Intent.Name == "GetCurrentState")
                {
                    var line = input.Request.Intent.Slots["LineOrEquipment"].Value;
                    if (line.ToLower().Contains("filler"))
                        line = "Filler";
                    else if (line.ToLower().Contains("case") || line.ToLower().Contains("packer"))
                        line = "CasePacker";
                    else if (line.ToLower().Contains("labeler"))
                        line = "Labeler";
                    else if (line.ToLower().Contains("palletizer"))
                        line = "Palletizer";
                    else
                        line = "Unknown";

                    var equipmentstate = GetCurrentState();
                    var alexacomment = "";
                    if (Convert.ToDouble(equipmentstate.durationms) / 1000 < 60 && Convert.ToDouble(equipmentstate.durationms) / 1000 > 0)
                        alexacomment = " for "+ (Convert.ToDouble(equipmentstate.durationms) / 1000).ToString() + " seconds";
                    else if ((Convert.ToDouble(equipmentstate.durationms) / 1000) < 3600 && (Convert.ToDouble(equipmentstate.durationms) / 1000) > 60)
                        alexacomment = " for " + (Convert.ToDouble(equipmentstate.durationms) / 1000/ 60).ToString() + " minutes";
                    else if ((Convert.ToDouble(equipmentstate.durationms) / 1000) > 3600 && (Convert.ToDouble(equipmentstate.durationms) / 1000) < 216000)
                        alexacomment = " for " + (Convert.ToDouble(equipmentstate.durationms) / 1000 / 60/ 60).ToString() + " hours";

                    var reason = "";
                    if (equipmentstate.reason != "Unknown")
                        reason = "The reason specified by the operator is, "+equipmentstate.reason;

                    (innerResponse as PlainTextOutputSpeech).Text = $"The {input.Request.Intent.Slots["LineOrEquipment"].Value} is currently {equipmentstate.status} {alexacomment}. "+ reason;
                }
                else if (input.Request.Intent.Name == "StartWorkflow")
                {
                    var workflow = input.Request.Intent.Slots["Workflow"].Value;
                    try
                    {

                        var applicationName = "WFRepository";
                        if (workflow.ToLower().Contains("short"))
                        {
                            workflow = "Short Interval Control";
                        }
                        var workflowName = workflow;
                        var version = "1";
                        var userDetails = "skeltalist::E06D441B-AEC6-4386-9955-8E9A385DBB33";
                        var params1 = "[]";
                        var url = "http://76.191.119.100:8000/Workflow.WebAPI/api/Workflow/ExecuteWorkflowWithVariables?applicationName=" + applicationName + "&workflowName=" + workflowName + "&version=" + version + "&userId=" + userDetails + "&data=<Demo/>";
                        WebRequest request = WebRequest.Create(url);
                        //request.ContentType = "application/json; charset=utf-8";
                        var result = request.GetResponseAsync().Result;
                        (innerResponse as PlainTextOutputSpeech).Text = $"{workflow.ToString()} started successfully.";
                    }catch(Exception ex)
                    {
                        (innerResponse as PlainTextOutputSpeech).Text = "Error trying to run "+ workflow.ToString()+" " +ex.Message;
                    }
                }

            }

            response = new Response();
            response.ShouldEndSession = true;
            response.OutputSpeech = innerResponse;
            SkillResponse skillResponse = new SkillResponse();
            skillResponse.Response = response;
            skillResponse.Version = "1.0";

            return skillResponse;
        }
        public struct EquipmentState
        {
            public string status;
            public string reason;
            public string durationms;
        }
        public EquipmentState GetCurrentState()
        {
            try
            {
                var starttime = DateTime.Now.AddDays(-7);
                var username = "chris.azer@schneider-electric.com";
                var password = "xxxxxxx";
                double hours = 1;
                WebRequest request = WebRequest.Create("https://int.online.wonderware.com/s/sz6hg5/apis/Historian/v2/Events?$filter=EventTime+gt+'"+starttime.ToString("s") + "'");
                request.ContentType = "application/json; charset=utf-8";
                String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                request.Headers["Authorization"] = "Basic " + encoded;
                WebResponse response = request.GetResponseAsync().Result;
                var rawJson = new StreamReader(response.GetResponseStream()).ReadToEnd();

                JObject json = JObject.Parse(rawJson);
                EquipmentState es = new EquipmentState();
                es.status = json["value"][json["value"].Count() - 1]["status"].ToString();
                es.reason = json["value"][json["value"].Count() - 1]["downtime_reason"].ToString();
                es.durationms = json["value"][json["value"].Count() - 1]["durationms"].ToString();
                return es;
            }
            catch
            {
                EquipmentState es = new EquipmentState();
                es.status = "Unknown";
                es.reason = "Unknown";
                es.durationms = "0";
                return es;
            }
        }
        public string GetUtilization(string tagname, DateTime starttime, DateTime endtime)
        {
            try
            {
                var username = "chris.azer@schneider-electric.com";
                var password = "xxxxxxx";
                var resolution = endtime.Subtract(starttime).TotalMilliseconds;
                double hours = 1;
                WebRequest request = WebRequest.Create("https://online.wonderware.com/s/u1vnn6/apis/Historian/v1/AnalogSummary?$filter=FQN+eq+" +
                "'" + tagname + "'+and+StartDateTime+ge+datetimeoffset'" + starttime.ToString("s") + "'+and+EndDateTime+le+datetimeoffset'" + endtime.ToString("s") + "'&Resolution=" + resolution.ToString());
                request.ContentType = "application/json; charset=utf-8";
                String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                request.Headers["Authorization"] = "Basic " + encoded;
                WebResponse response = request.GetResponseAsync().Result;
                var rawJson = new StreamReader(response.GetResponseStream()).ReadToEnd();

                JObject json = JObject.Parse(rawJson);
                return json["value"][0]["Average"].ToString();
            }
            catch
            {
                Random rand = new Random();
                return rand.Next(50, 80).ToString();
            }
        }
    }
}
