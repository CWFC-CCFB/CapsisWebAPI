using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capsis.Handler
{
    public class Utility
    {
        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static DataTable ReadCSVData(Stream inputStream)
        {
            var dt = new DataTable();

            bool firstLine = true;
            string csvSeparator = ",";

            string surveyID = "";

            using (var reader = new StreamReader(inputStream))
            {
                //int surveyIDIndex = -1;
                int lineNumber = 0;

                while (reader.Peek() >= 0)
                {
                    string line = reader.ReadLine().Replace("\"", "");

                    if (firstLine)
                    {
                        string possibleCSVSeparators = ",;";

                        // auto detect 
                        for (int i = 0; i < line.Length; i++)
                        {
                            for (int j = 0; j < possibleCSVSeparators.Length; j++)
                            {
                                if (line[i] == possibleCSVSeparators[j])
                                {
                                    csvSeparator = possibleCSVSeparators[j].ToString();
                                    i = line.Length;
                                    break;
                                }
                            }
                        }

                        var columnNames = line.Split(csvSeparator);
                        dt.Columns.AddRange(columnNames.Select(c => new DataColumn(c)).ToArray());

                        //for (int i = 0; i < columnNames.Length; i++)
                        //{
                        //    if (columnNames[i] == VariantFields.PlotID)
                        //    {
                        //        surveyIDIndex = i;
                        //        break;
                        //    }
                        //}

                        //if (surveyIDIndex == -1)
                        //    throw new ArgumentException(VariantFields.PlotID + " column not found in csv data");

                        firstLine = false;
                    }
                    else
                    {
                        var values = line.Split(csvSeparator);

                        dt.Rows.Add(values);
                    }

                    lineNumber++;
                }
            }

            if (dt.Rows.Count == 0)
                throw new ArgumentException("CSVRead led to an empty data table");

            return dt;
        }

        /// <summary>
        /// Deserializes a json string to an object, but using Json .NET DuplicatePropertyNameHandling setting to throw an error 
        /// when duplicate keys are encountered on the same level in the json structure 
        /// </summary>
        /// <param name="json">The json string to deserialize</param>
        /// <param name="settings">The specific settings, if any</param>
        /// <returns>The deserialized object of type T</returns>
        public static T? DeserializeObject<T>(string json, JsonSerializerSettings? settings = null)
        {
            JsonSerializer jsonSerializer = JsonSerializer.CreateDefault(settings);
            using (var stringReader = new StringReader(json))
            using (var jsonTextReader = new JsonTextReader(stringReader))
            {
                try
                {
                    jsonTextReader.DateParseHandling = DateParseHandling.None;
                    JsonLoadSettings loadSettings = new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    };
                    var jtoken = JToken.ReadFrom(jsonTextReader, loadSettings);
                    return jtoken.ToObject<T>(jsonSerializer);
                }
                catch (JsonReaderException e)
                {   // when a duplicate key is encountered, rethrow as JsonSerializationException 
                    throw new JsonSerializationException(e.Message, e);
                }
            }
        }
    }
}
