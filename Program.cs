using System;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CsvHelper;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace VehicleSimulator
{

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("config.json");
                IConfigurationRoot config = builder.Build();
                RouteTracker routeTracker = new RouteTracker(config["route-file"], config["endpoint"]);
                var autoEvent = new AutoResetEvent(false);
                var startTimeSpan = TimeSpan.Zero;
                var periodTimeSpan = TimeSpan.FromSeconds(1);
                var timer = new System.Threading.Timer(async (e) =>
                 {
                     await routeTracker.PostData(e);
                 }, autoEvent, startTimeSpan, periodTimeSpan);
                autoEvent.WaitOne();
                timer.Dispose();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class Route
    {
        public string type { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }

        [CsvHelper.Configuration.Attributes.Name("speedlimit (km/h)")]
        public string speedlimit { get; set; }

        [CsvHelper.Configuration.Attributes.Name("altitude (m)")]
        public string altitude { get; set; }
        public string course { get; set; }
        [CsvHelper.Configuration.Attributes.Name("slope (%)")]
        public string slope { get; set; }

        [CsvHelper.Configuration.Attributes.Name("distance (km)")]
        public string distance { get; set; }
        [CsvHelper.Configuration.Attributes.Name("distance_interval (m)")]
        public string distance_interval { get; set; }
        public string name { get; set; }
        public string desc { get; set; }
    }

    public class RouteTracker
    {
        private List<Route> _routes;
        private int _currentRouteIndex;
        private float _currentSpeed = 0F;
        private float _accelerationKph = 5.4F;
        private float _brakeKph = 9.0F;
        private string _url;

        public RouteTracker(string csvFile, string url)
        {
            TextReader tw = new StreamReader(csvFile);
            _url = url;
            var csvReader = new CsvReader(tw, System.Globalization.CultureInfo.CurrentCulture);
            List<Route> routesinCSV = csvReader.GetRecords<Route>().ToList();
            _routes = routesinCSV.FindAll(e => e.type == "T");
            _currentRouteIndex = 0;
        }

        public async Task PostData(Object stateInfo)
        {
            AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
            WayPoint wayPoint = _MakeWayPoint();
            var json = JsonConvert.SerializeObject(wayPoint);
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            using var client = new HttpClient();
            var response = await client.PostAsync(_url, data);
            string result = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine(DateTime.Now + "Route Updated" + json);
            _currentRouteIndex++;
            if (_currentRouteIndex == _routes.Count)
            {
                autoEvent.Set();
            }
        }

        private WayPoint _MakeWayPoint()
        {
            float latitude = float.Parse(_routes[_currentRouteIndex].latitude);
            float longitude = float.Parse(_routes[_currentRouteIndex].longitude);
            float speedlimit = int.Parse(_routes[_currentRouteIndex].speedlimit);
            _CalculateSpeed(speedlimit);
            float course = string.IsNullOrEmpty(_routes[_currentRouteIndex].course) ? 0 : float.Parse(_routes[_currentRouteIndex].course);
            return new WayPoint(latitude, longitude, _currentSpeed, course);
        }

        private void _CalculateSpeed(float speedlimit)
        {
            if (_currentRouteIndex > 0)
            {
                if (_currentSpeed < speedlimit)
                {
                    _currentSpeed += _accelerationKph;
                    if (_currentSpeed > speedlimit)
                    {
                        _currentSpeed = speedlimit;
                    }
                }
                else if (_currentSpeed > speedlimit)
                {
                    _currentSpeed -= _brakeKph;
                    if (_currentSpeed < speedlimit)
                    {
                        _currentSpeed = speedlimit;
                    }
                }
            }
        }
    }

    public class WayPoint
    {
        public float latitude { get; set; }
        public float longitude { get; set; }
        public float speed { get; set; }
        public float course { get; set; }

        public WayPoint(float latitudeVal, float longitudeVal, float speedVal, float courseVal)
        {
            latitude = latitudeVal;
            longitude = longitudeVal;
            speed = speedVal;
            course = courseVal;
        }
    }
}
