using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Reporting
{
    /// <summary>
    /// Сервис сериализации модели DashboardData в JSON для JavaScript.
    /// </summary>
    public class JsonSerializerService
    {
        private readonly JsonSerializerSettings _serializerSettings;

        public JsonSerializerService()
        {
            _serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
        }

        public string Serialize(DashboardData data)
        {
            return JsonConvert.SerializeObject(data, _serializerSettings);
        }
    }
}
