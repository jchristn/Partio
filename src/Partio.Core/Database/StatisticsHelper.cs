namespace Partio.Core.Database
{
    using System.Data;
    using System.Globalization;
    using Partio.Core.Models;

    /// <summary>
    /// Shared helper methods for building request statistics across database providers.
    /// All time bucketing is done in C# (not SQL) for cross-database reliability.
    /// </summary>
    internal static class StatisticsHelper
    {
        /// <summary>
        /// Get the UTC cutoff timestamp for the given timeframe.
        /// </summary>
        internal static DateTime GetCutoff(string? timeframe)
        {
            return (timeframe ?? "Day") switch
            {
                "Hour" => DateTime.UtcNow.AddHours(-1),
                "Day" => DateTime.UtcNow.AddDays(-1),
                "Week" => DateTime.UtcNow.AddDays(-7),
                "Month" => DateTime.UtcNow.AddDays(-30),
                _ => DateTime.UtcNow.AddDays(-1)
            };
        }

        /// <summary>
        /// Get the bucket step size for the given timeframe.
        /// Hour => 1 minute (~60 buckets),
        /// Day => 15 minutes (~96 buckets),
        /// Week => 1 hour (~168 buckets),
        /// Month => 4 hours (~180 buckets).
        /// </summary>
        internal static TimeSpan GetBucketStep(string? timeframe)
        {
            return (timeframe ?? "Day") switch
            {
                "Hour" => TimeSpan.FromMinutes(1),
                "Day" => TimeSpan.FromMinutes(15),
                "Week" => TimeSpan.FromHours(1),
                "Month" => TimeSpan.FromHours(4),
                _ => TimeSpan.FromMinutes(15)
            };
        }

        /// <summary>
        /// Truncate a DateTime to the start of its containing bucket for the given timeframe.
        /// </summary>
        internal static DateTime TruncateToBucket(DateTime dt, string? timeframe)
        {
            return (timeframe ?? "Day") switch
            {
                "Hour" => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc),
                "Day" => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (dt.Minute / 15) * 15, 0, DateTimeKind.Utc),
                "Week" => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc),
                "Month" => new DateTime(dt.Year, dt.Month, dt.Day, (dt.Hour / 4) * 4, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (dt.Minute / 15) * 15, 0, DateTimeKind.Utc)
            };
        }

        /// <summary>
        /// Format a bucket DateTime as an ISO 8601 key string: "yyyy-MM-ddTHH:mm".
        /// </summary>
        internal static string FormatBucketKey(DateTime bucket)
        {
            return bucket.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Add URL-based conditions for request type filtering.
        /// </summary>
        internal static void AddTypeConditions(List<string> conditions, string? requestType)
        {
            if (string.IsNullOrEmpty(requestType)) return;

            switch (requestType)
            {
                case "Embedding":
                    conditions.Add("(http_url LIKE '%/process%' OR http_url LIKE '%/embedding%')");
                    break;
                case "Inference":
                    conditions.Add("http_url LIKE '%/completion%'");
                    break;
            }
        }

        /// <summary>
        /// Add an endpoint URL substring filter condition.
        /// </summary>
        internal static void AddEndpointCondition(List<string> conditions, string? endpointFilter, Func<string?, string> sanitize)
        {
            if (!string.IsNullOrEmpty(endpointFilter))
            {
                conditions.Add("http_url LIKE '%" + sanitize(endpointFilter) + "%'");
            }
        }

        /// <summary>
        /// Build a RequestStatisticsResponse from raw request_history rows.
        /// Pre-generates all expected time buckets, then fills counts from raw data.
        /// </summary>
        internal static RequestStatisticsResponse BuildFromRawData(DataTable rawData, string? timeframe)
        {
            DateTime now = DateTime.UtcNow;
            DateTime cutoff = GetCutoff(timeframe);
            TimeSpan step = GetBucketStep(timeframe);

            // Pre-generate all expected bucket keys with zero counts
            Dictionary<string, (long success, long failure)> bucketMap = new Dictionary<string, (long, long)>();
            DateTime cursor = TruncateToBucket(cutoff, timeframe);
            DateTime end = TruncateToBucket(now, timeframe);

            while (cursor <= end)
            {
                bucketMap[FormatBucketKey(cursor)] = (0, 0);
                cursor = cursor.Add(step);
            }

            // Fill from raw data rows
            foreach (DataRow row in rawData.Rows)
            {
                string? createdStr = row["created_utc"]?.ToString();
                if (string.IsNullOrEmpty(createdStr)) continue;

                if (!DateTime.TryParse(createdStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime created))
                    continue;

                created = created.ToUniversalTime();
                string key = FormatBucketKey(TruncateToBucket(created, timeframe));

                bool isSuccess = row["http_status"] != DBNull.Value && Convert.ToInt32(row["http_status"]) < 400;

                if (bucketMap.TryGetValue(key, out var counts))
                {
                    if (isSuccess)
                        bucketMap[key] = (counts.success + 1, counts.failure);
                    else
                        bucketMap[key] = (counts.success, counts.failure + 1);
                }
            }

            // Build response
            RequestStatisticsResponse response = new RequestStatisticsResponse();

            foreach (var kvp in bucketMap.OrderBy(k => k.Key))
            {
                response.Buckets.Add(new RequestStatisticsBucket
                {
                    TimeBucket = kvp.Key,
                    SuccessCount = kvp.Value.success,
                    FailureCount = kvp.Value.failure
                });

                response.TotalSuccess += kvp.Value.success;
                response.TotalFailure += kvp.Value.failure;
            }

            return response;
        }
    }
}
