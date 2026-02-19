namespace Partio.Core.Summarization
{
    using System.Text.RegularExpressions;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.ThirdParty;
    using SyslogLogging;

    /// <summary>
    /// Engine for generating summaries of semantic cells using completion APIs.
    /// Ported from View's ViewSummarizer, adapted to Partio's model types.
    /// </summary>
    public class SummarizationEngine
    {
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[SummarizationEngine] ";

        /// <summary>
        /// Initialize a new SummarizationEngine.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        public SummarizationEngine(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Summarize the given cells according to the provided configuration.
        /// Returns the same cells with summary children injected.
        /// </summary>
        /// <param name="cells">Input cells (may be flat, hierarchical, or mixed).</param>
        /// <param name="config">Summarization configuration.</param>
        /// <param name="completionClient">Completion client to use for generation.</param>
        /// <param name="model">Model identifier to use for completion.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Cells with summary children injected.</returns>
        public async Task<List<SemanticCellRequest>> SummarizeAsync(
            List<SemanticCellRequest> cells,
            SummarizationConfiguration config,
            CompletionClientBase completionClient,
            string model,
            CancellationToken token = default)
        {
            if (cells == null || cells.Count == 0) return cells ?? new List<SemanticCellRequest>();
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (completionClient == null) throw new ArgumentNullException(nameof(completionClient));

            // Normalize to hierarchical form
            List<SemanticCellRequest> rootCells = Deflatten(cells);

            // Global failure counter (thread-safe)
            FailureCounter globalFailures = new FailureCounter();

            if (config.Order == SummarizationOrderEnum.BottomUp)
            {
                await ProcessBottomUpAsync(rootCells, config, completionClient, model, globalFailures, token).ConfigureAwait(false);
            }
            else
            {
                await ProcessTopDownAsync(rootCells, config, completionClient, model, globalFailures, token).ConfigureAwait(false);
            }

            return rootCells;
        }

        #region Hierarchy Helpers

        /// <summary>
        /// Convert a flat cell list to a tree using ParentGUID relationships.
        /// Handles mixed input where some cells use Children and some use ParentGUID.
        /// </summary>
        /// <param name="cells">Flat or mixed cell list.</param>
        /// <returns>Root-level cells with hierarchy built.</returns>
        public static List<SemanticCellRequest> Deflatten(List<SemanticCellRequest> cells)
        {
            if (cells == null || cells.Count == 0) return new List<SemanticCellRequest>();

            // Build lookup table
            Dictionary<Guid, SemanticCellRequest> lookup = new Dictionary<Guid, SemanticCellRequest>();
            foreach (SemanticCellRequest cell in cells)
            {
                lookup[cell.GUID] = cell;
            }

            // Also index all pre-existing children recursively
            foreach (SemanticCellRequest cell in cells)
            {
                IndexChildrenRecursive(cell, lookup);
            }

            // Build parent-child relationships from ParentGUID
            List<SemanticCellRequest> roots = new List<SemanticCellRequest>();

            foreach (SemanticCellRequest cell in cells)
            {
                if (cell.ParentGUID.HasValue && lookup.ContainsKey(cell.ParentGUID.Value))
                {
                    SemanticCellRequest parent = lookup[cell.ParentGUID.Value];
                    if (parent.Children == null) parent.Children = new List<SemanticCellRequest>();

                    // Avoid duplicates
                    if (!parent.Children.Any(c => c.GUID == cell.GUID))
                    {
                        parent.Children.Add(cell);
                    }
                }
                else if (!cell.ParentGUID.HasValue)
                {
                    // Check if this cell is already a child of another cell via Children
                    bool isChild = false;
                    foreach (SemanticCellRequest other in cells)
                    {
                        if (other.GUID != cell.GUID && other.Children != null && other.Children.Any(c => c.GUID == cell.GUID))
                        {
                            isChild = true;
                            break;
                        }
                    }

                    if (!isChild)
                    {
                        roots.Add(cell);
                    }
                }
            }

            // If no roots found (all cells have parents not in the collection), return original
            if (roots.Count == 0)
            {
                return cells;
            }

            return roots;
        }

        /// <summary>
        /// Convert a hierarchical cell tree to a flat list.
        /// Recursively walks the tree collecting all cells.
        /// </summary>
        /// <param name="cells">Root-level cells.</param>
        /// <returns>Flat list of all cells including children.</returns>
        public static List<SemanticCellRequest> Flatten(List<SemanticCellRequest> cells)
        {
            List<SemanticCellRequest> flat = new List<SemanticCellRequest>();
            if (cells == null) return flat;

            foreach (SemanticCellRequest cell in cells)
            {
                FlattenRecursive(cell, flat);
            }

            return flat;
        }

        /// <summary>
        /// Organize cells into groups by depth level.
        /// </summary>
        /// <param name="cells">Root-level cells.</param>
        /// <returns>Dictionary mapping depth level to cells at that level.</returns>
        public static Dictionary<int, List<SemanticCellRequest>> GetCellsByDepthLevel(List<SemanticCellRequest> cells)
        {
            Dictionary<int, List<SemanticCellRequest>> levels = new Dictionary<int, List<SemanticCellRequest>>();
            if (cells == null) return levels;

            foreach (SemanticCellRequest cell in cells)
            {
                GetCellsByDepthLevelRecursive(cell, 0, levels);
            }

            return levels;
        }

        /// <summary>
        /// Find a cell by GUID in a hierarchy.
        /// </summary>
        /// <param name="cells">Root-level cells to search.</param>
        /// <param name="guid">GUID to find.</param>
        /// <returns>The found cell, or null.</returns>
        public static SemanticCellRequest? FindCell(List<SemanticCellRequest> cells, Guid guid)
        {
            if (cells == null) return null;

            foreach (SemanticCellRequest cell in cells)
            {
                if (cell.GUID == guid) return cell;
                if (cell.Children != null)
                {
                    SemanticCellRequest? found = FindCell(cell.Children, guid);
                    if (found != null) return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Extract text content from a cell regardless of type.
        /// </summary>
        /// <param name="cell">The cell to extract content from.</param>
        /// <returns>Text content as a string.</returns>
        public static string GetCellContent(SemanticCellRequest cell)
        {
            if (cell == null) return string.Empty;

            if (!string.IsNullOrEmpty(cell.Text)) return cell.Text;

            if (cell.UnorderedList != null && cell.UnorderedList.Count > 0)
                return string.Join("\n", cell.UnorderedList);

            if (cell.OrderedList != null && cell.OrderedList.Count > 0)
                return string.Join("\n", cell.OrderedList);

            if (cell.Table != null && cell.Table.Count > 0)
            {
                List<string> rows = new List<string>();
                foreach (List<string> row in cell.Table)
                {
                    rows.Add(string.Join(" | ", row));
                }
                return string.Join("\n", rows);
            }

            if (cell.Binary != null && cell.Binary.Length > 0)
                return Convert.ToBase64String(cell.Binary);

            return string.Empty;
        }

        #endregion

        #region Processing

        private async Task ProcessBottomUpAsync(
            List<SemanticCellRequest> rootCells,
            SummarizationConfiguration config,
            CompletionClientBase completionClient,
            string model,
            FailureCounter globalFailures,
            CancellationToken token)
        {
            Dictionary<int, List<SemanticCellRequest>> levels = GetCellsByDepthLevel(rootCells);
            if (levels.Count == 0) return;

            // Process from deepest level to root
            int maxDepth = levels.Keys.Max();
            for (int depth = maxDepth; depth >= 0; depth--)
            {
                if (!levels.ContainsKey(depth)) continue;

                List<SemanticCellRequest> cellsAtLevel = levels[depth];
                await ProcessCellsInParallelAsync(cellsAtLevel, config, completionClient, model, globalFailures, isBottomUp: true, rootCells, token).ConfigureAwait(false);
            }
        }

        private async Task ProcessTopDownAsync(
            List<SemanticCellRequest> rootCells,
            SummarizationConfiguration config,
            CompletionClientBase completionClient,
            string model,
            FailureCounter globalFailures,
            CancellationToken token)
        {
            Dictionary<int, List<SemanticCellRequest>> levels = GetCellsByDepthLevel(rootCells);
            if (levels.Count == 0) return;

            // Process from root to deepest level
            int maxDepth = levels.Keys.Max();
            for (int depth = 0; depth <= maxDepth; depth++)
            {
                if (!levels.ContainsKey(depth)) continue;

                List<SemanticCellRequest> cellsAtLevel = levels[depth];
                await ProcessCellsInParallelAsync(cellsAtLevel, config, completionClient, model, globalFailures, isBottomUp: false, rootCells, token).ConfigureAwait(false);
            }
        }

        private async Task ProcessCellsInParallelAsync(
            List<SemanticCellRequest> cells,
            SummarizationConfiguration config,
            CompletionClientBase completionClient,
            string model,
            FailureCounter globalFailures,
            bool isBottomUp,
            List<SemanticCellRequest> rootCells,
            CancellationToken token)
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(config.MaxParallelTasks);
            List<Task> tasks = new List<Task>();

            foreach (SemanticCellRequest cell in cells)
            {
                // Skip summary cells (pre-summarized by client)
                if (cell.Type == AtomTypeEnum.Summary) continue;

                // Skip cells with insufficient content
                string content = GetCellContent(cell);
                if (content.Length < config.MinCellLength) continue;

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        // Check global failure limit
                        if (globalFailures.HasReachedLimit(config.MaxRetries))
                        {
                            throw new InvalidOperationException("Summarization failed: global retry limit exceeded (" + config.MaxRetries + ")");
                        }

                        await SummarizeCellAsync(cell, config, completionClient, model, globalFailures, isBottomUp, rootCells, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task SummarizeCellAsync(
            SemanticCellRequest cell,
            SummarizationConfiguration config,
            CompletionClientBase completionClient,
            string model,
            FailureCounter globalFailures,
            bool isBottomUp,
            List<SemanticCellRequest> rootCells,
            CancellationToken token)
        {
            string content = GetCellContent(cell);
            string context = BuildContext(cell, isBottomUp, rootCells);

            string prompt = config.SummarizationPrompt
                .Replace("{tokens}", config.MaxSummaryTokens.ToString())
                .Replace("{content}", content)
                .Replace("{context}", context);

            for (int attempt = 0; attempt <= config.MaxRetriesPerSummary; attempt++)
            {
                // Check global failure limit
                if (globalFailures.HasReachedLimit(config.MaxRetries))
                {
                    throw new InvalidOperationException("Summarization failed: global retry limit exceeded (" + config.MaxRetries + ")");
                }

                try
                {
                    string? summary = await completionClient.GenerateCompletionAsync(
                        prompt,
                        model,
                        config.MaxSummaryTokens,
                        config.TimeoutMs,
                        token,
                        SummarizationConfiguration.DefaultSystemPrompt).ConfigureAwait(false);

                    // Check for valid response
                    if (!string.IsNullOrWhiteSpace(summary) &&
                        !summary.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                        !summary.Equals("\"None\"", StringComparison.OrdinalIgnoreCase))
                    {
                        summary = CleanSummaryText(summary);

                        // Re-check after cleaning
                        if (string.IsNullOrWhiteSpace(summary) ||
                            summary.Equals("None", StringComparison.OrdinalIgnoreCase))
                        {
                            _Logging.Debug(_Header + "cell " + cell.GUID + " summary was empty after cleaning, skipping");
                            return;
                        }

                        // Create summary child cell
                        SemanticCellRequest summaryCell = new SemanticCellRequest();
                        summaryCell.GUID = Guid.NewGuid();
                        summaryCell.ParentGUID = cell.GUID;
                        summaryCell.Type = AtomTypeEnum.Summary;
                        summaryCell.Text = summary;
                        summaryCell.ChunkingConfiguration = cell.ChunkingConfiguration;
                        summaryCell.EmbeddingConfiguration = cell.EmbeddingConfiguration;

                        if (cell.Children == null) cell.Children = new List<SemanticCellRequest>();
                        cell.Children.Add(summaryCell);

                        _Logging.Debug(_Header + "generated summary for cell " + cell.GUID + " (" + summary.Length + " chars)");
                        return; // Success
                    }

                    // "None" or empty response — skip, no child created
                    _Logging.Debug(_Header + "cell " + cell.GUID + " returned None/empty summary, skipping");
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw; // Don't retry on external cancellation
                }
                catch (InvalidOperationException)
                {
                    throw; // Global failure limit — propagate
                }
                catch (Exception ex)
                {
                    globalFailures.Increment();
                    _Logging.Warn(_Header + "summarization attempt " + (attempt + 1) + " failed for cell " + cell.GUID + ": " + ex.Message);

                    if (attempt >= config.MaxRetriesPerSummary)
                    {
                        _Logging.Warn(_Header + "exhausted per-cell retries for cell " + cell.GUID);
                        return; // Give up on this cell
                    }
                }
            }
        }

        private string BuildContext(SemanticCellRequest cell, bool isBottomUp, List<SemanticCellRequest> rootCells)
        {
            List<string> contextParts = new List<string>();

            if (isBottomUp)
            {
                // Bottom-up: collect child content and child summaries as context
                if (cell.Children != null)
                {
                    foreach (SemanticCellRequest child in cell.Children)
                    {
                        if (child.Type == AtomTypeEnum.Summary)
                        {
                            contextParts.Add("Child summary: " + GetCellContent(child));
                        }
                        else
                        {
                            string childContent = GetCellContent(child);
                            if (!string.IsNullOrEmpty(childContent))
                            {
                                contextParts.Add("Child content: " + childContent);
                            }
                        }
                    }
                }
            }
            else
            {
                // Top-down: use parent content and parent summaries as context
                if (cell.ParentGUID.HasValue)
                {
                    SemanticCellRequest? parent = FindCell(rootCells, cell.ParentGUID.Value);
                    if (parent != null)
                    {
                        string parentContent = GetCellContent(parent);
                        if (!string.IsNullOrEmpty(parentContent))
                        {
                            contextParts.Add("Parent content: " + parentContent);
                        }

                        // Include parent's summary children
                        if (parent.Children != null)
                        {
                            foreach (SemanticCellRequest sibling in parent.Children)
                            {
                                if (sibling.Type == AtomTypeEnum.Summary)
                                {
                                    contextParts.Add("Parent summary: " + GetCellContent(sibling));
                                }
                            }
                        }
                    }
                }
            }

            return contextParts.Count > 0 ? string.Join("\n\n", contextParts) : "(none)";
        }

        #endregion

        #region Private Helpers

        private static void IndexChildrenRecursive(SemanticCellRequest cell, Dictionary<Guid, SemanticCellRequest> lookup)
        {
            if (cell.Children == null) return;
            foreach (SemanticCellRequest child in cell.Children)
            {
                lookup[child.GUID] = child;
                IndexChildrenRecursive(child, lookup);
            }
        }

        private static void FlattenRecursive(SemanticCellRequest cell, List<SemanticCellRequest> flat)
        {
            flat.Add(cell);
            if (cell.Children != null)
            {
                foreach (SemanticCellRequest child in cell.Children)
                {
                    FlattenRecursive(child, flat);
                }
            }
        }

        private static void GetCellsByDepthLevelRecursive(SemanticCellRequest cell, int depth, Dictionary<int, List<SemanticCellRequest>> levels)
        {
            if (!levels.ContainsKey(depth))
                levels[depth] = new List<SemanticCellRequest>();
            levels[depth].Add(cell);

            if (cell.Children != null)
            {
                foreach (SemanticCellRequest child in cell.Children)
                {
                    GetCellsByDepthLevelRecursive(child, depth + 1, levels);
                }
            }
        }

        /// <summary>
        /// Strip preamble and trailing filler commonly added by LLMs.
        /// </summary>
        private static string CleanSummaryText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string cleaned = text.Trim();

            // Strip leading preamble patterns (case-insensitive).
            // Matches lines like:
            //   "Here's a summary of the provided content, within the 1024 token limit:"
            //   "Here is a summary:"
            //   "Summary:"
            //   "Below is a summary of the text:"
            //   "Sure! Here's a summary:"
            //   "Certainly, here is a summary of the content:"
            cleaned = Regex.Replace(
                cleaned,
                @"^(?:(?:sure[!,.]?\s*|certainly[!,.]?\s*|okay[!,.]?\s*)?(?:here(?:'s| is) (?:a |the )?summary(?:\s+of\s+[^:\n]*)?(?:,\s*[^:\n]*)?|summary(?:\s+(?:text|of\s+[^:\n]*))?)\s*:\s*)",
                "",
                RegexOptions.IgnoreCase).TrimStart();

            // Strip trailing meta-commentary (e.g., "[References 1 & 2 are cited...]", "Let me know if...")
            cleaned = Regex.Replace(
                cleaned,
                @"\s*\[(?:References?|Note|Source)[^\]]*\]\s*$",
                "",
                RegexOptions.IgnoreCase).TrimEnd();

            cleaned = Regex.Replace(
                cleaned,
                @"\s*(?:Let me know if .*|I hope (?:this|that) .*|Feel free to .*|If you (?:need|want|have) .*)\s*$",
                "",
                RegexOptions.IgnoreCase).TrimEnd();

            return cleaned;
        }

        #endregion

        #region Inner Types

        /// <summary>
        /// Thread-safe failure counter that can be passed to async methods (unlike ref int).
        /// </summary>
        private class FailureCounter
        {
            private int _count;

            public int Count => Volatile.Read(ref _count);

            public int Increment() => Interlocked.Increment(ref _count);

            public bool HasReachedLimit(int limit) => Volatile.Read(ref _count) >= limit;
        }

        #endregion
    }
}
