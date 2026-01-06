using Geowerkstatt.Ilicop.Web.XtfLog;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetTopologySuite.IO.Converters;
using System.Text.Json;

namespace Geowerkstatt.Ilicop.Web
{
    [TestClass]
    public class GeoJsonHelperTest
    {
        private JsonSerializerOptions serializerOptions;

        [TestInitialize]
        public void Initialize()
        {
            serializerOptions = new JsonSerializerOptions();
            serializerOptions.Converters.Add(new GeoJsonConverterFactory());
        }

        [TestMethod]
        public void CreateFeatureCollectionForGeoJson()
        {
            var logResult = new[]
            {
                new LogError
                {
                    Message = "Error message without coordinate",
                    Type = "Error",
                },
                new LogError
                {
                    Message = "Error message 1",
                    Type = "Error",
                    ObjTag = "Model.Topic.Class",
                    Line = 11,
                    Geometry = new Geometry
                    {
                        Coord = new Coord
                        {
                            C1 = 2671295m,
                            C2 = 1208106m,
                        },
                    },
                },
            };

            var featureCollection = GeoJsonHelper.CreateFeatureCollection(logResult);
            Assert.HasCount(1, featureCollection);

            var geoJson = JsonSerializer.Serialize(featureCollection, serializerOptions);
            Assert.AreEqual("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[8.376399953106437,47.02016965999489]},\"properties\":{\"type\":\"Error\",\"message\":\"Error message 1\",\"objTag\":\"Model.Topic.Class\",\"dataSource\":null,\"line\":11,\"techDetails\":null}}]}", geoJson);
        }
    }
}
