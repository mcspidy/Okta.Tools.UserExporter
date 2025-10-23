using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Okta.Tools.UserExporter
{
    /// <summary>
    /// Utilities to export groups from Okta into CSV output.
    /// </summary>
    public class GroupsOnly
    {
        /// <summary>
        /// Exports Okta groups to a CSV file and returns an exit code.
        /// </summary>
        /// <remarks>
        /// This method:
        /// - Sets the output file via <see cref="Common.SetFile(string)"/>.
        /// - Creates an <see cref="HttpClient"/> configured for the Okta API.
        /// - Writes a CSV header row and then iterates all groups returned by
        ///   <see cref="Common.GetAllPagesAsJArrayAsync(System.Net.Http.HttpClient, string)"/>.
        /// - Extracts group fields, escapes each field with <see cref="Common.CsvEscape(object)"/>,
        ///   and writes CSV rows.
        /// The method returns 0 on success and 1 if an exception occurs.
        /// </remarks>
        /// <returns>Task resolving to 0 on success, 1 on error.</returns>
        public static async Task<int> GroupsOnlyMain()
        {
            Common.SetFile(Common.OutputGroupsOnlyFileName);

            try
            {
                using (var http = new HttpClient { BaseAddress = new Uri(Common.OktaUrl), Timeout = TimeSpan.FromSeconds(120) })
                {
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SSWS", Common.OktaApiKey);
                    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var headerColumns = new[] { "Group Id", "Name", "Description", "Type", "Object Class",
                        "Created", "Last Updated", "Source", "Links",
                        "Date Time" };

                    Console.WriteLine("Writing CSV to {0}", Common.FilePath);
                    using (var fs = new FileStream(Common.FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        // Write header
                        sw.WriteLine(string.Join(",", headerColumns.Select(Common.CsvEscape)));

                        // Fetch all groups with throttled HTTP + pagination
                        var groupsArray = await Common.GetAllPagesAsJArrayAsync(http, "/api/v1/groups?limit=200");

                        foreach (var g in groupsArray)
                        {
                            var g_id = (string)g["id"] ?? string.Empty;
                            var g_created = (string)g["created"] ?? string.Empty;
                            var g_lastUpdated = (string)g["lastUpdated"] ?? string.Empty;
                            var g_class = JTokenToFlatString(g["objectClass"]);
                            var g_type = (string)g["type"] ?? string.Empty;
                            var g_name = (string)(g["profile"] != null ? g["profile"]["name"] : null) ?? string.Empty;
                            var g_desc = (string)(g["profile"] != null ? g["profile"]["description"] : null) ?? string.Empty;
                            var g_source = g["source"] as JObject;
                            var g_links = g["_links"] as JObject;

                            var fields = new[]
                            {
                                Common.CsvEscape(g_id),
                                Common.CsvEscape(g_name),
                                Common.CsvEscape(g_desc),
                                Common.CsvEscape(g_type),
                                Common.CsvEscape(g_class),
                                Common.CsvEscape(g_created),
                                Common.CsvEscape(g_lastUpdated),
                                Common.CsvEscape(g_source),
                                Common.CsvEscape(g_links),

                                Common.CsvEscape(Common.DateTimeStamp_Rpt)
                            };
                            sw.WriteLine(string.Join(",", fields));
                        }
                    }
                }

                Console.WriteLine("Group-only export completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("An error occurred: {0}", ex);
                return 1;
            }
        }

        // helper to convert JToken to a CSV-safe single string
        private static string JTokenToFlatString(JToken token)
        {
            if (token == null) return string.Empty;

            switch (token.Type)
            {
                case JTokenType.Array:
                    // join array elements with semicolon (adjust separator as needed)
                    return string.Join(";", token.Select(t => t.Type == JTokenType.String ? (string)t : t.ToString()));
                case JTokenType.Object:
                    // flatten object to compact JSON or pick a property
                    return token.ToString(Newtonsoft.Json.Formatting.None);
                default:
                    return (string)token ?? string.Empty;
            }
        }
    }
}
