# Changelog

## v0.1.0 — 2026-02-06

### Added
- Initial release of Partio
- Multi-tenant REST API with bearer token authentication
- Semantic cell processing with chunking and embedding
- Chunking strategies: FixedTokenCount, SentenceBased, ParagraphBased, WholeList, ListEntry
- Overlap strategies: SlidingWindow, SentenceBoundaryAware, SemanticBoundaryAware
- Embedding clients: Ollama, OpenAI
- Database support: SQLite, PostgreSQL, MySQL, SQL Server
- Admin CRUD endpoints for tenants, users, credentials, and embedding endpoints
- Request history with filesystem body persistence and automatic cleanup
- React dashboard (Vite) with full admin UI
- SDKs: C#, Python, JavaScript
- Docker support with multi-arch builds (amd64, arm64)
- Automated test suite
