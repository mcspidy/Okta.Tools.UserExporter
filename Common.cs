using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent; // ADD THIS
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json.Linq;
using Common.Utils;

namespace Okta.Tools.UserExporter
{
    public class Common
    {
        public static string OutputFileName { get; private set; } = string.Empty;
        public static string OutputUsersGroupsFileName { get; private set; } = string.Empty;
        public static string OutputGroupsOnlyFileName { get; private set; } = string.Empty;
        public static string OutputUsersAppsFileName { get; private set; } = string.Empty;
        public static string OutputAppsOnlyFileName { get; private set; } = string.Empty;
        public static string OutputJsonFileName { get; private set; } = string.Empty;
        private static string ConfiguredFolder { get; set; } = string.Empty;
        public static string FilePath { get; private set; } = string.Empty;
        public static string OktaUrl { get; private set; } = string.Empty;
        public static string OktaApiKey { get; private set; } = string.Empty;
        public static int RetryCount { get; private set; } = 0;
        public static int BaseDelayMs { get; private set; } = 0;
        public static AsyncRetryPolicy RetryPolicy { get; private set; }
        public static List<string> UserIds { get; set; } = new List<string>();
        public static List<string> UserGroupIds { get; set; } = new List<string>();
        public static List<string> WriteLines { get; set; } = new List<string>();

        public static DateTime Now_DTS { get; set; }
        public static string Todays_Dt { get; private set; } = string.Empty;
        public static string DateTime_Dt { get; private set; } = string.Empty;
        public static string DateTimeStamp_Dt { get; private set; } = string.Empty;
        public static string DateTimeStamp_Rpt { get; private set; } = string.Empty;

        // ADD THIS: map userId -> list of groupIds
        public static ConcurrentDictionary<string, List<string>> UserToGroupIds { get; } =
            new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Throttling configuration
        // Increase default inter-request delay to reduce 429s
        public static int InterRequestDelayMs { get; private set; } = 1000; // default 1000ms (1 request/sec)
        public static int HttpMaxRetries { get; private set; } = 6;

        // Throttling state
        private static readonly object _throttleLock = new object();
        private static DateTime _nextAvailableUtc = DateTime.MinValue;
        private static readonly Random _jitter = new Random();

        public static int Read_AppSettings()
        {
            // Configuration (prefer environment variable for secrets in production)
            OutputFileName = ReplaceDatePlaceholders(ConfigurationManager.AppSettings["OutputFileName"]) ?? "OktaUsers.csv";
            OutputUsersGroupsFileName = ReplaceDatePlaceholders(ConfigurationManager.AppSettings["OutputUsersGroupsFileName"]) ?? "OktaUsersGroups.csv";
            OutputGroupsOnlyFileName = ReplaceDatePlaceholders(ConfigurationManager.AppSettings["OutputGroupsOnlyFileName"]) ?? "OktaGroupsOnly.csv";
            OutputUsersAppsFileName = ReplaceDatePlaceholders(ConfigurationManager.AppSettings["OutputUsersAppsFileName"]) ?? "OktaUsersApps.csv";
            OutputAppsOnlyFileName = ReplaceDatePlaceholders(ConfigurationManager.AppSettings["OutputAppsOnlyFileName"]) ?? "OktaAppsOnly.csv";
            OutputJsonFileName = ReplaceDatePlaceholders(ConfigurationManager.AppSettings["OutputJsonFileName"]) ?? "OktaExtract.json";

            // Determine the folder path from configuration if provided; otherwise use current directory
            ConfiguredFolder = ConfigurationManager.AppSettings["OutputFolderPath"] ?? string.Empty;

            OktaUrl = ConfigurationManager.AppSettings["OktaUrl"];
            OktaApiKey = Environment.GetEnvironmentVariable("OKTA_API_KEY")
                                 ?? ConfigurationManager.AppSettings["OktaApiKey"];
            if (string.IsNullOrEmpty(OktaApiKey))
            {
                Console.Error.WriteLine("Okta API key not set. Set environment variable OKTA_API_KEY or AppSettings:OktaApiKey.");
                return 2;
            }

            // Polly settings (can be overridden in App.config)
            RetryCount = ParseIntOrDefault(ConfigurationManager.AppSettings["PollyRetryCount"], 3);
            BaseDelayMs = ParseIntOrDefault(ConfigurationManager.AppSettings["PollyRetryBaseDelayMs"], 500);

            // HTTP/429-specific settings
            InterRequestDelayMs = ParseIntOrDefault(ConfigurationManager.AppSettings["InterRequestDelayMs"], InterRequestDelayMs);
            HttpMaxRetries = ParseIntOrDefault(ConfigurationManager.AppSettings["HttpMaxRetries"], HttpMaxRetries);

            // Build an async retry policy with exponential backoff
            RetryPolicy = Polly.Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    RetryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (ex, timespan, attempt, context) =>
                    {
                        Console.Error.WriteLine("Retry {0} after {1}ms due to: {2}", attempt, (int)timespan.TotalMilliseconds, ex.Message);
                    });

