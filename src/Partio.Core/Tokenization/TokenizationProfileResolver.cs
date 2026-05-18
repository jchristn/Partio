namespace Partio.Core.Tokenization
{
    using System.Collections.Concurrent;
    using System.Text;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using Partio.Core.ThirdParty;
    using SyslogLogging;

    /// <summary>
    /// Resolves tokenization profiles for embedding requests with endpoint-aware fallback behavior.
    /// </summary>
    public class TokenizationProfileResolver
    {
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ConcurrentDictionary<string, CacheEntry> _CapabilityCache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _CacheLifetime;
        private readonly string _Header = "[TokenizationResolver] ";

        /// <summary>
        /// Initialize a new tokenization profile resolver.
        /// </summary>
        public TokenizationProfileResolver(ServerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _CacheLifetime = TimeSpan.FromSeconds(_Settings.TokenizationDefaults.CapabilityCacheTtlSeconds);
        }

        /// <summary>
        /// Resolve the active tokenization profile for an embedding endpoint/model pair.
        /// </summary>
        public async Task<ResolvedTokenizationProfile> ResolveAsync(
            EmbeddingEndpoint endpoint,
            string model,
            EmbeddingClientBase client,
            bool allowCalibration = true,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentNullException(nameof(model));
            if (client == null) throw new ArgumentNullException(nameof(client));

            EndpointTokenizationSettings? overrideSettings = endpoint.Tokenization;
            bool allowDynamicResolution = overrideSettings?.AutoDetect != false;

            bool usedCapabilityData = false;
            bool usedProviderDefault = false;
            bool usedGlobalFallback = false;

            EmbeddingModelCapabilities? capabilities = allowDynamicResolution
                ? await GetCapabilitiesAsync(endpoint, model, client, token).ConfigureAwait(false)
                : null;

            EndpointTokenizationSettings? providerDefaults = ResolveProviderDefaults(endpoint, model, capabilities);
            EndpointTokenizationSettings globalFallback = _Settings.TokenizationDefaults.GlobalFallback;

            TokenizerKindEnum? provisionalTokenizerKind = GetValue(
                overrideSettings?.TokenizerKind,
                capabilities?.TokenizerKind,
                providerDefaults?.TokenizerKind,
                globalFallback.TokenizerKind,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback);

            string provisionalTokenizerModel = GetValue(
                overrideSettings?.TokenizerModel,
                capabilities?.TokenizerModel,
                providerDefaults?.TokenizerModel,
                globalFallback.TokenizerModel,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback) ?? "cl100k_base";

            int provisionalMaxInputTokens = GetValue(
                overrideSettings?.MaxInputTokens,
                capabilities?.MaxInputTokens,
                providerDefaults?.MaxInputTokens,
                globalFallback.MaxInputTokens,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback) ?? 1;

            BatchLimitModeEnum provisionalBatchLimitMode = GetBatchLimitModeValue(
                overrideSettings?.BatchLimitMode,
                capabilities?.BatchLimitMode,
                providerDefaults?.BatchLimitMode,
                globalFallback.BatchLimitMode,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback);

            if (allowDynamicResolution
                && allowCalibration
                && !(overrideSettings?.EffectiveInputBudget.HasValue ?? false)
                && ShouldCalibrate(endpoint, capabilities, provisionalTokenizerKind, provisionalMaxInputTokens))
            {
                await ApplyCalibrationAsync(
                    endpoint,
                    model,
                    client,
                    provisionalTokenizerKind ?? TokenizerKindEnum.Cl100kBase,
                    provisionalTokenizerModel,
                    provisionalMaxInputTokens,
                    provisionalBatchLimitMode,
                    capabilities!,
                    token).ConfigureAwait(false);

                usedCapabilityData = false;
                usedProviderDefault = false;
                usedGlobalFallback = false;
                providerDefaults = ResolveProviderDefaults(endpoint, model, capabilities);
            }

            TokenizerKindEnum? tokenizerKind = GetValue(
                overrideSettings?.TokenizerKind,
                capabilities?.TokenizerKind,
                providerDefaults?.TokenizerKind,
                globalFallback.TokenizerKind,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback);

            string tokenizerModel = GetValue(
                overrideSettings?.TokenizerModel,
                capabilities?.TokenizerModel,
                providerDefaults?.TokenizerModel,
                globalFallback.TokenizerModel,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback) ?? "cl100k_base";

            int maxInputTokens = GetValue(
                overrideSettings?.MaxInputTokens,
                capabilities?.MaxInputTokens,
                providerDefaults?.MaxInputTokens,
                globalFallback.MaxInputTokens,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback) ?? 1;

            int reservedTokens = GetValue(
                overrideSettings?.ReservedInputTokens,
                capabilities?.ReservedInputTokens,
                providerDefaults?.ReservedInputTokens,
                globalFallback.ReservedInputTokens,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback) ?? 0;

            int? effectiveBudget = GetValue(
                overrideSettings?.EffectiveInputBudget,
                capabilities?.EffectiveInputBudget,
                providerDefaults?.EffectiveInputBudget,
                null,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback);

            BatchLimitModeEnum batchLimitMode = GetBatchLimitModeValue(
                overrideSettings?.BatchLimitMode,
                capabilities?.BatchLimitMode,
                providerDefaults?.BatchLimitMode,
                globalFallback.BatchLimitMode,
                ref usedCapabilityData,
                ref usedProviderDefault,
                ref usedGlobalFallback);

            TokenizationProfileSourceEnum profileSource = DetermineProfileSource(
                overrideSettings,
                usedCapabilityData,
                usedProviderDefault,
                capabilities);

            ResolvedTokenizationProfile profile = new ResolvedTokenizationProfile
            {
                TokenizerKind = tokenizerKind ?? TokenizerKindEnum.Cl100kBase,
                TokenizerModel = tokenizerModel,
                MaxInputTokens = Math.Max(1, maxInputTokens),
                ReservedInputTokens = Math.Max(0, reservedTokens),
                BatchLimitMode = batchLimitMode,
                ProfileSource = profileSource,
                UsedFallback = profileSource == TokenizationProfileSourceEnum.ProviderDefault
                    || profileSource == TokenizationProfileSourceEnum.GlobalFallback
            };

            profile.EffectiveInputBudget = effectiveBudget.HasValue
                ? Math.Max(1, Math.Min(profile.MaxInputTokens, effectiveBudget.Value))
                : Math.Max(1, profile.MaxInputTokens - profile.ReservedInputTokens);
            profile.ReservedInputTokens = Math.Max(0, profile.MaxInputTokens - profile.EffectiveInputBudget);
            profile.ProviderMetadata = capabilities?.ProviderMetadata != null
                ? new Dictionary<string, string>(capabilities.ProviderMetadata, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            profile.ProviderMetadata["EndpointId"] = endpoint.Id;
            profile.ProviderMetadata["ApiFormat"] = endpoint.ApiFormat.ToString();
            profile.ProviderMetadata["Model"] = model;
            profile.ProviderMetadata["ResolvedMaxInputTokens"] = profile.MaxInputTokens.ToString();
            profile.ProviderMetadata["ResolvedEffectiveInputBudget"] = profile.EffectiveInputBudget.ToString();
            profile.ProviderMetadata["ResolvedBatchLimitMode"] = profile.BatchLimitMode.ToString();

            if (profile.UsedFallback)
            {
                _Logging.Warn(_Header + "resolved tokenization via " + profile.ProfileSource
                    + " for endpoint " + endpoint.Id
                    + " model " + model
                    + " using tokenizer " + profile.TokenizerKind
                    + " budget " + profile.EffectiveInputBudget);
            }
            else
            {
                _Logging.Debug(_Header + "resolved tokenization via " + profile.ProfileSource
                    + " for endpoint " + endpoint.Id
                    + " model " + model
                    + " using tokenizer " + profile.TokenizerKind
                    + " budget " + profile.EffectiveInputBudget);
            }

            return profile;
        }

        /// <summary>
        /// Invalidate cached capabilities for an endpoint.
        /// </summary>
        public void Invalidate(string endpointId)
        {
            if (string.IsNullOrWhiteSpace(endpointId)) return;

            foreach (string key in _CapabilityCache.Keys)
            {
                if (key.StartsWith(endpointId + "|", StringComparison.OrdinalIgnoreCase))
                    _CapabilityCache.TryRemove(key, out _);
            }
        }

        private async Task<EmbeddingModelCapabilities?> GetCapabilitiesAsync(
            EmbeddingEndpoint endpoint,
            string model,
            EmbeddingClientBase client,
            CancellationToken token)
        {
            string cacheKey = endpoint.Id + "|" + model;
            if (_CapabilityCache.TryGetValue(cacheKey, out CacheEntry? entry))
            {
                if (_CacheLifetime == TimeSpan.Zero || DateTime.UtcNow - entry.CreatedUtc <= _CacheLifetime)
                    return entry.Capabilities;

                _CapabilityCache.TryRemove(cacheKey, out _);
            }

            try
            {
                EmbeddingModelCapabilities? capabilities = await client.GetModelCapabilitiesAsync(model, token).ConfigureAwait(false);
                if (capabilities != null && _CacheLifetime > TimeSpan.Zero)
                {
                    _CapabilityCache[cacheKey] = new CacheEntry
                    {
                        Capabilities = capabilities,
                        CreatedUtc = DateTime.UtcNow
                    };
                }

                return capabilities;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "capability resolution failed for endpoint " + endpoint.Id + " model " + model + ": " + ex.Message);
                return null;
            }
        }

        private EndpointTokenizationSettings? ResolveProviderDefaults(
            EmbeddingEndpoint endpoint,
            string model,
            EmbeddingModelCapabilities? capabilities)
        {
            switch (endpoint.ApiFormat)
            {
                case ApiFormatEnum.OpenAI:
                    return _Settings.TokenizationDefaults.OpenAI;
                case ApiFormatEnum.vLLM:
                    return _Settings.TokenizationDefaults.vLLM;
                case ApiFormatEnum.Gemini:
                    return _Settings.TokenizationDefaults.Gemini;
                case ApiFormatEnum.Ollama:
                    if (IsBertLikeModel(model, capabilities))
                    {
                        int maxTokens = capabilities?.MaxInputTokens ?? 512;
                        int fallbackReservedTokens = capabilities?.ReservedInputTokens
                            ?? _Settings.TokenizationDefaults.Ollama?.ReservedInputTokens
                            ?? 8;
                        int fallbackEffectiveBudget = capabilities?.EffectiveInputBudget
                            ?? Math.Max(1, maxTokens - fallbackReservedTokens);
                        return new EndpointTokenizationSettings
                        {
                            TokenizerKind = TokenizerKindEnum.BertWordPiece,
                            TokenizerModel = "bert-base-uncased",
                            MaxInputTokens = maxTokens,
                            ReservedInputTokens = fallbackReservedTokens,
                            EffectiveInputBudget = fallbackEffectiveBudget,
                            BatchLimitMode = capabilities?.BatchLimitMode == BatchLimitModeEnum.Unknown
                                ? (_Settings.TokenizationDefaults.Ollama?.BatchLimitMode ?? BatchLimitModeEnum.PerInput)
                                : (capabilities?.BatchLimitMode ?? _Settings.TokenizationDefaults.Ollama?.BatchLimitMode ?? BatchLimitModeEnum.PerInput),
                            AutoDetect = true
                        };
                    }

                    return _Settings.TokenizationDefaults.Ollama;
                default:
                    return null;
            }
        }

        private static bool IsBertLikeModel(string model, EmbeddingModelCapabilities? capabilities)
        {
            if (capabilities?.TokenizerKind == TokenizerKindEnum.BertWordPiece)
                return true;

            if (capabilities?.ProviderMetadata != null)
            {
                if (capabilities.ProviderMetadata.TryGetValue("TokenizerFamily", out string? tokenizerFamily)
                    && tokenizerFamily.Equals("bert", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (capabilities.ProviderMetadata.TryGetValue("Architecture", out string? architecture)
                    && architecture.IndexOf("bert", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return model.IndexOf("bert", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("minilm", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("e5", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("gte", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("bge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static TokenizationProfileSourceEnum DetermineProfileSource(
            EndpointTokenizationSettings? overrideSettings,
            bool usedCapabilityData,
            bool usedProviderDefault,
            EmbeddingModelCapabilities? capabilities)
        {
            if (OverrideHasCoreFields(overrideSettings) && !usedCapabilityData && !usedProviderDefault)
                return TokenizationProfileSourceEnum.EndpointOverride;

            if (usedCapabilityData)
                return capabilities?.SourceHint ?? TokenizationProfileSourceEnum.ProviderProbe;

            if (usedProviderDefault)
                return TokenizationProfileSourceEnum.ProviderDefault;

            return TokenizationProfileSourceEnum.GlobalFallback;
        }

        private static bool OverrideHasCoreFields(EndpointTokenizationSettings? settings)
        {
            return settings != null
                && settings.TokenizerKind.HasValue
                && !string.IsNullOrWhiteSpace(settings.TokenizerModel)
                && settings.MaxInputTokens.HasValue;
        }

        private static T? GetValue<T>(
            T? overrideValue,
            T? capabilityValue,
            T? providerDefaultValue,
            T? globalFallbackValue,
            ref bool usedCapabilityData,
            ref bool usedProviderDefault,
            ref bool usedGlobalFallback) where T : struct
        {
            if (overrideValue.HasValue) return overrideValue.Value;
            if (capabilityValue.HasValue)
            {
                usedCapabilityData = true;
                return capabilityValue.Value;
            }
            if (providerDefaultValue.HasValue)
            {
                usedProviderDefault = true;
                return providerDefaultValue.Value;
            }

            usedGlobalFallback = true;
            return globalFallbackValue;
        }

        private static string? GetValue(
            string? overrideValue,
            string? capabilityValue,
            string? providerDefaultValue,
            string? globalFallbackValue,
            ref bool usedCapabilityData,
            ref bool usedProviderDefault,
            ref bool usedGlobalFallback)
        {
            if (!string.IsNullOrWhiteSpace(overrideValue)) return overrideValue;
            if (!string.IsNullOrWhiteSpace(capabilityValue))
            {
                usedCapabilityData = true;
                return capabilityValue;
            }
            if (!string.IsNullOrWhiteSpace(providerDefaultValue))
            {
                usedProviderDefault = true;
                return providerDefaultValue;
            }
            if (!string.IsNullOrWhiteSpace(globalFallbackValue))
            {
                usedGlobalFallback = true;
                return globalFallbackValue;
            }

            return null;
        }

        private static BatchLimitModeEnum GetBatchLimitModeValue(
            BatchLimitModeEnum? overrideValue,
            BatchLimitModeEnum? capabilityValue,
            BatchLimitModeEnum? providerDefaultValue,
            BatchLimitModeEnum? globalFallbackValue,
            ref bool usedCapabilityData,
            ref bool usedProviderDefault,
            ref bool usedGlobalFallback)
        {
            if (overrideValue.HasValue && overrideValue.Value != BatchLimitModeEnum.Unknown)
                return overrideValue.Value;
            if (capabilityValue.HasValue && capabilityValue.Value != BatchLimitModeEnum.Unknown)
            {
                usedCapabilityData = true;
                return capabilityValue.Value;
            }
            if (providerDefaultValue.HasValue && providerDefaultValue.Value != BatchLimitModeEnum.Unknown)
            {
                usedProviderDefault = true;
                return providerDefaultValue.Value;
            }
            if (globalFallbackValue.HasValue && globalFallbackValue.Value != BatchLimitModeEnum.Unknown)
            {
                usedGlobalFallback = true;
                return globalFallbackValue.Value;
            }

            return overrideValue
                ?? capabilityValue
                ?? providerDefaultValue
                ?? globalFallbackValue
                ?? BatchLimitModeEnum.Unknown;
        }

        private static bool ShouldCalibrate(
            EmbeddingEndpoint endpoint,
            EmbeddingModelCapabilities? capabilities,
            TokenizerKindEnum? tokenizerKind,
            int maxInputTokens)
        {
            if (endpoint.Tokenization?.AutoDetect == false) return false;
            if (capabilities == null) return false;
            if (capabilities.EffectiveInputBudget.HasValue && capabilities.BatchLimitMode.HasValue && capabilities.BatchLimitMode.Value != BatchLimitModeEnum.Unknown)
                return false;
            if (capabilities.SourceHint != TokenizationProfileSourceEnum.ProviderProbe) return false;
            if (!tokenizerKind.HasValue) return false;
            if (maxInputTokens <= 1) return false;
            return true;
        }

        private async Task ApplyCalibrationAsync(
            EmbeddingEndpoint endpoint,
            string model,
            EmbeddingClientBase client,
            TokenizerKindEnum tokenizerKind,
            string tokenizerModel,
            int maxInputTokens,
            BatchLimitModeEnum batchLimitMode,
            EmbeddingModelCapabilities capabilities,
            CancellationToken token)
        {
            try
            {
                ResolvedTokenizationProfile provisionalProfile = new ResolvedTokenizationProfile
                {
                    TokenizerKind = tokenizerKind,
                    TokenizerModel = string.IsNullOrWhiteSpace(tokenizerModel) ? "cl100k_base" : tokenizerModel,
                    MaxInputTokens = Math.Max(1, maxInputTokens),
                    ReservedInputTokens = Math.Max(0, capabilities.ReservedInputTokens ?? 0),
                    EffectiveInputBudget = Math.Max(1, maxInputTokens - Math.Max(0, capabilities.ReservedInputTokens ?? 0)),
                    BatchLimitMode = batchLimitMode
                };

                ITokenizerAdapter tokenizer = TokenizerAdapterFactory.Create(provisionalProfile);
                int effectiveBudget = await DiscoverEffectiveInputBudgetAsync(client, model, tokenizer, provisionalProfile.MaxInputTokens, token).ConfigureAwait(false);
                BatchLimitModeEnum discoveredBatchLimitMode = await DiscoverBatchLimitModeAsync(
                    client,
                    model,
                    tokenizer,
                    provisionalProfile.MaxInputTokens,
                    effectiveBudget,
                    provisionalProfile.BatchLimitMode,
                    token).ConfigureAwait(false);

                capabilities.EffectiveInputBudget = Math.Max(1, Math.Min(provisionalProfile.MaxInputTokens, effectiveBudget));
                capabilities.ReservedInputTokens = Math.Max(0, provisionalProfile.MaxInputTokens - capabilities.EffectiveInputBudget.Value);
                capabilities.BatchLimitMode = discoveredBatchLimitMode;
                capabilities.ProviderMetadata["CalibrationApplied"] = bool.TrueString;
                capabilities.ProviderMetadata["CalibrationTimestampUtc"] = DateTime.UtcNow.ToString("o");
                capabilities.ProviderMetadata["CalibrationTokenizerKind"] = tokenizerKind.ToString();
                capabilities.ProviderMetadata["CalibrationTokenizerModel"] = provisionalProfile.TokenizerModel;
                capabilities.ProviderMetadata["CalibrationRawMaxInputTokens"] = provisionalProfile.MaxInputTokens.ToString();
                capabilities.ProviderMetadata["CalibrationEffectiveInputBudget"] = capabilities.EffectiveInputBudget.Value.ToString();
                capabilities.ProviderMetadata["CalibrationReservedInputTokens"] = capabilities.ReservedInputTokens.Value.ToString();
                capabilities.ProviderMetadata["CalibrationBatchLimitMode"] = discoveredBatchLimitMode.ToString();
            }
            catch (Exception ex)
            {
                capabilities.ProviderMetadata["CalibrationApplied"] = bool.FalseString;
                capabilities.ProviderMetadata["CalibrationError"] = ex.Message;
                _Logging.Warn(_Header + "calibration failed for endpoint " + endpoint.Id + " model " + model + ": " + ex.Message);
            }
        }

        private async Task<int> DiscoverEffectiveInputBudgetAsync(
            EmbeddingClientBase client,
            string model,
            ITokenizerAdapter tokenizer,
            int maxInputTokens,
            CancellationToken token)
        {
            int low = 1;
            int high = Math.Max(1, maxInputTokens);
            int best = 1;

            while (low <= high)
            {
                int candidate = low + ((high - low) / 2);
                bool success = await ProbeBatchAsync(
                    client,
                    model,
                    tokenizer,
                    new List<int> { candidate },
                    "CalibrationBudgetProbe",
                    maxInputTokens,
                    maxInputTokens,
                    BatchLimitModeEnum.PerInput,
                    token).ConfigureAwait(false);

                if (success)
                {
                    best = candidate;
                    low = candidate + 1;
                }
                else
                {
                    high = candidate - 1;
                }
            }

            return Math.Max(1, best);
        }

        private async Task<BatchLimitModeEnum> DiscoverBatchLimitModeAsync(
            EmbeddingClientBase client,
            string model,
            ITokenizerAdapter tokenizer,
            int maxInputTokens,
            int effectiveInputBudget,
            BatchLimitModeEnum fallbackMode,
            CancellationToken token)
        {
            int candidateTokens = Math.Min(effectiveInputBudget, Math.Max(1, (maxInputTokens / 2) + 1));
            if ((candidateTokens * 2) <= maxInputTokens)
                return fallbackMode;

            bool success = await ProbeBatchAsync(
                client,
                model,
                tokenizer,
                new List<int> { candidateTokens, candidateTokens },
                "CalibrationBatchModeProbe",
                effectiveInputBudget,
                maxInputTokens,
                BatchLimitModeEnum.Unknown,
                token).ConfigureAwait(false);

            return success ? BatchLimitModeEnum.PerInput : BatchLimitModeEnum.WholeRequest;
        }

        private async Task<bool> ProbeBatchAsync(
            EmbeddingClientBase client,
            string model,
            ITokenizerAdapter tokenizer,
            List<int> inputTokenCounts,
            string purpose,
            int effectiveInputBudget,
            int maxInputTokens,
            BatchLimitModeEnum batchLimitMode,
            CancellationToken token)
        {
            List<string> inputs = inputTokenCounts.Select(count => BuildExactTokenText(tokenizer, count)).ToList();
            int existingCallCount = client.CallDetails.Count;

            try
            {
                await client.EmbedBatchAsync(inputs, model, token).ConfigureAwait(false);
                AnnotateCallDetails(
                    client,
                    existingCallCount,
                    purpose,
                    tokenizer,
                    inputs,
                    effectiveInputBudget,
                    maxInputTokens,
                    batchLimitMode,
                    null);
                return true;
            }
            catch
            {
                AnnotateCallDetails(
                    client,
                    existingCallCount,
                    purpose,
                    tokenizer,
                    inputs,
                    effectiveInputBudget,
                    maxInputTokens,
                    batchLimitMode,
                    "Calibration probe rejected by upstream endpoint.");
                return false;
            }
        }

        private static string BuildExactTokenText(ITokenizerAdapter tokenizer, int tokenCount)
        {
            if (tokenCount <= 0) return string.Empty;

            StringBuilder builder = new StringBuilder();
            string candidate = string.Empty;

            while (tokenizer.CountTokens(candidate) < tokenCount)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append("calibrationtoken");
                candidate = tokenizer.SliceByTokenRange(builder.ToString(), 0, tokenCount).Trim();
            }

            return candidate;
        }

        private static void AnnotateCallDetails(
            EmbeddingClientBase client,
            int startIndex,
            string purpose,
            ITokenizerAdapter tokenizer,
            List<string> inputs,
            int effectiveInputBudget,
            int maxInputTokens,
            BatchLimitModeEnum batchLimitMode,
            string? failureHint)
        {
            if (client.CallDetails.Count <= startIndex) return;

            List<EmbeddingCallInputDetail> inputDetails = inputs.Select((input, index) =>
            {
                int tokenCount = tokenizer.CountTokens(input);
                return new EmbeddingCallInputDetail
                {
                    Index = index,
                    CharacterCount = input.Length,
                    TokenCount = tokenCount,
                    ExceedsEffectiveInputBudget = tokenCount > effectiveInputBudget,
                    Preview = BuildPreview(input)
                };
            }).ToList();

            List<int> failedInputIndices = inputDetails
                .Where(detail => detail.ExceedsEffectiveInputBudget)
                .Select(detail => detail.Index)
                .ToList();

            for (int i = startIndex; i < client.CallDetails.Count; i++)
            {
                EmbeddingCallDetail detail = client.CallDetails[i];
                detail.Purpose = purpose;
                detail.Inputs = inputDetails;
                detail.BatchTokenCount = inputDetails.Sum(d => d.TokenCount);
                detail.EffectiveInputBudget = effectiveInputBudget;
                detail.MaxInputTokens = maxInputTokens;
                detail.BatchLimitMode = batchLimitMode;
                detail.FailedInputIndices = failedInputIndices.Count > 0 ? failedInputIndices : null;
                detail.FailureReasonHint = failureHint;
            }
        }

        private static string BuildPreview(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            const int maxLength = 160;
            string flattened = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return flattened.Length <= maxLength ? flattened : flattened.Substring(0, maxLength);
        }

        private sealed class CacheEntry
        {
            public EmbeddingModelCapabilities? Capabilities { get; set; }
            public DateTime CreatedUtc { get; set; }
        }
    }
}
