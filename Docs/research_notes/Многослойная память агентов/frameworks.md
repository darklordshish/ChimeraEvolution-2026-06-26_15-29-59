# Layered memory architectures for LLM agents: frameworks (notes as of 2026-11-10)

Method: about 17 tool calls, mostly primary sources (official docs, GitHub READMEs, arXiv abstracts). Search snippets from third-party sites were not used as facts. WebFetch summaries come from a small model, so details not shown on a page are listed under Gaps rather than guessed.

## 1. MemGPT / Letta: tiers, shared blocks, sleep-time, local hosting

### Takeaway
MemGPT introduced "virtual context management", an OS-style hierarchy of memory tiers. Letta (Apache-2.0) turned this into core memory blocks (always in the prompt) plus out-of-context recall and archival stores. Blocks can be shared across agents, but the docs call blocks the legacy v1 mechanism. The current recommendation is shared "memory repositories" (MemFS). Concurrent edits are only partly safe.

### Cited Findings
- The MemGPT paper (arXiv 2310.08560) proposes "virtual context management", modelled on OS hierarchical memory. It moves information between tiers so the LLM can work past its context window. It was tested on long-document analysis and multi-session chat. — [arXiv abstract](https://arxiv.org/abs/2310.08560)
- A memory block has a label (e.g. "human", "persona"), a string value, a size limit and optional descriptions. It is persisted in the DB with a unique `block_id`. The context window is compiled from DB state on each request, with a Jinja prompt template. Blocks can be `read_only`, in which case only the developer can change them. The original MemGPT had two char-limited blocks, human and persona. — [Letta blog: memory blocks](https://www.letta.com/blog/memory-blocks)
- Blocks can be attached to several agents. One agent's update is immediately visible to the others, which allows coordination without messaging. Attach goes through `block_ids` or `agents.blocks.attach`. External systems can sync in with `blocks.update()`, which replaces the whole value. — [Letta docs: shared memory](https://docs.letta.com/v1-sdk/memory/shared-memory/index.md)
- Read-only applies to the whole block. A block cannot be writable for some agents and read-only for others. — [Letta docs: shared memory](https://docs.letta.com/v1-sdk/memory/shared-memory/index.md)
- Concurrency by tool: `memory_insert` is append-only and safe. `memory_replace` is mostly safe but fails if the target text changed. `memory_rethink` is a full rewrite, last writer wins, and is unsafe. The guide advises one owner agent (or sleep-time memory) for heavy edits, with the others appending. — [Letta docs: shared memory](https://docs.letta.com/v1-sdk/memory/shared-memory/index.md)
- The same page marks memory blocks as legacy and recommends shared memory repositories and migrating to MemFS for new work. — [Letta docs: shared memory](https://docs.letta.com/v1-sdk/memory/shared-memory/index.md)
- Sleep-time agents process information in idle periods and write "learned context" into blocks that other agents can share. Listed uses: shared knowledge bases, sleep-time compute, collaborative team memory. — [Letta blog: memory blocks](https://www.letta.com/blog/memory-blocks)
- Core / recall / archival tiers: core = blocks always in the prompt. Recall = searchable conversation history (`conversation_search`). Archival = long-term semantic store. Only recall and archival are searched on demand. This comes from a secondary summary of Letta docs and is not confirmed on a primary page I fetched. — [search summary citing zby.github.io review](https://zby.github.io/commonplace/agent-memory-systems/reviews/letta/)
- A Letta forum thread says plans exist to make archival memory shared, "pending a few core updates". The proposed workaround is a shared block such as `archival_context`. The thread's date is unknown and it may be stale. — [Letta forum](https://forum.letta.com/t/should-archival-memories-be-managed-by-a-sleeptime-agent/74)
- License is Apache-2.0. The repo now centres on Letta Code (npm CLI, `letta`), an App Server run with `letta server` for local or self-hosted agents, and an Agent SDK. The older V1 API server was retired to an `archive` branch. — [GitHub letta-ai/letta](https://github.com/letta-ai/letta)

### Inferences
- If this project needs multi-agent shared state, shared blocks give a simple partition: one named string per scope, attached to chosen agents. Staleness handling is weak: last writer wins for rewrites, so a single-writer convention is needed.
- The v1 server was retired, so older tutorials (Docker plus Postgres plus Ollama) may no longer match the current repo. Check the current docs before relying on them.

### Gaps
- No primary page fetched confirmed Ollama or other local-LLM support, the DB requirement of the current server, or the MemFS design. The README did not mention them.
- Shared archival memory status as of late 2026 is unconfirmed.
- The sleep-time agent mechanics (trigger frequency, which model) were not found in a primary source.

## 2. mem0 (open source): memory types, scoping, graph, local setup

### Takeaway
Mem0 OSS (Apache-2.0) is a flat store of extracted facts scoped by `user_id` / `agent_id` / `run_id`. Its graph memory has been removed from the OSS build and now exists only on the paid Platform. Defaults point at OpenAI, though other providers are supported.

### Cited Findings
- License Apache-2.0. Defaults: LLM `gpt-5-mini`, embedder `text-embedding-3-small`, both OpenAI. Many other LLMs are supported. The README describes "Multi-Level Memory" retaining User, Session and Agent state. — [GitHub mem0ai/mem0](https://github.com/mem0ai/mem0)
- Install is `pip install mem0ai`. `mem0ai[nlp]` plus a spaCy model gives BM25 keyword matching and entity extraction. A self-hosted server is available via `docker compose up -d` (dashboard at localhost:3000) or `make bootstrap`. Auth is on by default. — [GitHub mem0ai/mem0](https://github.com/mem0ai/mem0)
- A new memory algorithm (April 2026) is reported as LoCoMo 71.4 to 92.5, LongMemEval 67.8 to 94.4, BEAM 1M 64.1, BEAM 10M 48.6. The README says these come from the managed platform, which has "proprietary optimizations not available in the open-source SDK", and that OSS gains will be "directionally similar". A migration guide "oss-v2-to-v3" exists. — [GitHub mem0ai/mem0](https://github.com/mem0ai/mem0)
- Graph memory: Neo4j, Memgraph, Kuzu, Apache AGE and Neptune drivers (about 4,000 lines) were removed from OSS, with no OSS replacement. The `enable_graph` flag and `graph_store` config are gone, and `relations` is no longer returned. Platform graph memory is "built in and always on". — [Mem0 docs: graph memory migration](https://docs.mem0.ai/open-source/graph_memory/overview)
- Scoping: `user_id`, `agent_id`, `run_id` can be combined. In the new API, `add()` and `delete_all()` take entity IDs as top-level kwargs, while `search()` and `get_all()` need them inside `filters`, and top-level kwargs raise `ValueError`. — [Mem0 docs: graph memory migration](https://docs.mem0.ai/open-source/graph_memory/overview)
- Entity matching now uses spaCy and creates a `{collection_name}_entities` vector collection, not a graph store. — [Mem0 docs](https://docs.mem0.ai/open-source/graph_memory/overview)
- Informal meaning of the IDs: `user_id` = user profile, `agent_id` = agent identity, `run_id` = one conversation session (third-party guide). — [DataCamp tutorial](https://www.datacamp.com/tutorial/mem0-tutorial)

### Inferences
- Slices in mem0 are metadata filters over one vector collection, not separate stores. They are cheap but give weak isolation.
- A fully local, free setup is plausible (local LLM and embedder plus a local vector store), but I did not confirm a vendor-documented Ollama config. See Gaps.
- Older graph-memory tutorials online are obsolete for OSS as of the current release.

### Gaps
- Default vector store and the list of supported local vector stores were not on the fetched page.
- No official Ollama configuration page was fetched. A Spanish-language tutorial claims it works, and I rate that source low quality.
- Conflict handling (the add/update/delete decision logic, as described in the Mem0 paper) was not verified from a primary source in this pass.

## 3. Zep / Graphiti: temporal knowledge graph

### Takeaway
Graphiti (Apache-2.0) is the open-source engine behind Zep. It builds an incrementally updated graph of episodes, entities, edges and communities, with bi-temporal facts that are invalidated rather than deleted. It can run locally with Neo4j or FalkorDB, but it needs an LLM that produces reliable structured output. Zep itself is a managed service.

### Cited Findings
- The Zep paper describes Graphiti as a "temporally-aware knowledge graph engine" combining conversational and structured business data. Reported results: DMR 94.8% vs 93.4% for MemGPT. LongMemEval: up to 18.5% higher accuracy and 90% lower latency than baselines. — [arXiv 2501.13956](https://arxiv.org/abs/2501.13956)
- Graphiti is Apache-2.0, offers "explicit bi-temporal tracking with automatic fact invalidation", and keeps old facts for historical queries. Backends: Neo4j 5.26, FalkorDB 1.1.2, Amazon Neptune. Kuzu is listed but deprecated. — [GitHub getzep/graphiti](https://github.com/getzep/graphiti)
- LLM: defaults to OpenAI and needs a key. Anthropic, Gemini and Groq are optional. It works best with Structured Output support. Local models go through OpenAI-compatible endpoints (Ollama, vLLM, llama.cpp, LM Studio) via `OpenAIGenericClient`. Small local models often return schema-breaking JSON, so use the strongest model you can run. — [GitHub getzep/graphiti](https://github.com/getzep/graphiti)
- A reference MCP server supports episode, entity and group management and search. It has no authentication and is not production-ready. Anyone can read, change or delete data in any `group_id`. — [GitHub getzep/graphiti](https://github.com/getzep/graphiti)
- Zep is the managed enterprise layer on Graphiti (managed or BYOC). Graphiti is the self-hosted framework. — [GitHub getzep/graphiti](https://github.com/getzep/graphiti)
- Structure per a third-party analysis: episodes are raw inputs, entities are extracted, communities come from community detection, and edges hold fact text plus validity intervals. Each episode runs through LLM steps for extraction and dedup, then a community update. — [zby commonplace review](https://zby.github.io/commonplace/sources/graphiti-temporal-knowledge-graph) (secondary source)

### Inferences
- `group_id` is the partition ("slice") key: one graph database can hold many per-user, per-agent or per-project graphs. The README does not explain the mechanism, so check the code before relying on isolation.
- The temporal slice is native: facts have valid-from and valid-to plus an ingestion time, so "state as of date X" queries are possible. This is the strongest staleness handling among the systems covered here, but it is paid for with several LLM calls per ingested episode.

### Gaps
- Exact `group_id` semantics, current release number, and the Zep Community Edition status were not found.
- Hardware needs for local operation were not found.
- Whether Zep still has a free self-hosted edition other than Graphiti is unknown. Treat Zep as paid SaaS.

## 4. Cognee, LangMem, A-MEM, MemOS, Memobase, MIRIX

### Takeaway
All six are open source (licenses below). They split memory by type (MIRIX, LangMem), by user (Memobase), by cube (MemOS), by dataset (Cognee), or by linked notes (A-MEM). Only some give real multi-tenant isolation.

### Cited Findings
**Cognee**
- Apache-2.0. Four operations: remember (permanent or session memory, with `dataset_name`), recall (with a `datasets` list), improve (enrich, apply feedback, move session knowledge into the graph), forget (item or dataset). Session memory feeds into the permanent graph. — [GitHub topoteretes/cognee](https://github.com/topoteretes/cognee)
- Local Ollama supported (the README links a guide). Multi-tenant mode with authenticated users is the default unless `ENABLE_BACKEND_ACCESS_CONTROL=false`. Postgres can serve graph, vector, relational and session storage in a demo. v1.6.1 was released 2026-09-24. Python 3.10-3.14. — [GitHub topoteretes/cognee](https://github.com/topoteretes/cognee)

**LangMem (LangChain)**
- Types: semantic (collection of records or one updated "profile"), episodic (successful past interactions with situation, reasoning, action, result), procedural (system behaviour, refined instructions). Namespaces are hierarchical (organisation, user, application) with template variables like `{user_id}` filled from `RunnableConfig`. Formation is either hot path (immediate, adds latency) or background (reflects after the conversation, delayed). The core API does not depend on a storage system. — [LangMem conceptual guide](https://langchain-ai.github.io/langmem/concepts/conceptual_guide/)
- MIT license. Quickstart uses `InMemoryStore`, which loses data on restart. Production should use a DB-backed store such as `AsyncPostgresStore`. About 1.8k stars. No published releases are shown. The example uses Anthropic and OpenAI embeddings, so a local model would need substitution. — [GitHub langchain-ai/langmem](https://github.com/langchain-ai/langmem)

**A-MEM (arXiv 2502.12110)**
- Zettelkasten-inspired. A new memory becomes a structured note with context description, keywords and tags. It is linked to similar past notes. "Memory evolution": new memories can update the context and attributes of existing ones. Reported to beat baselines on six foundation models. — [arXiv abstract](https://arxiv.org/abs/2502.12110)

**MemOS (arXiv 2507.03724)**
- A "memory operating system" with the MemCube as basic unit (content plus provenance and versioning metadata). It unifies plaintext, activation-based and parametric memory. — [arXiv abstract](https://arxiv.org/abs/2507.03724)
- Repo is Apache-2.0, MemOS 2.0 "Stardust". Cubes give "isolation, controlled sharing, and dynamic composition across users, projects, and agents". Self-host: `docker compose up` brings the API (localhost:8000), Neo4j and Qdrant, with LLM/embedder/vector/graph/scheduler options in `docker/.env.example-full`. MemScheduler runs memory ops asynchronously. The local plugin 2.0 (2026-05-09) uses SQLite with tiers L1 traces, L2 policies, L3 world models and Skills, and needs Node.js plus a host such as OpenClaw or Hermes. — [GitHub MemTensor/MemOS](https://github.com/MemTensor/MemOS)

**Memobase**
- Apache-2.0. Memory is per user, not per agent: a profile (topic / sub_topic / content entries, schema-configurable), a time-ordered event timeline, and an input buffer that flushes at roughly 1024 tokens or about an hour idle. Server is FastAPI plus Postgres plus Redis in Docker. The repo claims online latency under 100 ms (a vendor claim). — [GitHub memodb-io/memobase](https://github.com/memodb-io/memobase)

**MIRIX (arXiv 2507.07957)**
- Six memory types: Core, Episodic, Semantic, Procedural, Resource Memory, Knowledge Vault, coordinated by multiple agents. Reported: ScreenshotVQA +35% accuracy over RAG with 99.9% less storage, and LOCOMO 85.4%. A packaged local app stores data locally. Paper is CC BY 4.0. — [arXiv abstract](https://arxiv.org/abs/2507.07957)

### Inferences
- The two typology families differ. Cognitive-type slicing (LangMem, MIRIX) answers "what kind of memory is this". Scope slicing (mem0, Memobase, Cognee datasets, Graphiti group_id, MemOS cubes) answers "whose memory is this". A real design usually needs both.
- For a free and local setup, the lowest-complexity options are Memobase (Postgres + Redis, but it needs an LLM for extraction), LangMem (library only, you supply a store), and mem0 OSS (no graph). Heaviest: Graphiti (graph DB plus a capable LLM) and MemOS (Neo4j + Qdrant).

### Gaps
- Licenses of A-MEM's and MIRIX's code repositories were not checked (only the MIRIX paper licence was seen).
- Memobase local-LLM config, MemOS Ollama support (not mentioned on the README), and the LangMem maintenance status were not established.
- No independent head-to-head benchmark was found. All benchmark numbers above are vendor or author claims.

## 5. Concurrent agents sharing one memory: partitions, conflicts, staleness

### Takeaway
Partitioning is well supported everywhere (blocks, IDs, namespaces, group_id, datasets, cubes). Principled conflict handling is rare. Graphiti (temporal invalidation) is the clearest. Letta documents explicit single-writer guidance. The rest rely on the LLM's extraction decisions.

### Cited Findings
- Letta: shared blocks, immediate visibility, append-safe vs rewrite-unsafe tools, single-owner convention. — [Letta docs](https://docs.letta.com/v1-sdk/memory/shared-memory/index.md)
- Graphiti: contradicted facts are invalidated rather than deleted, with bi-temporal validity. The servers have no auth, so isolation by `group_id` is only a convention. — [GitHub getzep/graphiti](https://github.com/getzep/graphiti)
- Cognee: multi-tenant access control by default, plus dataset scoping. — [GitHub topoteretes/cognee](https://github.com/topoteretes/cognee)
- MemOS: cube-level isolation and controlled sharing across users, projects and agents. — [GitHub MemTensor/MemOS](https://github.com/MemTensor/MemOS)
- mem0: user/agent/run filters inside one store. — [Mem0 docs](https://docs.mem0.ai/open-source/graph_memory/overview)
- A-MEM: staleness is handled by "memory evolution" (updating old notes when new ones arrive). — [arXiv 2502.12110](https://arxiv.org/abs/2502.12110)
- LangMem: a profile is "updated in place", which gives last-writer semantics. Collections accumulate records. — [LangMem conceptual guide](https://langchain-ai.github.io/langmem/concepts/conceptual_guide/)

### Inferences
- Pattern worth borrowing: scope key (namespace) × memory kind (core/episodic/semantic/procedural) × time validity (valid-from/to), with one writer per shared block and append-only logs for concurrent writers.

### Gaps
- No source on optimistic locking or versioned conflict resolution in mem0, MemOS, Cognee or Memobase.
- Paid-only flags: Zep (managed), mem0 Platform (graph memory, proprietary optimizations behind the benchmark numbers), Letta cloud and MemOS Cloud plugin were not examined for pricing. The OSS components above are all free to run, but an OpenAI-default config still costs money unless switched to a local model.
