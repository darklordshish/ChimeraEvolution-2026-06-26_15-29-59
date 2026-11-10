# Measured effectiveness of agent long-term memory systems (benchmarks, baselines, controversies)

Researched 2026-11-10. Flag legend: [V] vendor-reported, [I] independent/academic, [S] secondary summary (not verified against primary).

## 1. Which benchmarks exist, what they measure

### Takeaway
LoCoMo (long chat QA, ~26k tokens/conversation) is the most-cited but weakest discriminator; LongMemEval, MemoryAgentBench and BEAM stress update/forgetting/contradiction and much longer histories, where every tested method does badly.

### Cited Findings
- LoCoMo (via Mem0 paper setup): 4 graded categories single-hop, multi-hop, open-domain, temporal; metric LLM-as-judge J. A full conversation is ~26,031 tokens, so it fits any modern context window — [Mem0 paper, arXiv 2504.19413](https://arxiv.org/html/2504.19413)
- LoCoMo category 5 (adversarial, 446 q, 444 have no answer field) is dropped by Mem0, Zep, EverMemOS, leaving 1,540 graded questions — [braindetox summary of audits](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]
- LongMemEval (Oct 2024): 500 curated questions, 5 abilities (information extraction, multi-session reasoning, temporal reasoning, knowledge updates, abstention), freely scalable user-assistant histories; commercial chat assistants and long-context LLMs show ~30% accuracy drop over sustained interactions; proposes indexing/retrieval/reading framework with session decomposition, fact-augmented key expansion, time-aware query expansion — [LongMemEval arXiv 2410.10813](https://arxiv.org/abs/2410.10813) [I]
- LongMemEval_S histories average ~115k tokens — [Zep summary via emergentmind](https://www.emergentmind.com/papers/2501.13956) [S]. The dataset was replaced by a cleaned version in Sept 2025, so older/newer scores are not on the same test — [web search synthesis citing braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]
- MemoryAgentBench (Hu, Wang, McAuley, UCSD; v1 Jul 2025, v3 Mar 2026): incremental multi-turn format; 4 competencies: accurate retrieval, test-time learning, long-range understanding, conflict resolution/selective forgetting; new datasets EventQA and FactConsolidation (FC-SH single-hop, FC-MH multi-hop); tests long-context agents, RAG agents, commercial memory agents (Mem0, Zep, MemGPT, MIRIX) — [arXiv 2507.05257](https://arxiv.org/abs/2507.05257v2)
- BEAM (Tavakoli et al., arXiv 2510.27246 Oct 2025, ICLR 2026): 100 generated conversations, 2,000 validated questions, 100K to 10M tokens; 10 abilities: abstention, contradiction resolution, event ordering, information extraction, instruction following, knowledge update, multi-hop, preference following, summarization, temporal reasoning. Proposes LIGHT (episodic memory + working memory + scratchpad) — [BEAM paper](https://arxiv.org/html/2510.27246v1), [search summary](https://arxiv.org/abs/2510.27246)
- Other 2026 items surfaced but NOT read: STALE benchmark (stale-memory adjudication; aggregator only), "When Stored Evidence Stops Being Usable: Scale-Conditioned Evaluation of Agent Memory" (arXiv 2605.07313) — [arXiv 2605.07313](https://arxiv.org/pdf/2605.07313)

### Inferences
- LoCoMo-scale (26k tokens) tests mostly measure the answering model; BEAM/MemoryAgentBench are the ones where memory architecture could matter, and they show it mostly does not yet.

### Gaps
- Did not read LongMemEval_S/M per-system tables first-hand, nor STALE, nor 2605.07313.

## 2. Reported scores vs baselines (full context, RAG, files); latency and tokens

### Takeaway
In the most-quoted independent-style table (Mem0 paper, LoCoMo, GPT-4o-mini) the full-context baseline (72.9 J) beat every memory system (best 68.4); memory systems win only on latency/tokens (~91% lower p95, ~90% fewer tokens). On MemoryAgentBench, commercial memory agents (Mem0, Zep, MemGPT, MIRIX) scored far BELOW long-context and even BM25 RAG.

### Cited Findings
**LoCoMo, Mem0 paper (April 2025, GPT-4o-mini; [V] authors are Mem0 — note it also reports Zep/others run by them)** — [arXiv 2504.19413 HTML](https://arxiv.org/html/2504.19413):
- Overall J: Full-context 72.90; Mem0g 68.44; Mem0 66.88; Zep 65.99; LangMem 58.10; OpenAI memory 52.90; A-Mem 48.38 (re-run, temp 0); best RAG (k=2, 256-token chunks) 60.97. MemGPT/Letta not reported in the table.
- Per category J (single/multi/open/temporal): Mem0 67.13/51.15/72.93/55.51; Mem0g 65.71/47.19/75.71/58.13; Zep 61.70/41.35/76.60/49.31; LangMem 62.23/47.92/71.12/23.43; OpenAI 63.79/42.92/62.29/21.71; A-Mem 39.79/18.85/54.05/49.91.
- Latency p95 (search / total, s): Mem0 0.200/1.440; Mem0g 0.657/2.590; Zep 0.778/2.926; LangMem 59.82/60.40; A-Mem 1.485/4.374; RAG 0.699/1.907; OpenAI total 0.889; Full-context total 17.117.
- Retrieved context tokens: Mem0 1,764; Mem0g 3,616; Zep 3,911; LangMem 127; OpenAI 4,437; A-Mem 2,520; Full-context 26,031. Stored footprint per conversation: Mem0 ~7k, Mem0g ~14k, Zep >600k tokens.
- Abstract headline: Mem0 +26% relative over OpenAI in LLM-judge, graph variant ~+2% over base, p95 latency ~91% lower and >90% token savings vs full-context — [arXiv abs 2504.19413](https://arxiv.org/abs/2504.19413)
- Zep's complaint: this paper's full-context baseline (~73) exceeded Mem0's best (~68) — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]

**Zep/Graphiti paper (Jan 2025, [V] Zep)**: LongMemEval_S, gpt-4o: Zep 71.2% vs full-context 60.2%; gpt-4o-mini 63.8% vs 55.4%; "up to 18.5% accuracy gain, 90% latency cut" (latency from prompt ~1.6K vs ~115K tokens, e.g. 2.58 s vs 28.9 s); DMR 94.8% vs MemGPT 93.4% — [arXiv 2501.13956 summaries](https://www.emergentmind.com/papers/2501.13956) [S]. Counterpoint: full context with gemini-3.1-flash-lite scores 81.6% on LongMemEval_S (about $2.51 for LoCoMo-size runs) — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S].

**MemoryAgentBench Table 3 (independent, UCSD; RAG and commercial agents on GPT-4o-mini unless noted)** — [arXiv 2507.05257v3](https://arxiv.org/html/2507.05257v3). Columns: Overall score; SH-QA/MH-QA; EventQA; FC-SH / FC-MH (selective forgetting):
- Long-context: Claude-3.7-Sonnet 49.6 (FC-SH 43, FC-MH 2); GPT-4o 48.8 (60/5); GPT-4.1-mini 46.9; Gemini-2.0-Flash 42.4; GPT-4o-mini 42.2.
- BM25 RAG 41.5; Qwen3-Embedding-4B RAG 38.2; Contriever 29.8.
- Commercial/agentic: MIRIX (GPT-4o-mini) 26.2; Mem0 21.1; Zep 24.0; MemGPT 28.3; MIRIX with GPT-4.1-mini backbone 37.7 (still below BM25 RAG and all long-context).
- Examples: EventQA GPT-4o long-context 77.2 vs Mem0 37.5, Zep 42.5, MemGPT 26.2, MIRIX 29.8; Summarization F1 Mem0 4.8, Zep 4.2 vs Claude-3.7 52.5. MIRIX is best commercial on SH/MH-QA (62/61; 73/75 with GPT-4.1-mini).
- All methods fail multi-hop selective forgetting: max 7% (Contriever FC-MH); long-context is okay on single-hop updating (GPT-4o 60) but ~2–5% on multi-hop.

**BEAM (independent, academic)** — [arXiv 2510.27246](https://arxiv.org/html/2510.27246v1). Average scores Vanilla long-context / RAG / LIGHT:
- Llama 4 Maverick: 100K 0.240/0.323/0.358; 1M 0.259/0.307/0.336; 10M 0.104/0.249/0.266.
- Qwen 2.5: 100K 0.280/0.269/0.311; 10M 0.133/0.211/0.238. GPT-4.1-nano 10M 0.109/0.218/0.226. Gemini 2.0 Flash 10M 0.122/0.216/0.192 (RAG beats LIGHT).
- LIGHT improvement over strongest baseline 3.5–12.69%. Contradiction resolution is weakest ability: <0.05 for all methods at 100K–1M; event ordering ~0.16–0.27 across the board. Absolute scores (<0.36) show the benchmark is far from solved. (Mem0/Letta/Zep were not evaluated there as far as read; only first ~100k of 188k chars read.)

**Newer vendor claims (2026), not comparable**: Mem0 self-reports ~92.5 LoCoMo and ~94.4 LongMemEval; its "old algorithm" 71.4 LoCoMo, 49.0 on LongMemEval in an independent eval (arXiv 2603.04814, cited by a Vectorize comparison; not read). Zep: 75.14 (2025 corrected), 94.7 (2026, gpt-5.4 answer and judge). EverMemOS 92.3 headline. — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]; [search synthesis](https://mem0.ai/library/agent-memory/ai-memory-benchmarks-in-2026) [V]
- Same Zep config reported as 84%, 58%, 75% depending on who ran it — [agenticwire/DEV-style roundups via search] [S]; full list of Zep numbers 58.44 (Mem0 re-run), 59.22 (MemOS paper), 65.99 (Mem0 paper), 75.14, 79.09 (MIRIX) — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]

**MemOS** [V] (arXiv 2507.03724): claims best on LoCoMo vs mem0/OpenAI-memory/zep with GPT-4o-mini; mirrors list avg 0.7331 vs OpenAI 0.5275, but percent-improvement claims conflict between sources (e.g. "+159% temporal" vs "+43.7%"); unreconciled — [MemOS paper](https://arxiv.org/pdf/2507.03724), [MemTensor PR](https://github.com/MemTensor/MemOS/pull/428/files)

### Inferences
- Memory frameworks' defensible value is cost/latency (≈1.7k vs 26k tokens, 1.4 s vs 17 s p95), not accuracy, at LoCoMo scale; at 100K–10M tokens (BEAM) RAG/structured memory beats vanilla long context, but gap to "solved" is large.
- The independent MemoryAgentBench ranking (long-context > BM25 > embedding RAG > commercial agents) is the opposite of vendor-run rankings, strongly suggesting framework-specific tuning/ingestion mismatch (they were run on a different ingestion regime, incremental chunks).

### Gaps
- No first-hand LangMem or A-MEM own-paper numbers; MemGPT/Letta absent from Mem0 table; no independent published LoCoMo/LongMemEval figure for Letta beyond its own blog; MIRIX/A-MEM paper numbers not read; Mem0's 2026 numbers use new grader prompts so cannot be compared with 2025.

## 3. Controversies: Letta vs Mem0, Zep vs Mem0, "filesystem is all you need", benchmark validity

### Takeaway
Headline scores swing 20–35 points with grader prompt/model and answer model; audits find 6–7% broken answer keys and lenient judges. Letta's plain-filesystem agent (74.0 with GPT-4o-mini) beat Mem0's reported 68.5 graph score — but it is a vendor claim vs. a vendor claim.

### Cited Findings
- Letta (Aug 2025, [V]): filesystem agent with GPT-4o-mini scored 74.0% on LoCoMo vs Mem0's self-reported 68.5% (top graph variant); tools: grep, semantic search_files, open/close, answer_question; Letta could not reproduce Mem0's MemGPT run (no easy way to backfill LoCoMo into Letta) and says Mem0 did not respond; own caveats: partially autonomous with tool rules, minimal prompt tuning, weak model, LoCoMo a limited test — [Letta blog](https://www.letta.com/blog/benchmarking-ai-agent-memory)
- A roundup lists Letta at 83.2 on LoCoMo vs Mem0 92.5 (Mem0's own page lists Letta 74.0) — conflicting; basis unverified — [web search synthesis](https://mem0.ai/compare/mem0-vs-letta) [V]
- Zep vs Mem0 (May 2025): Zep's original ~84% was corrected to 75.14% after Mem0's CTO noted category 5 was removed from the denominator but kept in the numerator; Zep rebutted Mem0's setup (user role for both speakers, timestamps in message text, sequential searches) — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) citing [Zep blog](https://blog.getzep.com/lies-damn-lies-statistics-is-mem0-really-sota-in-agent-memory/) [S, all parties vendors]
- Audits (all vendor/blog authors): CoreDotToday found 52 wrong + 56 ambiguous answers (7.0% of 1,540; open-domain 30.2%); Penfield found 99 score-corrupting errors (6.4%, ceiling ~93.6%) and a judge accepting up to 62.81% of intentionally wrong answers; vague wrong answers passed 45.4% under Mem0's 2026 grader; re-grading the same 1,540 answers ranged 55.8%–81.4% depending only on prompt; no-conversation floor 25.9% — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]
- Full-context reproduction: gpt-4o-mini full conversation 74.0% (paper 72.90%); gemini-3.1-flash-lite 80.7% (2025 prompt) / 88.1% (2026 prompt); Gemini 2.5 Flash with no memory 72.8% on LoCoMo — [braindetox](https://braindetox.kr/en/posts/memory_benchmark_scores_reliability_2026.html) [S]; [search synthesis](https://mem0.ai/blog/ai-memory-benchmarks) [V]
- Independent conflict-resolution evidence contradicting the headline-score race: MemoryAgentBench and BEAM show near-zero multi-hop update/contradiction handling for all systems (section 2).

### Inferences
- "Filesystem + grep + semantic search matches memory frameworks" is supported by Letta's 74.0 and by MemoryAgentBench where BM25/long-context beat framework agents, but no neutral party has replicated Letta's run; treat as plausible, not established.
- Any score cited without answer model, judge model, judge prompt, category handling is not comparable.

### Gaps
- Did not read the Penfield/CoreDotToday/Mem0 "how to read benchmarks" primary posts or Mem0's response to Letta; Zep primary post not opened.

## 4. Coding/agentic workflow evidence (project files, context engineering, Anthropic)

### Takeaway
Repo context files (AGENTS.md/CLAUDE.md) do NOT reliably raise task success and cost >20% more; Anthropic's own numbers for memory tool + context editing are internal, undefined-baseline claims (+39% / +29%, -84% tokens in one eval). Anthropic's recommended pattern is a hybrid: small CLAUDE.md up front, grep/glob just-in-time, notes outside context.

### Cited Findings
- "Evaluating AGENTS.md" (Gloaguen et al., ETH, arXiv 2602.11988, Feb 2026, [I]): Claude Code Sonnet-4.5, Codex GPT-5.2/5.1-mini, Qwen Code; SWE-bench Lite + new AGENTbench (12 repos with developer-written context files); context files give no real improvement in success; inference cost up >20% on average; secondary summary: LLM-generated files -0.5 to -2%, developer-written ~+4% [S, unverified]; agents obey files and explore/test more; conclusion: describe only minimal requirements — [arXiv](https://arxiv.org/abs/2602.11988), [ICLR listing](https://iclr.cc/virtual/2026/10021142)
- Anthropic (Sept 2025, [V], internal): memory tool + context editing +39% over baseline, context editing alone +29% on internal agentic-search set; 100-turn web search eval: context editing cut tokens 84% and let agents finish workflows that otherwise failed on context exhaustion. Baseline, metric, sample size, model not stated. Memory tool is client-side file CRUD in a /memories directory, developer owns storage — [Claude blog](https://claude.com/blog/context-management)
- Anthropic "Effective context engineering" (Sept 2025): context rot (recall drops as window fills), finite attention budget (n² pairwise); techniques: compaction (Claude Code keeps architectural decisions, unresolved bugs, and the 5 most recent files), clearing old tool results as lightest compaction, structured note-taking (Pokémon agent keeps maps/progress notes across resets), just-in-time retrieval via lightweight references (glob, grep, head, tail), sub-agents returning 1,000–2,000 token summaries from tens of thousands of tokens of exploration; Claude Code is hybrid (CLAUDE.md up front + glob/grep). No benchmark numbers given — [Anthropic engineering](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents)
- Caution: besthub.dev attributes the 39%/84% to "Claude Code's folder-based memory"; not supported by Anthropic's post — [besthub](https://besthub.dev/articles/how-claude-code-s-folder-based-memory-boosts-performance-by-39-96636408bf45)

### Inferences
- For a coding agent, evidence favors short curated project files + on-demand retrieval over large auto-generated memory; auto-generated context is neutral-to-harmful.
- The Anthropic result is the only quantitative data on file-based memory with a frontier agent, and is unverifiable.

### Gaps
- No independent benchmark of memory frameworks (Mem0/Letta/Zep) on coding agents (SWE-bench-like) found; no study of cross-session coding memory specifically; Cursor/Windsurf memory features not covered.

## 5. Failure modes: stale memories, contradictions, retrieval noise, bloat

### Takeaway
Contradiction/update handling is the most consistently measured weakness across benchmarks; low-quality stored experience propagates errors, and pruning helps (~+10% abs).

### Cited Findings
- Xiong et al., "How Memory Management Impacts LLM Agents: Experience-Following Behavior" (arXiv 2505.16067, ACL 2026, [I]): error propagation (bad past records compound) and misaligned experience replay (similar-looking records don't help); "Combined Deletion" of redundant/low-quality records gives avg ~10% absolute gain across three agents/tasks — [arXiv](https://arxiv.org/abs/2505.16067v2)
- MemoryAgentBench: multi-hop selective forgetting ≤7% for all methods — [arXiv 2507.05257v3](https://arxiv.org/html/2507.05257v3). BEAM: contradiction resolution <0.05 at 100K–1M — [BEAM](https://arxiv.org/html/2510.27246v1)
- Zep's graph stores >600k tokens per LoCoMo conversation (vs 26k raw) — storage bloat; LangMem p95 search latency ~60 s — [Mem0 paper](https://arxiv.org/html/2504.19413) [V]
- STALE benchmark: current systems poor at judging that a memory is no longer valid (aggregator only) — [emergentmind stale](https://www.emergentmind.com/topics/stale.md) [S]
- Practitioner claim: precision of retrieval declining is the leading bloat indicator; an add-all study dropping to 13% accuracy after >2,400 records — [tianpan blog](https://tianpan.co/blog/2026-04-12-the-forgetting-problem-when-agent-memory-becomes-a-liability) [S, unverified]
- Retrieval dependence on grader/answer model: see section 3 (up to 12 point swing from judge model alone).

### Inferences
- Design implication: write-time dedup/supersede with timestamps and periodic pruning matter more than richer retrieval; plain dated notes files can implement supersession cheaply but no benchmark measures that.

### Gaps
- Original add-all and STALE papers not read; no quantified stale-memory rate for CLAUDE.md-style files.
