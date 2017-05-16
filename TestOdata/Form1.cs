using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestOdata
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var username = "chris.azer@schneider-electric.com";
            var password = "Chr!saz3r";
            var tagname = "WonderPlant.WONDERPLANT.FillerAvailability.AvailabilityPercent";
            double hours = 1;
            WebRequest request = WebRequest.Create("https://online.wonderware.com/s/u1vnn6/apis/Historian/v1/AnalogSummary?$filter=FQN+eq+" +
            "'"+tagname+"'+and+StartDateTime+ge+datetimeoffset'"+System.DateTime.Now.AddHours(-1).ToString("s")+"'+and+EndDateTime+le+datetimeoffset'"+ System.DateTime.Now.ToString("s") + "'&Resolution="+(1*60*60*1000).ToString());
            request.ContentType = "application/json; charset=utf-8";
            String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            request.Headers.Add("Authorization", "Basic " + encoded);
            WebResponse response = request.GetResponse();
            var rawJson = new StreamReader(response.GetResponseStream()).ReadToEnd();

            JObject json = JObject.Parse(rawJson);
            var average = json["value"][0]["Average"];
        }
    }
}