            return 0;
        }

        /// <summary>
        /// Sets <see cref="FilePath"/> using <see cref="File_Utils.SetFile(string,string)"/> and returns the resulting path.
        /// </summary>
        /// <param name="value">Filename or key used to compute the full path.</param>
        /// <returns>The computed file path.</returns>
        public static string SetFile(string value)
        {
            FilePath = File_Utils.SetFile(value, ConfiguredFolder);

            return FilePath;
        }

        /// <summary>
        /// Escapes <paramref name="value"/> for inclusion in a CSV field.
        /// - Null returns an empty quoted field: <c>""</c>.
        /// - <see cref="DateTime"/> values are formatted using the round-trip ("o") format.
        /// - Other values are converted using <see cref="Convert.ToString(object,System.IFormatProvider)"/>.
        /// Newlines are replaced with spaces and double quotes are doubled. The result is wrapped in double quotes.
        /// </summary>
        /// <param name="value">The value to escape (may be string, DateTime, JToken, etc.).</param>
        /// <returns>CSV-safe quoted string.</returns>
        public static string CsvEscape(object value)
        {
            if (value == null) return "\"\"";
            string s = value is DateTime dt
                ? dt.ToString("o", CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            s = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\"", "\"\"");
            return $"\"{s}\"";
        }

        public static string FormatNullable(DateTime? dt) => dt?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty;

        public static int ParseIntOrDefault(string s, int def)
        {
            return int.TryParse(s, out var v) ? v : def;
        }

