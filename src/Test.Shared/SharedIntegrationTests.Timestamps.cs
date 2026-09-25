namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    /// <summary>
    /// Server-set timestamp cases. CreatedUtc and LastUpdateUtc are owned by the server: values a caller
    /// supplies on create or update are ignored. These cases use raw HTTP so they can send forged
    /// timestamps regardless of what the SDK models default to.
    /// </summary>
    public static partial class SharedIntegrationTests
    {
        private static readonly DateTime _ForgedCreatedUtc = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        private static readonly DateTime _ForgedUpdatedUtc = new DateTime(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        private static readonly TimeSpan _ClockTolerance = TimeSpan.FromMinutes(2);
        private static readonly HttpClient _TimestampHttp = new HttpClient();

        /// <summary>
        /// For every resource: create with forged timestamps, verify the server stamped both; update with
        /// forged timestamps (GET-modify-PUT), verify CreatedUtc is unchanged and LastUpdateUtc advanced.
        /// </summary>
        public static async Task TestServerSetTimestampsAsync()
        {
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);

            string tenantId = await VerifyTimestampLifecycleAsync("/v1.0/tenants", new JsonObject { ["Name"] = "ts-tenant-" + suffix }).ConfigureAwait(false);
            string userId = await VerifyTimestampLifecycleAsync("/v1.0/users", new JsonObject
            {
                ["TenantId"] = "default",
                ["Email"] = "ts-" + suffix + "@example.com",
                ["Password"] = "password"
            }).ConfigureAwait(false);
            string credentialId = await VerifyTimestampLifecycleAsync("/v1.0/credentials", new JsonObject
            {
                ["TenantId"] = "default",
                ["UserId"] = userId,
                ["Name"] = "ts-credential-" + suffix
            }).ConfigureAwait(false);
            string embeddingId = await VerifyTimestampLifecycleAsync("/v1.0/endpoints/embedding", new JsonObject
            {
                ["TenantId"] = "default",
                ["Name"] = "ts-embedding-" + suffix,
                ["Endpoint"] = "http://127.0.0.1:1",
                ["ApiFormat"] = "Ollama",
                ["Model"] = "ts-model"
            }).ConfigureAwait(false);
            string completionId = await VerifyTimestampLifecycleAsync("/v1.0/endpoints/completion", new JsonObject
            {
                ["TenantId"] = "default",
                ["Name"] = "ts-completion-" + suffix,
                ["Endpoint"] = "http://127.0.0.1:1",
                ["ApiFormat"] = "Ollama",
                ["Model"] = "ts-model"
            }).ConfigureAwait(false);

            (await SendAdminAsync(HttpMethod.Delete, "/v1.0/endpoints/completion/" + completionId, null).ConfigureAwait(false)).Dispose();
            (await SendAdminAsync(HttpMethod.Delete, "/v1.0/endpoints/embedding/" + embeddingId, null).ConfigureAwait(false)).Dispose();
            (await SendAdminAsync(HttpMethod.Delete, "/v1.0/credentials/" + credentialId, null).ConfigureAwait(false)).Dispose();
            (await SendAdminAsync(HttpMethod.Delete, "/v1.0/users/" + userId, null).ConfigureAwait(false)).Dispose();
            (await SendAdminAsync(HttpMethod.Delete, "/v1.0/tenants/" + tenantId, null).ConfigureAwait(false)).Dispose();
        }

        /// <summary>
        /// A create that omits the timestamps entirely still gets server-set values.
        /// </summary>
        public static async Task TestServerSetTimestampsWhenOmittedAsync()
        {
            DateTime before = DateTime.UtcNow;
            JsonObject created = await SendAdminJsonAsync(HttpMethod.Put, "/v1.0/endpoints/completion", new JsonObject
            {
                ["TenantId"] = "default",
                ["Name"] = "ts-omitted-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ["Endpoint"] = "http://127.0.0.1:1",
                ["Model"] = "ts-model"
            }).ConfigureAwait(false);
            DateTime after = DateTime.UtcNow;

            string id = created["Id"]!.GetValue<string>();
            try
            {
                AssertServerTime(created, "CreatedUtc", before, after, "completion create (omitted)");
                AssertServerTime(created, "LastUpdateUtc", before, after, "completion create (omitted)");
            }
            finally
            {
                (await SendAdminAsync(HttpMethod.Delete, "/v1.0/endpoints/completion/" + id, null).ConfigureAwait(false)).Dispose();
            }
        }

        /// <summary>
        /// Updating a record that does not exist returns 404 for every resource instead of echoing the body.
        /// </summary>
        public static async Task TestUpdateMissingResourceReturns404Async()
        {
            string missing = "missing-" + Guid.NewGuid().ToString("N");
            foreach (string path in new[] { "/v1.0/tenants", "/v1.0/users", "/v1.0/credentials", "/v1.0/endpoints/embedding", "/v1.0/endpoints/completion" })
            {
                JsonObject body = new JsonObject
                {
                    ["TenantId"] = "default",
                    ["Name"] = "missing",
                    ["Email"] = "missing@example.com",
                    ["Endpoint"] = "http://127.0.0.1:1",
                    ["Model"] = "m",
                    ["CreatedUtc"] = _ForgedCreatedUtc.ToString("o")
                };

                using HttpResponseMessage response = await SendAdminAsync(HttpMethod.Put, path + "/" + missing, body).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.NotFound)
                    throw new Exception("PUT " + path + "/{missing} should return 404 but returned " + (int)response.StatusCode);
            }
        }

        private static async Task<string> VerifyTimestampLifecycleAsync(string path, JsonObject body)
        {
            body["CreatedUtc"] = _ForgedCreatedUtc.ToString("o");
            body["LastUpdateUtc"] = _ForgedUpdatedUtc.ToString("o");

            DateTime beforeCreate = DateTime.UtcNow;
            JsonObject created = await SendAdminJsonAsync(HttpMethod.Put, path, body).ConfigureAwait(false);
            DateTime afterCreate = DateTime.UtcNow;

            string id = created["Id"]!.GetValue<string>();
            AssertServerTime(created, "CreatedUtc", beforeCreate, afterCreate, path + " create response");
            AssertServerTime(created, "LastUpdateUtc", beforeCreate, afterCreate, path + " create response");

            JsonObject stored = await SendAdminJsonAsync(HttpMethod.Get, path + "/" + id, null).ConfigureAwait(false);
            DateTime storedCreated = ReadUtc(stored, "CreatedUtc");
            AssertClose(ReadUtc(created, "CreatedUtc"), storedCreated, path + " stored CreatedUtc should match the create response");

            // GET-modify-PUT with forged timestamps: the server must keep CreatedUtc and stamp LastUpdateUtc.
            await Task.Delay(50).ConfigureAwait(false);
            stored["CreatedUtc"] = _ForgedCreatedUtc.ToString("o");
            stored["LastUpdateUtc"] = _ForgedUpdatedUtc.ToString("o");
            if (stored.ContainsKey("Name")) stored["Name"] = (stored["Name"]?.GetValue<string>() ?? "") + "-updated";

            DateTime beforeUpdate = DateTime.UtcNow;
            JsonObject updated = await SendAdminJsonAsync(HttpMethod.Put, path + "/" + id, stored).ConfigureAwait(false);
            DateTime afterUpdate = DateTime.UtcNow;

            AssertClose(storedCreated, ReadUtc(updated, "CreatedUtc"), path + " update response must keep the stored CreatedUtc");
            AssertServerTime(updated, "LastUpdateUtc", beforeUpdate, afterUpdate, path + " update response");

            JsonObject reread = await SendAdminJsonAsync(HttpMethod.Get, path + "/" + id, null).ConfigureAwait(false);
            AssertClose(storedCreated, ReadUtc(reread, "CreatedUtc"), path + " CreatedUtc must not change after update");
            AssertServerTime(reread, "LastUpdateUtc", beforeUpdate, afterUpdate, path + " stored LastUpdateUtc after update");
            if (ReadUtc(reread, "LastUpdateUtc") < ReadUtc(reread, "CreatedUtc"))
                throw new Exception(path + " LastUpdateUtc should not precede CreatedUtc.");

            return id;
        }

        private static void AssertServerTime(JsonObject json, string property, DateTime before, DateTime after, string context)
        {
            DateTime value = ReadUtc(json, property);
            if (value < before - _ClockTolerance || value > after + _ClockTolerance)
                throw new Exception(context + ": " + property + " should be server-set near now but was " + value.ToString("o") + " (window " + before.ToString("o") + " .. " + after.ToString("o") + ").");
        }

        private static void AssertClose(DateTime expected, DateTime actual, string message)
        {
            if ((expected - actual).Duration() > TimeSpan.FromSeconds(1))
                throw new Exception(message + ": expected " + expected.ToString("o") + " but got " + actual.ToString("o") + ".");
        }

        private static DateTime ReadUtc(JsonObject json, string property)
        {
            JsonNode? node = json[property];
            if (node == null) throw new Exception("Response is missing " + property + ": " + json.ToJsonString());
            return DateTime.Parse(node.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        }

        private static async Task<JsonObject> SendAdminJsonAsync(HttpMethod method, string path, JsonObject? body)
        {
            using HttpResponseMessage response = await SendAdminAsync(method, path, body).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new Exception(method + " " + path + " returned " + (int)response.StatusCode + ": " + text);
            return JsonNode.Parse(text)?.AsObject() ?? throw new Exception(method + " " + path + " returned no JSON object.");
        }

        private static async Task<HttpResponseMessage> SendAdminAsync(HttpMethod method, string path, JsonObject? body)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, _Endpoint.TrimEnd('/') + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _AdminKey);
            if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            return await _TimestampHttp.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
        }
    }
}
