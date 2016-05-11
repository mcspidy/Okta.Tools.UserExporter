using Okta.Core;
using Okta.Core.Clients;
using Okta.Core.Models;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// Testing note: This code interacts with the Okta API and requires valid credentials and network access to run successfully.

namespace Okta.Tools.UserExporter
{
    class Program
    {
        static void Main(string[] args) {

            // Loop through args 
            foreach (string arg in args)
            {
                Console.WriteLine(arg);
            }

            // Override Date for Testing
            //Common.Now_DTS = new DateTime(2025, 10, 22, 11, 54, 56);

            Common.Read_AppSettings();

            //Users.UsersMain().GetAwaiter().GetResult();
            GroupsOnly.GroupsOnlyMain().GetAwaiter().GetResult();
            //Groups.GroupsMain().GetAwaiter().GetResult();
            //AppsOnly.AppsOnlyMain().GetAwaiter().GetResult();
            //Apps.AppsMain().GetAwaiter().GetResult();

            // Build JSON summary of created files
            Build_JSON.BuildJsonMain().GetAwaiter().GetResult();
        }


    }
}