        // Compiled regex with ordered alternation so %DateTimeStamp% is matched before %DateTime%
        private static readonly Regex DatePlaceholderRegex =
            new Regex("%DateTimeStamp%|%DateTime%|%Today%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Replaces case-insensitive placeholders with formatted date/time values:
        // %Today%        -> yyyy-MM-dd
        // %DateTime%     -> yyyy-MM-dd_HH-mm-ss
        // %DateTimeStamp%-> yyyy-MM-ddTHH:mm:ss.ffffff (26 characters)
        public static string ReplaceDatePlaceholders(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

            var now = DateTime.Now;
            if (Now_DTS.ToString("yyyyMMdd") != "00010101")
            {
                now = Now_DTS;
            }
            if (Todays_Dt == string.Empty)
            {
                Todays_Dt = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }
            if (DateTime_Dt == string.Empty)
            {
                DateTime_Dt = now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            }
            if (DateTimeStamp_Dt == string.Empty)
            {
                DateTimeStamp_Dt = now.ToString("yyyyMMdd_HHmmss.ffffff", CultureInfo.InvariantCulture); // 21 chars
                DateTimeStamp_Rpt = now.ToString("yyyy-MM-dd_HH:mm:ss.ffffff", CultureInfo.InvariantCulture); // 26 chars
            }

            return DatePlaceholderRegex.Replace(input, m =>
            {
                var token = m.Value;
                if (token.Equals("%Today%", StringComparison.OrdinalIgnoreCase)) return Todays_Dt;
                if (token.Equals("%DateTime%", StringComparison.OrdinalIgnoreCase)) return DateTime_Dt;
                return DateTimeStamp_Dt; // %DateTimeStamp%
            });
        }

        // Enforce a minimum gap between HTTP requests to avoid rate limiting.
        private static Task ThrottleAsync()
        {
            int delayMs = 0;
            lock (_throttleLock)
            {
                var now = DateTime.UtcNow;
                if (_nextAvailableUtc > now)
                {
                    delayMs = (int)Math.Max(0, (_nextAvailableUtc - now).TotalMilliseconds);
                }
                // Reserve the next slot now to prevent stampedes
                _nextAvailableUtc = now.AddMilliseconds(InterRequestDelayMs);
            }
            return delayMs > 0 ? Task.Delay(delayMs) : Task.CompletedTask;
        }

        // Perform GET with throttling + robust retries for 429 and 5xx
        public static async Task<HttpResponseMessage> GetAsyncWithThrottling(HttpClient http, string url, CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= HttpMaxRetries; attempt++)
            {
                await ThrottleAsync().ConfigureAwait(false);

                HttpResponseMessage resp = null;
                try
                {
                    resp = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout - treat like transient error
                    resp = new HttpResponseMessage(HttpStatusCode.RequestTimeout);
                }
                catch (HttpRequestException ex)
                {
                    // Transient network error - apply backoff and retry
                    if (attempt == HttpMaxRetries) throw;
                    var backoff = ComputeBackoff(attempt);
                    Console.Error.WriteLine("HTTP exception on attempt {0}, retrying after {1}ms: {2}", attempt, backoff, ex.Message);
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if ((int)resp.StatusCode == 429)
                {
                    var serverSuggested = GetRetryAfterDelay(resp);
                    var computed = ComputeBackoff(attempt, true);
                    // Choose largest of server suggestion, computed backoff, and a conservative fallback (10x inter-request delay)
                    var fallback = InterRequestDelayMs * 10;
                    var wait = serverSuggested ?? Math.Max(computed, fallback);

                    // Log server-provided retry info when available
                    string ra = resp.Headers.RetryAfter != null ? resp.Headers.RetryAfter.ToString() : string.Empty;
                    IEnumerable<string> resetHeaders;
                    string reset = resp.Headers.TryGetValues("X-Rate-Limit-Reset", out resetHeaders) ? string.Join(";", resetHeaders) : string.Empty;
                    Console.Error.WriteLine("Received 429 (Too Many Requests). Retry-After: {0} X-Rate-Limit-Reset: {1}. Waiting {2}ms before retry (attempt {3}/{4}).", ra, reset, wait, attempt, HttpMaxRetries);
                    if (attempt == HttpMaxRetries)
                    {
                        // Give final attempt a chance to surface the 429
                        return resp;
                    }
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if ((int)resp.StatusCode >= 500)
                {
                    var wait = ComputeBackoff(attempt);
                    Console.Error.WriteLine("Server error {0}. Waiting {1}ms then retry (attempt {2}/{3}).", (int)resp.StatusCode, wait, attempt, HttpMaxRetries);
                    if (attempt == HttpMaxRetries)
                    {
                        return resp; // let caller decide
                    }
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // For other statuses (including success), return and let caller handle
                return resp;
            }

            // Should not reach here
            throw new InvalidOperationException("Exceeded maximum HTTP retries.");
        }

        private static int ComputeBackoff(int attempt, bool for429 = false)
        {
            // Exponential backoff with jitter
            var baseMs = for429 ? Math.Max(InterRequestDelayMs * 2, 500) : Math.Max(BaseDelayMs, 250);
            var delay = (int)(baseMs * Math.Pow(2, attempt - 1));
            var jitter = _jitter.Next(50, 250);
            return delay + jitter;
        }

        private static int? GetRetryAfterDelay(HttpResponseMessage resp)
        {
            // Okta may return Retry-After as seconds or as a date.
            if (resp.Headers.RetryAfter != null)
            {
                if (resp.Headers.RetryAfter.Delta.HasValue)
                {
                    var ms = (int)resp.Headers.RetryAfter.Delta.Value.TotalMilliseconds;
                    return Math.Max(ms, InterRequestDelayMs);
                }
                if (resp.Headers.RetryAfter.Date.HasValue)
                {
                    var target = resp.Headers.RetryAfter.Date.Value.UtcDateTime;
                    var ms = (int)Math.Max(0, (target - DateTime.UtcNow).TotalMilliseconds);
                    return Math.Max(ms, InterRequestDelayMs);
                }
            }
            // Some Okta responses include X-Rate-Limit-Reset (epoch seconds)
            if (resp.Headers.TryGetValues("X-Rate-Limit-Reset", out var vals))
            {
                var resetStr = vals.FirstOrDefault();
                long epoch;
                if (long.TryParse(resetStr, out epoch))
                {
                    var resetUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                    var ms = (int)Math.Max(0, (resetUtc - DateTime.UtcNow).TotalMilliseconds);
                    return Math.Max(ms, InterRequestDelayMs);
                }
            }
            return null;
        }

        /// <summary>
        /// Fetches all pages from an Okta collection endpoint and aggregates the results into a single <see cref="JArray"/>.
        /// </summary>
        /// <param name="http">Configured <see cref="HttpClient"/> to use for requests.</param>
        /// <param name="relativeOrAbsoluteUrl">Relative or absolute URL to the collection endpoint.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="Task{JArray}"/> containing all items across pages.</returns>
        /// <exception cref="HttpRequestException">If a non-success status code is returned (except the method allows 404 to surface for visibility).</exception>
        public static async Task<JArray> GetAllPagesAsJArrayAsync(HttpClient http, string relativeOrAbsoluteUrl, CancellationToken cancellationToken = default(CancellationToken))
        {
            var aggregate = new JArray();
            string url = relativeOrAbsoluteUrl;

            while (!string.IsNullOrEmpty(url))
            {
                var resp = await GetAsyncWithThrottling(http, url, cancellationToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    // Allow 404 to surface for visibility; otherwise throw
                    resp.EnsureSuccessStatusCode();
                }
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var arr = JArray.Parse(json);
                    foreach (var item in arr)
                    {
                        aggregate.Add(item);
                    }
                }

                url = TryGetNextLink(resp);
            }

            return aggregate;
        }

        private static string TryGetNextLink(HttpResponseMessage resp)
        {
            IEnumerable<string> links;
            if (resp.Headers.TryGetValues("Link", out links))
            {
                foreach (var header in links)
                {
                    // Link: <https://.../api/v1/users/..../groups?after=...&limit=200>; rel="next"
                    var parts = header.Split(',');
                    foreach (var part in parts)
                    {
                        var seg = part.Trim();
                        if (seg.EndsWith("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                        {
                            var start = seg.IndexOf('<');
                            var end = seg.IndexOf('>');
                            if (start >= 0 && end > start)
                            {
                                var url = seg.Substring(start + 1, end - start - 1);
                                return url;
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}
