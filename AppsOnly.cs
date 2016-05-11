using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Okta.Tools.UserExporter
{
    public class AppsOnly
    {

        public static bool SelfService { get; set; }
        public static string ErrorRedirectUrl { get; set; }
        public static string LoginRedirectUrl { get; set; }


        // Exports only App Id and App Name
        public static async Task<int> AppsOnlyMain()
        {
            Common.SetFile(Common.OutputAppsOnlyFileName);

            try
            {
                using (var http = new HttpClient { BaseAddress = new Uri(Common.OktaUrl), Timeout = TimeSpan.FromSeconds(120) })
                {
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SSWS", Common.OktaApiKey);
                    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var headerColumns = new[] { "App Id", "ORN", "Name","Label", "Status",
                        "Last Updated", "Created", "Accessibility", "Visibility", "Features",
                        "Sign On Mode", "Credentials", "Universal Logout", "Settings", "Links",
                        "Date Time" };

                    Console.WriteLine("Writing CSV to {0}", Common.FilePath);
                    using (var fs = new FileStream(Common.FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        // Write header
                        sw.WriteLine(string.Join(",", headerColumns.Select(Common.CsvEscape)));

                        // Fetch all apps with throttled HTTP + pagination
                        var appsArray = await Common.GetAllPagesAsJArrayAsync(http, "/api/v1/apps?limit=200");

                        foreach (var a in appsArray)
                        {
                            var a_id = (string)a["id"] ?? string.Empty;
                            var a_orn = (string)a["orn"] ?? string.Empty;
                            var a_name = (string)a["name"] ?? string.Empty;
                            var a_label = (string)a["label"] ?? string.Empty;
                            var a_status = (string)a["status"] ?? string.Empty;
                            var a_lastUpdated = (string)a["lastUpdated"] ?? string.Empty;
                            var a_created = (string)a["created"] ?? string.Empty;

                            var a_accessibility = a["accessibility"] as JObject;
                            var a_visibility = a["visibility"] as JObject;
                            var a_features = a["features"] as JObject;
                            var a_signOnMode = a["signOnMode"] as JObject;
                            var a_credentials = a["credentials"] as JObject;
                            var a_universalLogout = a["universalLogout"] as JObject;
                            var a_settings = a["settings"] as JObject;
                            var a_links = a["_links"] as JObject;

                            var fields = new[]
                            {
                                Common.CsvEscape(a_id),
                                Common.CsvEscape(a_orn),
                                Common.CsvEscape(a_name),
                                Common.CsvEscape(a_label),
                                Common.CsvEscape(a_status),
                                Common.CsvEscape(a_lastUpdated),
                                Common.CsvEscape(a_created),
                                Common.CsvEscape(a_accessibility),
                                Common.CsvEscape(a_visibility),
                                Common.CsvEscape(a_features),
                                Common.CsvEscape(a_signOnMode),
                                Common.CsvEscape(a_credentials),
                                Common.CsvEscape(a_universalLogout),
                                Common.CsvEscape(a_settings),
                                Common.CsvEscape(a_links),
                                Common.CsvEscape(Common.DateTime_Dt)
                            };
                            sw.WriteLine(string.Join(",", fields));
                        }
                    }
                } 

                Console.WriteLine("Apps-only export completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("An error occurred: {0}", ex);
                return 1;
            }
        }

        //bool aa_selfService = false;
        //string aa_errorRedirectUrl = string.Empty;
        //string aa_loginRedirectUrl = string.Empty;
        //if (a_accessibility != null)
        //{
        //    aa_selfService = (bool?)a_accessibility["selfService"] ?? false;
        //    aa_errorRedirectUrl = (string)a_accessibility["errorRedirectUrl"] ?? string.Empty;
        //    aa_loginRedirectUrl = (string)a_accessibility["loginRedirectUrl"] ?? string.Empty;
        //}
    }
}
