using Okta.Core;
using Okta.Core.Clients;
using Okta.Core.Models;
//using Polly;
//using Polly.Retry;
using System;
using System.Collections.Generic;
//using System.Configuration;
//using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Okta.Tools.UserExporter
{
    public class Users
    {
        // Main is async; Visual Studio 2022 with modern C# language version required.
        public static async Task<int> UsersMain()
        {

            Common.SetFile(Common.OutputFileName);

            try
            {
                var oktaClient = new OktaClient(Common.OktaApiKey, new Uri(Common.OktaUrl));
                try
                {
                    var usersClient = oktaClient.GetUsersClient();

                    // First pass: collect union of all unmapped property names across all users
                    var dynamicColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    Uri nextPage = null;
                    PagedResults<User> usersPage;

                    Console.WriteLine("Collecting unmapped property names from Okta...");
                    do
                    {
                        // The Okta SDK call is synchronous; wrap in Task.Run and protect with Polly retry policy
                        usersPage = await Common.RetryPolicy.ExecuteAsync(() =>
                            Task.Run(() => usersClient.GetList(pageSize: 100, nextPage: nextPage)));

                        foreach (var user in usersPage.Results)
                        {
                            Common.UserIds.Add(user.Id);

                            var unmappedNames = user.Profile.GetUnmappedPropertyNames();
                            foreach (var name in unmappedNames)
                            {
                                if (!string.IsNullOrWhiteSpace(name))
                                    dynamicColumns.Add(name);
                            }
                        }

                        nextPage = usersPage.NextPage;
                    } while (!usersPage.IsLastPage);

                    // Build header: fixed columns + sorted dynamic columns (sorted for deterministic output)
                    var fixedColumns = new[]
                    {
                        "Id","Login","Status","Created","Activated","LastLogin","LastUpdated","PasswordChanged","StatusChanged",
                        "FirstName","LastName","Email","SecondaryEmail","MobilePhone"
                    };

                    var dynamicColumnsList = new List<string>(dynamicColumns);
                    dynamicColumnsList.Sort(StringComparer.OrdinalIgnoreCase);

                    var headerColumns = new List<string>(fixedColumns.Length + dynamicColumnsList.Count + 1);
                    headerColumns.AddRange(fixedColumns);
                    headerColumns.AddRange(dynamicColumnsList);
                    headerColumns.Add("Date Time");

                    // Second pass: fetch again and write CSV with header that includes dynamic columns
                    Console.WriteLine("Writing CSV to {0}", Common.FilePath);
                    using (var fs = new FileStream(Common.FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                    {
                        // Write header
                        sw.WriteLine(string.Join(",", headerColumns.ConvertAll(Common.CsvEscape)));

                        nextPage = null;
                        do
                        {
                            usersPage = await Common.RetryPolicy.ExecuteAsync(() =>
                                Task.Run(() => usersClient.GetList(pageSize: 100, nextPage: nextPage)));

                            var lines = new List<string>(usersPage.Results.Count);
                            foreach (var user in usersPage.Results)
                            {
                                var fields = new List<string>(headerColumns.Count);
                                var groups = new List<string>();

                                // Fixed columns (use helper to format dates)
                                fields.Add(Common.CsvEscape(user.Id));
                                fields.Add(Common.CsvEscape(user.Profile?.Login));
                                fields.Add(Common.CsvEscape(user.Status));
                                fields.Add(Common.CsvEscape(Common.FormatNullable(user.Created)));
                                fields.Add(Common.CsvEscape(Common.FormatNullable(user.Activated)));
                                fields.Add(Common.CsvEscape(Common.FormatNullable(user.LastLogin)));
                                fields.Add(Common.CsvEscape(Common.FormatNullable(user.LastUpdated)));
                                fields.Add(Common.CsvEscape(Common.FormatNullable(user.PasswordChanged)));
                                fields.Add(Common.CsvEscape(Common.FormatNullable(user.StatusChanged)));
                                fields.Add(Common.CsvEscape(user.Profile?.FirstName));
                                fields.Add(Common.CsvEscape(user.Profile?.LastName));
                                fields.Add(Common.CsvEscape(
                                    string.IsNullOrWhiteSpace(user.Profile?.Email) ? " " : user.Profile?.Email));
                                fields.Add(Common.CsvEscape(
                                    string.IsNullOrWhiteSpace(user.Profile?.SecondaryEmail) ? " " : user.Profile?.SecondaryEmail));
                                fields.Add(Common.CsvEscape(
                                    string.IsNullOrWhiteSpace(user.Profile?.MobilePhone) ? " " : user.Profile?.MobilePhone));

                                // Dynamic columns: ensure order matches headerColumns
                                foreach (var dyn in dynamicColumnsList)
                                {
                                    try
                                    {
                                        string value = user.Profile.GetProperty(dyn) ?? string.Empty;
                                        fields.Add(Common.CsvEscape(value));
                                    }
                                    catch
                                    {
                                        // In case of any error (e.g., property not found), add empty field
                                        fields.Add(Common.CsvEscape(string.Empty));
                                    }
                                }
                                // Date Time
                                fields.Add(Common.CsvEscape(Common.DateTimeStamp_Rpt));

                                lines.Add(string.Join(",", fields));
                            }

                            // Write page in one batch then flush
                            foreach (var l in lines) sw.WriteLine(l);
                            sw.Flush();

                            nextPage = usersPage.NextPage;
                        } while (!usersPage.IsLastPage);
                    }
                }
                finally
                {
                    // No disposal needed for OktaClient since it does not implement IDisposable
                }

                Console.WriteLine("Export completed successfully.");
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
