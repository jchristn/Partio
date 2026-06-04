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
        private static readonly string[] CalibrationProbeCorpora = new[]
        {
            "Calibration sample 01. The quick brown fox reviews clinical notes, numbered steps, and percentages like 15.2% or 1 - 3 days. URLs such as https://example.test/path?a=1 are included alongside dosage references like 100 U/10 mL and section numbers 4.2.",
            "PDF-style sample with symbols: \u00AE BOTOX, 2 \u00B0C to 8 \u00B0C, bullets \u2022 one \u2022 two, ranges 256 - 295 days, parentheses (U), slashes, commas, hyphenated terms, and mixed CASE headings.\nNEW ZEALAND DATA SHEET\nPage 1 of 37.",
            "1. Initial dose: 1.25 U to 2.5 U. 2. Follow-up: re-examine 7 - 14 days later. A | B | C columns, itemized lists, quoted text like \"booster\" injections, and labels such as adverse-event, post-treatment, and over-active-bladder are included."
        };

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

            EndpointTokenizationSettings? overrideSettings = NormalizeOverrideSettings(endpoint.Tokenization);
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
                                ? (_Settings.TokenizationDefaults.Ollama?.BatchLimitMode ?? BatchLimitModeEnum.WholeRequest)
                                : (capabilities?.BatchLimitMode ?? _Settings.TokenizationDefaults.Ollama?.BatchLimitMode ?? BatchLimitModeEnum.WholeRequest),
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

        private static EndpointTokenizationSettings? NormalizeOverrideSettings(EndpointTokenizationSettings? settings)
        {
            if (settings == null) return null;

            EndpointTokenizationSettings normalized = new EndpointTokenizationSettings
            {
                TokenizerKind = settings.TokenizerKind,
                TokenizerModel = settings.TokenizerModel,
                MaxInputTokens = settings.MaxInputTokens,
                ReservedInputTokens = settings.ReservedInputTokens,
                EffectiveInputBudget = settings.EffectiveInputBudget,
                BatchLimitMode = settings.BatchLimitMode,
                AutoDetect = settings.AutoDetect
            };

            if (normalized.MaxInputTokens.HasValue)
            {
                if (!normalized.EffectiveInputBudget.HasValue && normalized.ReservedInputTokens.HasValue)
                {
                    normalized.EffectiveInputBudget = Math.Max(1, normalized.MaxInputTokens.Value - normalized.ReservedInputTokens.Value);
                }
                else if (normalized.EffectiveInputBudget.HasValue && !normalized.ReservedInputTokens.HasValue)
                {
                    normalized.ReservedInputTokens = Math.Max(0, normalized.MaxInputTokens.Value - normalized.EffectiveInputBudget.Value);
                }
            }

            return normalized;
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
                bool success = await ProbeBudgetAcrossCorporaAsync(
                    client,
                    model,
                    tokenizer,
                    candidate,
                    maxInputTokens,
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
            int candidateTokens = Math.Max(1, effectiveInputBudget);
            List<string> probeInputs = BuildCalibrationProbeTexts(tokenizer, candidateTokens)
                .Take(2)
                .ToList();

            if (probeInputs.Count < 2)
                return fallbackMode;

            bool success = await ProbeInputsAsync(
                client,
                model,
                tokenizer,
                probeInputs,
                "CalibrationBatchModeProbe",
                effectiveInputBudget,
                maxInputTokens,
                BatchLimitModeEnum.Unknown,
                token).ConfigureAwait(false);

            return success ? BatchLimitModeEnum.PerInput : BatchLimitModeEnum.WholeRequest;
        }

        private async Task<bool> ProbeBudgetAcrossCorporaAsync(
            EmbeddingClientBase client,
            string model,
            ITokenizerAdapter tokenizer,
            int candidateTokens,
            int maxInputTokens,
            CancellationToken token)
        {
            List<string> probeInputs = BuildCalibrationProbeTexts(tokenizer, candidateTokens);
            for (int i = 0; i < probeInputs.Count; i++)
            {
                bool success = await ProbeInputsAsync(
                    client,
                    model,
                    tokenizer,
                    new List<string> { probeInputs[i] },
                    "CalibrationBudgetProbe#" + (i + 1),
                    candidateTokens,
                    maxInputTokens,
                    BatchLimitModeEnum.PerInput,
                    token).ConfigureAwait(false);

                if (!success)
                    return false;
            }

            return true;
        }

        private async Task<bool> ProbeInputsAsync(
            EmbeddingClientBase client,
            string model,
            ITokenizerAdapter tokenizer,
            List<string> inputs,
            string purpose,
            int effectiveInputBudget,
            int maxInputTokens,
            BatchLimitModeEnum batchLimitMode,
            CancellationToken token)
        {
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

        private static List<string> BuildCalibrationProbeTexts(ITokenizerAdapter tokenizer, int tokenCount)
        {
            List<string> texts = new List<string>();
            foreach (string corpus in CalibrationProbeCorpora)
            {
                string text = BuildCalibrationTextFromCorpus(tokenizer, tokenCount, corpus);
                if (!string.IsNullOrWhiteSpace(text))
                    texts.Add(text);
            }

            if (texts.Count < 1)
                texts.Add(BuildCalibrationTextFromCorpus(tokenizer, tokenCount, "calibrationtoken"));

            return texts
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string BuildCalibrationTextFromCorpus(ITokenizerAdapter tokenizer, int tokenCount, string corpus)
        {
            if (tokenCount <= 0) return string.Empty;
            if (string.IsNullOrWhiteSpace(corpus)) corpus = "calibrationtoken";

            StringBuilder builder = new StringBuilder();
            int builderTokenCount = 0;
            int maxIterations = Math.Max(8, tokenCount * 2);

            for (int i = 0; i < maxIterations && builderTokenCount < tokenCount; i++)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(corpus);
                builderTokenCount = tokenizer.CountTokens(builder.ToString());
            }

            string source = builder.Length > 0 ? builder.ToString() : corpus;
            string candidate = tokenizer.SliceByTokenRange(source, 0, tokenCount).Trim();
            candidate = PadCalibrationTextToTokenCount(tokenizer, candidate, tokenCount);
            return TrimCalibrationTextToTokenCount(tokenizer, candidate, tokenCount);
        }

        private static string PadCalibrationTextToTokenCount(ITokenizerAdapter tokenizer, string text, int tokenCount)
        {
            string candidate = text ?? string.Empty;
            int currentTokenCount = tokenizer.CountTokens(candidate);
            if (currentTokenCount >= tokenCount) return candidate;

            string[] paddingTerms = new[] { "a", "the", "sample", "data", "token", "calibration" };
            int maxIterations = Math.Max(32, tokenCount * 4);

            for (int i = 0; i < maxIterations && currentTokenCount < tokenCount; i++)
            {
                bool advanced = false;

                foreach (string term in paddingTerms)
                {
                    string next = string.IsNullOrWhiteSpace(candidate)
                        ? term
                        : candidate + " " + term;
                    int nextTokenCount = tokenizer.CountTokens(next);

                    if (nextTokenCount > currentTokenCount && nextTokenCount <= tokenCount)
                    {
                        candidate = next;
                        currentTokenCount = nextTokenCount;
                        advanced = true;
                        break;
                    }
                }

                if (!advanced)
                    break;
            }

            return candidate;
        }

        private static string TrimCalibrationTextToTokenCount(ITokenizerAdapter tokenizer, string text, int tokenCount)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            int currentTokenCount = tokenizer.CountTokens(text);
            if (currentTokenCount <= tokenCount) return text;

            IReadOnlyList<int> tokenIds = tokenizer.Encode(text);
            int take = Math.Min(tokenCount, tokenIds.Count);

            while (take > 0)
            {
                string candidate = tokenizer.Decode(tokenIds.Take(take)).Trim();
                if (tokenizer.CountTokens(candidate) <= tokenCount)
                    return candidate;

                take--;
            }

            return string.Empty;
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
            IReadOnlyList<EmbeddingCallDetail> callDetails = client.CallDetails;
            if (callDetails.Count <= startIndex) return;

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

            for (int i = startIndex; i < callDetails.Count; i++)
            {
                EmbeddingCallDetail detail = callDetails[i];
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
