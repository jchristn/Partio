namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Partio.Sdk.Models;

    /// <summary>
    /// Unit tests for the v0.2.0 summarization feature models.
    /// These tests validate model instantiation, default values, and local logic only.
    /// They do NOT require a running server.
    /// </summary>
    public static class SummarizationTests
    {
        public static async Task RunAll(Func<string, Func<Task>, Task> runTest)
        {
            // ===== SummarizationConfiguration Defaults =====

            await runTest("SumConfig: MaxSummaryTokens defaults to 1024", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.MaxSummaryTokens != 1024)
                    throw new Exception("Expected MaxSummaryTokens=1024, got " + config.MaxSummaryTokens);
                await Task.CompletedTask;
            });

            await runTest("SumConfig: MinCellLength defaults to 0", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.MinCellLength != 0)
                    throw new Exception("Expected MinCellLength=0, got " + config.MinCellLength);
                await Task.CompletedTask;
            });

            await runTest("SumConfig: MaxParallelTasks defaults to 4", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.MaxParallelTasks != 4)
                    throw new Exception("Expected MaxParallelTasks=4, got " + config.MaxParallelTasks);
                await Task.CompletedTask;
            });

            await runTest("SumConfig: MaxRetries defaults to 10", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.MaxRetries != 10)
                    throw new Exception("Expected MaxRetries=10, got " + config.MaxRetries);
                await Task.CompletedTask;
            });

            await runTest("SumConfig: MaxRetriesPerSummary defaults to 2", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.MaxRetriesPerSummary != 2)
                    throw new Exception("Expected MaxRetriesPerSummary=2, got " + config.MaxRetriesPerSummary);
                await Task.CompletedTask;
            });

            await runTest("SumConfig: TimeoutMs defaults to 30000", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.TimeoutMs != 30000)
                    throw new Exception("Expected TimeoutMs=30000, got " + config.TimeoutMs);
                await Task.CompletedTask;
            });

            await runTest("SumConfig: Order defaults to BottomUp", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.Order != "BottomUp")
                    throw new Exception("Expected Order='BottomUp', got '" + config.Order + "'");
                await Task.CompletedTask;
            });

            await runTest("SumConfig: SummarizationPrompt defaults to null", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                // SDK model defaults prompt to null (server fills in the default template)
                if (config.SummarizationPrompt != null)
                    throw new Exception("Expected SummarizationPrompt=null, got '" + config.SummarizationPrompt + "'");
                await Task.CompletedTask;
            });

            await runTest("SumConfig: CompletionEndpointId defaults to empty", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                if (config.CompletionEndpointId != string.Empty)
                    throw new Exception("Expected CompletionEndpointId='', got '" + config.CompletionEndpointId + "'");
                await Task.CompletedTask;
            });

            await runTest("SumConfig: Custom values are preserved", async () =>
            {
                SummarizationConfiguration config = new SummarizationConfiguration();
                config.MaxSummaryTokens = 2048;
                config.MinCellLength = 256;
                config.MaxParallelTasks = 8;
                config.MaxRetries = 20;
                config.MaxRetriesPerSummary = 5;
                config.TimeoutMs = 60000;
                config.Order = "TopDown";
                config.CompletionEndpointId = "cep_test123";
                config.SummarizationPrompt = "Summarize: {content}";

                if (config.MaxSummaryTokens != 2048) throw new Exception("MaxSummaryTokens not preserved");
                if (config.MinCellLength != 256) throw new Exception("MinCellLength not preserved");
                if (config.MaxParallelTasks != 8) throw new Exception("MaxParallelTasks not preserved");
                if (config.MaxRetries != 20) throw new Exception("MaxRetries not preserved");
                if (config.MaxRetriesPerSummary != 5) throw new Exception("MaxRetriesPerSummary not preserved");
                if (config.TimeoutMs != 60000) throw new Exception("TimeoutMs not preserved");
                if (config.Order != "TopDown") throw new Exception("Order not preserved");
                if (config.CompletionEndpointId != "cep_test123") throw new Exception("CompletionEndpointId not preserved");
                if (config.SummarizationPrompt != "Summarize: {content}") throw new Exception("SummarizationPrompt not preserved");
                await Task.CompletedTask;
            });

            // ===== CompletionEndpoint Model Tests =====

            await runTest("CompletionEndpoint: Default creation succeeds", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep == null) throw new Exception("Failed to create CompletionEndpoint");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: Id is null by default (server assigns)", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                // SDK model has Id as nullable string, server generates the cep_ prefixed ID
                if (ep.Id != null)
                    throw new Exception("Expected Id=null, got '" + ep.Id + "'");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: Active defaults to true", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (!ep.Active) throw new Exception("Expected Active=true");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: EnableRequestHistory defaults to true", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (!ep.EnableRequestHistory) throw new Exception("Expected EnableRequestHistory=true");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: ApiFormat defaults to Ollama", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.ApiFormat != "Ollama")
                    throw new Exception("Expected ApiFormat='Ollama', got '" + ep.ApiFormat + "'");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthCheckEnabled defaults to true", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (!ep.HealthCheckEnabled) throw new Exception("Expected HealthCheckEnabled=true");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthCheckIntervalMs defaults to 30000", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthCheckIntervalMs != 30000)
                    throw new Exception("Expected HealthCheckIntervalMs=30000, got " + ep.HealthCheckIntervalMs);
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthCheckTimeoutMs defaults to 5000", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthCheckTimeoutMs != 5000)
                    throw new Exception("Expected HealthCheckTimeoutMs=5000, got " + ep.HealthCheckTimeoutMs);
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthCheckExpectedStatusCode defaults to 200", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthCheckExpectedStatusCode != 200)
                    throw new Exception("Expected HealthCheckExpectedStatusCode=200, got " + ep.HealthCheckExpectedStatusCode);
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthyThreshold defaults to 3", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthyThreshold != 3)
                    throw new Exception("Expected HealthyThreshold=3, got " + ep.HealthyThreshold);
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: UnhealthyThreshold defaults to 3", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.UnhealthyThreshold != 3)
                    throw new Exception("Expected UnhealthyThreshold=3, got " + ep.UnhealthyThreshold);
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthCheckUseAuth defaults to false", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthCheckUseAuth) throw new Exception("Expected HealthCheckUseAuth=false");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: Custom field assignment", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                ep.TenantId = "tenant_abc";
                ep.Name = "My LLM Endpoint";
                ep.Endpoint = "http://localhost:11434";
                ep.ApiFormat = "OpenAI";
                ep.ApiKey = "sk-test-key";
                ep.Model = "gpt-4o";
                ep.Labels = new List<string> { "prod", "llm" };
                ep.Tags = new Dictionary<string, string> { { "region", "us-east" } };

                if (ep.TenantId != "tenant_abc") throw new Exception("TenantId not preserved");
                if (ep.Name != "My LLM Endpoint") throw new Exception("Name not preserved");
                if (ep.Endpoint != "http://localhost:11434") throw new Exception("Endpoint not preserved");
                if (ep.ApiFormat != "OpenAI") throw new Exception("ApiFormat not preserved");
                if (ep.ApiKey != "sk-test-key") throw new Exception("ApiKey not preserved");
                if (ep.Model != "gpt-4o") throw new Exception("Model not preserved");
                if (ep.Labels == null || ep.Labels.Count != 2) throw new Exception("Labels not preserved");
                if (ep.Tags == null || !ep.Tags.ContainsKey("region")) throw new Exception("Tags not preserved");
                await Task.CompletedTask;
            });

            // ===== SemanticCellRequest Hierarchy Tests =====

            await runTest("SemanticCellRequest: GUID is auto-assigned", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.GUID == Guid.Empty)
                    throw new Exception("Expected auto-assigned GUID, got Guid.Empty");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Each instance gets unique GUID", async () =>
            {
                SemanticCellRequest cell1 = new SemanticCellRequest();
                SemanticCellRequest cell2 = new SemanticCellRequest();
                if (cell1.GUID == cell2.GUID)
                    throw new Exception("Two instances got the same GUID");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: ParentGUID defaults to null", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.ParentGUID != null)
                    throw new Exception("Expected ParentGUID=null, got " + cell.ParentGUID);
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: ParentGUID links parent-child correctly", async () =>
            {
                SemanticCellRequest parent = new SemanticCellRequest();
                SemanticCellRequest child = new SemanticCellRequest();
                child.ParentGUID = parent.GUID;

                if (!child.ParentGUID.HasValue)
                    throw new Exception("ParentGUID should have a value after assignment");
                if (child.ParentGUID.Value != parent.GUID)
                    throw new Exception("ParentGUID does not match parent's GUID");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Children defaults to null", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.Children != null)
                    throw new Exception("Expected Children=null, got non-null");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Children collection can be initialized and populated", async () =>
            {
                SemanticCellRequest parent = new SemanticCellRequest();
                SemanticCellRequest child1 = new SemanticCellRequest();
                SemanticCellRequest child2 = new SemanticCellRequest();

                parent.Children = new List<SemanticCellRequest> { child1, child2 };

                if (parent.Children == null) throw new Exception("Children should not be null");
                if (parent.Children.Count != 2) throw new Exception("Expected 2 children, got " + parent.Children.Count);
                if (parent.Children[0].GUID != child1.GUID) throw new Exception("First child GUID mismatch");
                if (parent.Children[1].GUID != child2.GUID) throw new Exception("Second child GUID mismatch");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Type defaults to Text", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.Type != "Text")
                    throw new Exception("Expected Type='Text', got '" + cell.Type + "'");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Type can be set to Summary", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                cell.Type = "Summary";
                if (cell.Type != "Summary")
                    throw new Exception("Expected Type='Summary', got '" + cell.Type + "'");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: SummarizationConfiguration defaults to null", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.SummarizationConfiguration != null)
                    throw new Exception("Expected SummarizationConfiguration=null");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: SummarizationConfiguration can be attached", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                SummarizationConfiguration config = new SummarizationConfiguration();
                config.CompletionEndpointId = "cep_test";
                config.MaxSummaryTokens = 512;

                cell.SummarizationConfiguration = config;

                if (cell.SummarizationConfiguration == null)
                    throw new Exception("SummarizationConfiguration should not be null after assignment");
                if (cell.SummarizationConfiguration.CompletionEndpointId != "cep_test")
                    throw new Exception("CompletionEndpointId not preserved on cell");
                if (cell.SummarizationConfiguration.MaxSummaryTokens != 512)
                    throw new Exception("MaxSummaryTokens not preserved on cell");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Text content assignment", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                cell.Text = "Hello, this is test content for summarization.";
                if (cell.Text != "Hello, this is test content for summarization.")
                    throw new Exception("Text content not preserved");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: ChunkingConfiguration defaults populated", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.ChunkingConfiguration == null)
                    throw new Exception("ChunkingConfiguration should not be null by default");
                if (cell.ChunkingConfiguration.Strategy != "FixedTokenCount")
                    throw new Exception("Expected default Strategy='FixedTokenCount', got '" + cell.ChunkingConfiguration.Strategy + "'");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: EmbeddingConfiguration defaults populated", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                if (cell.EmbeddingConfiguration == null)
                    throw new Exception("EmbeddingConfiguration should not be null by default");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Flat list with ParentGUID relationships", async () =>
            {
                // Build a flat list where child references parent via ParentGUID
                SemanticCellRequest root = new SemanticCellRequest { Text = "Root content" };
                SemanticCellRequest child1 = new SemanticCellRequest { Text = "Child 1", ParentGUID = root.GUID };
                SemanticCellRequest child2 = new SemanticCellRequest { Text = "Child 2", ParentGUID = root.GUID };
                SemanticCellRequest grandchild = new SemanticCellRequest { Text = "Grandchild", ParentGUID = child1.GUID };

                List<SemanticCellRequest> flat = new List<SemanticCellRequest> { root, child1, child2, grandchild };

                // Verify the flat list preserves parent-child GUID links
                SemanticCellRequest? foundChild1 = flat.FirstOrDefault(c => c.GUID == child1.GUID);
                if (foundChild1 == null) throw new Exception("child1 not found in flat list");
                if (!foundChild1.ParentGUID.HasValue) throw new Exception("child1 ParentGUID should not be null");
                if (foundChild1.ParentGUID.Value != root.GUID) throw new Exception("child1 ParentGUID should reference root");

                SemanticCellRequest? foundGrandchild = flat.FirstOrDefault(c => c.GUID == grandchild.GUID);
                if (foundGrandchild == null) throw new Exception("grandchild not found in flat list");
                if (!foundGrandchild.ParentGUID.HasValue || foundGrandchild.ParentGUID.Value != child1.GUID)
                    throw new Exception("grandchild ParentGUID should reference child1");

                // Roots are cells with no ParentGUID
                List<SemanticCellRequest> roots = flat.Where(c => !c.ParentGUID.HasValue).ToList();
                if (roots.Count != 1) throw new Exception("Expected 1 root, got " + roots.Count);
                if (roots[0].GUID != root.GUID) throw new Exception("Root GUID mismatch");

                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Nested Children hierarchy", async () =>
            {
                SemanticCellRequest grandchild = new SemanticCellRequest { Text = "Grandchild" };
                SemanticCellRequest child = new SemanticCellRequest
                {
                    Text = "Child",
                    Children = new List<SemanticCellRequest> { grandchild }
                };
                SemanticCellRequest root = new SemanticCellRequest
                {
                    Text = "Root",
                    Children = new List<SemanticCellRequest> { child }
                };

                if (root.Children == null || root.Children.Count != 1) throw new Exception("Root should have 1 child");
                List<SemanticCellRequest>? childChildren = root.Children[0].Children;
                if (childChildren == null || childChildren.Count != 1)
                    throw new Exception("Child should have 1 grandchild");
                if (childChildren[0].Text != "Grandchild")
                    throw new Exception("Grandchild text mismatch");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellRequest: Labels and Tags assignment", async () =>
            {
                SemanticCellRequest cell = new SemanticCellRequest();
                cell.Labels = new List<string> { "important", "chapter1" };
                cell.Tags = new Dictionary<string, string> { { "source", "document.pdf" }, { "page", "5" } };

                if (cell.Labels == null || cell.Labels.Count != 2) throw new Exception("Labels not preserved");
                if (!cell.Labels.Contains("important")) throw new Exception("Label 'important' missing");
                if (cell.Tags == null || cell.Tags.Count != 2) throw new Exception("Tags not preserved");
                if (cell.Tags["source"] != "document.pdf") throw new Exception("Tag 'source' value mismatch");
                await Task.CompletedTask;
            });

            // ===== SemanticCellResponse Tests =====

            await runTest("SemanticCellResponse: Default creation succeeds", async () =>
            {
                SemanticCellResponse resp = new SemanticCellResponse();
                if (resp == null) throw new Exception("Failed to create SemanticCellResponse");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: Type field supports Summary", async () =>
            {
                SemanticCellResponse resp = new SemanticCellResponse();
                resp.Type = "Summary";
                if (resp.Type != "Summary")
                    throw new Exception("Expected Type='Summary', got '" + resp.Type + "'");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: Type field supports Text", async () =>
            {
                SemanticCellResponse resp = new SemanticCellResponse();
                resp.Type = "Text";
                if (resp.Type != "Text")
                    throw new Exception("Expected Type='Text', got '" + resp.Type + "'");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: Children collection", async () =>
            {
                SemanticCellResponse parent = new SemanticCellResponse();
                parent.GUID = Guid.NewGuid();
                parent.Type = "Text";
                parent.Text = "Parent content";

                SemanticCellResponse summaryChild = new SemanticCellResponse();
                summaryChild.GUID = Guid.NewGuid();
                summaryChild.ParentGUID = parent.GUID;
                summaryChild.Type = "Summary";
                summaryChild.Text = "This is a summary of the parent.";

                parent.Children = new List<SemanticCellResponse> { summaryChild };

                if (parent.Children == null || parent.Children.Count != 1)
                    throw new Exception("Expected 1 child on response");
                if (parent.Children[0].Type != "Summary")
                    throw new Exception("Child type should be Summary");
                if (parent.Children[0].ParentGUID != parent.GUID)
                    throw new Exception("Child ParentGUID should match parent GUID");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: GUID preserved through assignment", async () =>
            {
                Guid testGuid = Guid.NewGuid();
                SemanticCellResponse resp = new SemanticCellResponse();
                resp.GUID = testGuid;
                if (resp.GUID != testGuid)
                    throw new Exception("GUID not preserved: expected " + testGuid + ", got " + resp.GUID);
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: ParentGUID preserved through assignment", async () =>
            {
                Guid parentGuid = Guid.NewGuid();
                SemanticCellResponse resp = new SemanticCellResponse();
                resp.ParentGUID = parentGuid;
                if (!resp.ParentGUID.HasValue)
                    throw new Exception("ParentGUID should have value after assignment");
                if (resp.ParentGUID.Value != parentGuid)
                    throw new Exception("ParentGUID not preserved");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: ParentGUID defaults to null", async () =>
            {
                SemanticCellResponse resp = new SemanticCellResponse();
                if (resp.ParentGUID != null)
                    throw new Exception("Expected ParentGUID=null by default");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: Text defaults to empty", async () =>
            {
                SemanticCellResponse resp = new SemanticCellResponse();
                if (resp.Text != string.Empty)
                    throw new Exception("Expected Text='', got '" + resp.Text + "'");
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: Chunks defaults to empty list", async () =>
            {
                SemanticCellResponse resp = new SemanticCellResponse();
                if (resp.Chunks == null)
                    throw new Exception("Chunks should not be null by default");
                if (resp.Chunks.Count != 0)
                    throw new Exception("Chunks should be empty by default, got " + resp.Chunks.Count);
                await Task.CompletedTask;
            });

            await runTest("SemanticCellResponse: Nested response hierarchy", async () =>
            {
                Guid rootGuid = Guid.NewGuid();
                Guid childGuid = Guid.NewGuid();
                Guid summaryGuid = Guid.NewGuid();

                SemanticCellResponse summaryResp = new SemanticCellResponse
                {
                    GUID = summaryGuid,
                    ParentGUID = childGuid,
                    Type = "Summary",
                    Text = "Summary of child"
                };

                SemanticCellResponse childResp = new SemanticCellResponse
                {
                    GUID = childGuid,
                    ParentGUID = rootGuid,
                    Type = "Text",
                    Text = "Child content",
                    Children = new List<SemanticCellResponse> { summaryResp }
                };

                SemanticCellResponse rootResp = new SemanticCellResponse
                {
                    GUID = rootGuid,
                    Type = "Text",
                    Text = "Root content",
                    Children = new List<SemanticCellResponse> { childResp }
                };

                // Verify the hierarchy is correctly wired
                if (rootResp.Children == null || rootResp.Children.Count != 1)
                    throw new Exception("Root should have 1 child");
                SemanticCellResponse foundChild = rootResp.Children[0];
                if (foundChild.GUID != childGuid) throw new Exception("Child GUID mismatch");
                if (foundChild.ParentGUID != rootGuid) throw new Exception("Child ParentGUID should be root");
                if (foundChild.Children == null || foundChild.Children.Count != 1)
                    throw new Exception("Child should have 1 summary child");
                SemanticCellResponse foundSummary = foundChild.Children[0];
                if (foundSummary.Type != "Summary") throw new Exception("Summary type mismatch");
                if (foundSummary.ParentGUID != childGuid) throw new Exception("Summary ParentGUID should be child");
                await Task.CompletedTask;
            });

            // ===== EmbeddingEndpoint vs CompletionEndpoint Comparison =====

            await runTest("CompletionEndpoint: HealthCheckMethod defaults to GET", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthCheckMethod != "GET")
                    throw new Exception("Expected HealthCheckMethod='GET', got '" + ep.HealthCheckMethod + "'");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: Labels and Tags default to null", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.Labels != null) throw new Exception("Expected Labels=null by default");
                if (ep.Tags != null) throw new Exception("Expected Tags=null by default");
                await Task.CompletedTask;
            });

            await runTest("CompletionEndpoint: HealthCheckUrl defaults to null", async () =>
            {
                CompletionEndpoint ep = new CompletionEndpoint();
                if (ep.HealthCheckUrl != null)
                    throw new Exception("Expected HealthCheckUrl=null, got '" + ep.HealthCheckUrl + "'");
                await Task.CompletedTask;
            });

            // ===== Summary Type Round-Trip =====

            await runTest("Summary cell round-trip: request to response types align", async () =>
            {
                // Simulate creating a summary cell request and its corresponding response
                SemanticCellRequest reqCell = new SemanticCellRequest();
                reqCell.Type = "Summary";
                reqCell.Text = "This is a generated summary.";
                Guid cellGuid = reqCell.GUID;
                Guid parentGuid = Guid.NewGuid();
                reqCell.ParentGUID = parentGuid;

                // Build a corresponding response
                SemanticCellResponse respCell = new SemanticCellResponse();
                respCell.GUID = cellGuid;
                respCell.ParentGUID = parentGuid;
                respCell.Type = reqCell.Type;
                respCell.Text = reqCell.Text;

                if (respCell.GUID != reqCell.GUID) throw new Exception("GUID not preserved from request to response");
                if (respCell.ParentGUID != reqCell.ParentGUID) throw new Exception("ParentGUID not preserved");
                if (respCell.Type != reqCell.Type) throw new Exception("Type not preserved");
                if (respCell.Text != reqCell.Text) throw new Exception("Text not preserved");
                await Task.CompletedTask;
            });
        }
    }
}
