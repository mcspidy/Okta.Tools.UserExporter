using Okta.Core;
using Okta.Core.Clients;
using Okta.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Okta.Tools.UserExporter
{
    public class Groups
    {
        // Main is async; Visual Studio 2022 with modern C# language version required.
        public static async Task<int> GroupsMain()
        {
            Common.SetFile(Common.OutputUsersGroupsFileName);

            try
            {
                var oktaClient = new OktaClient(Common.OktaApiKey, new Uri(Common.OktaUrl));

                // HttpClient to call /api/v1/users/{id}/groups
                using (var http = new HttpClient { BaseAddress = new Uri(Common.OktaUrl), Timeout = TimeSpan.FromSeconds(120) })
                {
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SSWS", Common.OktaApiKey);
                    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    Uri nextPage = null;

                    // Ensure we have user IDs
                    if (Common.UserIds.Count == 0)
                    {
                        Console.WriteLine("Fetching users from Okta...");
                        var usersClient = oktaClient.GetUsersClient();
                        PagedResults<User> usersPage;

                        do
                        {
                            // The Okta SDK call is synchronous; wrap in Task.Run and protect with Polly retry policy
                            usersPage = await Common.RetryPolicy.ExecuteAsync(() =>
                                Task.Run(() => usersClient.GetList(pageSize: 200, nextPage: nextPage)));

                            foreach (var user in usersPage.Results)
                            {
                                Common.UserIds.Add(user.Id);
                            }

                            nextPage = usersPage.NextPage;
                            // Pause briefly to avoid rate limiting between pages
                            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        } while (!usersPage.IsLastPage);
                    }

                    // Prepare CSV header
                    var headerColumns = new[] { "Id", "Group Id", "Date Time" }; //, "Group Name" };

                    Console.WriteLine("Writing CSV to {0}", Common.FilePath);
                    using (var fs = new FileStream(Common.FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        // Write header
                        sw.WriteLine(string.Join(",", headerColumns.Select(Common.CsvEscape)));

                        // Iterate all users and fetch their groups with throttled HTTP + pagination
                        int userIndex = 0;
                        foreach (var userId in Common.UserIds)
                        {
                            userIndex++;
                            if (userIndex % 25 == 0)
                            {
                                sw.Flush(); // periodically flush to disk
                            }

                            // Fetch groups for this user (handles 429/5xx and pagination)
                            var groupsArray = await Common.GetAllPagesAsJArrayAsync(http, $"/api/v1/users/{userId}/groups?limit=200");

                            // Cache group ids by user if needed later
                            var groupIds = new List<string>(groupsArray.Count);

                            foreach (var g in groupsArray)
                            {
                                var gid = (string)g["id"] ?? string.Empty;
                                var gname = (string)(g["profile"] != null ? g["profile"]["name"] : null) ?? string.Empty;
                                groupIds.Add(gid);

                                var fields = new List<string>(3);
                                fields.Add(Common.CsvEscape(userId));
                                fields.Add(Common.CsvEscape(gid));
                                //fields.Add(Common.CsvEscape(gname));
                                fields.Add(Common.CsvEscape(Common.DateTimeStamp_Rpt));
                                sw.WriteLine(string.Join(",", fields));
                            }

                            Common.UserToGroupIds[userId] = groupIds;
                        }
                    }
                }

                Console.WriteLine("Group export completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("An error occurred: {0}", ex);
                return 1;
            }
        }
    }
}