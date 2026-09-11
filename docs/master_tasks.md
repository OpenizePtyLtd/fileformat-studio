# Master Tasks Tracker: FileFormat AI Studio

> **Repository:** [OpenizePtyLtd/fileformat-studio](https://github.com/OpenizePtyLtd/fileformat-studio)  
> **Tracker Purpose:** Single source of truth for all granular engineering tasks, architectural spikes, and feature implementation across GitHub issues and project milestones.

---

## 1. Milestones Overview

| Milestone | Goal & Description | Target Deliverable |
| :--- | :--- | :--- |
| **M1: Document Parsing & Vector Engine Core** | Core architectural spikes, EF Core models, pluggable parsers (Aspose, DotNet OSS, Node.js officeparser), chunking, and in-process SIMD vector store. | Zero-config in-process parsing and vector similarity engine. |
| **M2: Knowledgebase Management & Ingestion UI** | Service orchestration, multi-file browsing, Knowledgebase manager and creation wizard UI with real-time indexing progress. | Full WinUI 3 Knowledgebase management workflow. |
| **M3: Multi-KB Chat Attachment & Grounded RAG** | Multi-knowledgebase attachment to chat sessions, grounded similarity retrieval, prompt augmentation, and source citations. | End-to-end multi-turn RAG chat with document citations. |
| **M4: Text Extraction Benchmark & Comparison Suite** | Multi-library extraction benchmark grouped by document categories (Word, Excel, PowerPoint, PDF, Text), multi-metric scoring (character volume, latency, memory, cleanliness), and side-by-side comparison UI. | Full in-app benchmark runner, scorecard comparison matrix, and side-by-side text diff inspector. |

---

## 2. Master Tasks Matrix

| Task ID | Issue | Group / Phase | Task Title | Milestone | Prerequisites | Status |
| :---: | :---: | :--- | :--- | :--- | :--- | :---: |
| **TASK-01** | [#24](https://github.com/OpenizePtyLtd/fileformat-studio/issues/24) | 1. Architectural Decisions | `spike(rag): Evaluate & decide Vector DB engine (In-Process SQLite BLOB + SIMD vs sqlite-vec vs Embedded)` | M1 | None | ✅ Completed |
| **TASK-02** | [#25](https://github.com/OpenizePtyLtd/fileformat-studio/issues/25) | 1. Architectural Decisions | `spike(rag): Evaluate & decide Embedding Generation pipeline via Microsoft.Extensions.AI` | M1 | None | ✅ Completed |
| **TASK-03** | [#26](https://github.com/OpenizePtyLtd/fileformat-studio/issues/26) | 1. Architectural Decisions | `spike(parser): Design Node.js IPC runner for officeparser / node scripts in WinUI 3` | M1 | None | ⏳ Pending |
| **TASK-04** | [#27](https://github.com/OpenizePtyLtd/fileformat-studio/issues/27) | 2. Data & Persistence | `feat(data): Implement Knowledgebase and Document Chunk EF Core Entities` | M1 | TASK-01 | ✅ Completed |
| **TASK-05** | [#28](https://github.com/OpenizePtyLtd/fileformat-studio/issues/28) | 2. Data & Persistence | `feat(data): Add EF Core migration for Knowledgebase & Vector schema` | M1 | TASK-04 | ✅ Completed |
| **TASK-06** | [#29](https://github.com/OpenizePtyLtd/fileformat-studio/issues/29) | 3. Parser Subsystem | `feat(parser): Implement IDocumentParser contract, metadata models, and DocumentParserFactory` | M1 | None | ✅ Completed |
| **TASK-07** | [#30](https://github.com/OpenizePtyLtd/fileformat-studio/issues/30) | 3. Parser Subsystem | `feat(parser): Implement AsposeDocumentParser (.NET Words, Cells, Slides, PDF)` | M1 | TASK-06 | ✅ Completed |
| **TASK-08** | [#31](https://github.com/OpenizePtyLtd/fileformat-studio/issues/31) | 3. Parser Subsystem | `feat(parser): Implement DotNetOssDocumentParser (OpenXML, PdfPig, ExcelDataReader)` | M1 | TASK-06 | ✅ Completed |
| **TASK-09** | [#32](https://github.com/OpenizePtyLtd/fileformat-studio/issues/32) | 3. Parser Subsystem | `feat(parser): Implement NodeJsDocumentParser runner with officeparser & JSON IPC` | M1 | TASK-03, TASK-06 | ⏳ Pending |
| **TASK-10** | [#33](https://github.com/OpenizePtyLtd/fileformat-studio/issues/33) | 4. Chunking & Vector Search | `feat(rag): Implement TextChunker with token window and overlap` | M1 | TASK-06 | ✅ Completed |
| **TASK-11** | [#34](https://github.com/OpenizePtyLtd/fileformat-studio/issues/34) | 4. Chunking & Vector Search | `feat(rag): Implement VectorStoreService with SIMD Cosine Similarity search` | M1 | TASK-01, TASK-05, TASK-10 | ✅ Completed |
| **TASK-12** | [#35](https://github.com/OpenizePtyLtd/fileformat-studio/issues/35) | 5. Knowledgebase Services | `feat(service): Implement KnowledgebaseService & multi-file ingestion pipeline` | M2 | TASK-02, TASK-05, TASK-06, TASK-10, TASK-11 | ✅ Completed |
| **TASK-13** | [#36](https://github.com/OpenizePtyLtd/fileformat-studio/issues/36) | 6. Knowledgebase UI | `feat(ui): Build KnowledgebasePage and KnowledgebaseViewModel` | M2 | TASK-12 | ✅ Completed |
| **TASK-14** | [#37](https://github.com/OpenizePtyLtd/fileformat-studio/issues/37) | 6. Knowledgebase UI | `feat(ui): Build Knowledgebase Creation Wizard dialog with multi-file picker` | M2 | TASK-13 | ✅ Completed |
| **TASK-15** | [#38](https://github.com/OpenizePtyLtd/fileformat-studio/issues/38) | 6. Knowledgebase UI | `feat(ui): Implement real-time indexing progress UI for Knowledgebase creation` | M2 | TASK-14 | ✅ Completed |
| **TASK-16** | [#39](https://github.com/OpenizePtyLtd/fileformat-studio/issues/39) | 7. Multi-KB Chat & Citations | `feat(chat): Add Multi-KB selector chips to ChatPage and ChatViewModel` | M3 | TASK-12, TASK-13 | ✅ Completed |
| **TASK-17** | [#40](https://github.com/OpenizePtyLtd/fileformat-studio/issues/40) | 7. Multi-KB Chat & Citations | `feat(rag): Implement Grounded RAG query pipeline and source citations UI` | M3 | TASK-11, TASK-16 | ✅ Completed |
| **TASK-18** | [#41](https://github.com/OpenizePtyLtd/fileformat-studio/issues/41) | 8. Settings & Model Management | `feat(settings): Auto-validate models on add with dynamic embedding dimension probe` | M2 | None | ✅ Completed |
| **TASK-19** | [#42](https://github.com/OpenizePtyLtd/fileformat-studio/issues/42) | 6. Knowledgebase UI | `feat(ui): Allow adding documents to existing knowledgebase from KnowledgebasePage` | M2 | TASK-12, TASK-13 | ✅ Completed |
| **TASK-20** | [#46](https://github.com/OpenizePtyLtd/fileformat-studio/issues/46) | 9. Benchmark & Comparison | `feat(benchmark): Design Document Category & Format Registry with IBenchmarkMetric abstraction` | M4 | None | ✅ Completed |
| **TASK-21** | [#47](https://github.com/OpenizePtyLtd/fileformat-studio/issues/47) | 9. Benchmark & Comparison | `feat(data): Implement EF Core entities and migrations for Benchmark sessions, runs, and metric results` | M4 | TASK-20 | ✅ Completed |
| **TASK-22** | [#48](https://github.com/OpenizePtyLtd/fileformat-studio/issues/48) | 9. Benchmark & Comparison | `feat(benchmark): Implement BenchmarkRunnerService for multi-parser parallel execution and scoring engine` | M4 | TASK-20, TASK-21 | ✅ Completed |
| **TASK-23** | [#49](https://github.com/OpenizePtyLtd/fileformat-studio/issues/49) | 9. Benchmark & Comparison | `feat(benchmark): Implement Core Benchmark Metrics (Character Count, Speed, Memory, Text Cleanliness)` | M4 | TASK-20 | ✅ Completed |
| **TASK-24** | [#50](https://github.com/OpenizePtyLtd/fileformat-studio/issues/50) | 9. Benchmark & Comparison | `feat(ui): Build BenchmarkPage with Category & Format filter and document upload runner` | M4 | TASK-20, TASK-22 | ✅ Completed |
| **TASK-25** | [#51](https://github.com/OpenizePtyLtd/fileformat-studio/issues/51) | 9. Benchmark & Comparison | `feat(ui): Implement Benchmark Results Comparison Matrix and Winner Scorecard` | M4 | TASK-22, TASK-23, TASK-24 | 🔄 In Progress |
| **TASK-26** | [#52](https://github.com/OpenizePtyLtd/fileformat-studio/issues/52) | 9. Benchmark & Comparison | `feat(ui): Implement Side-by-Side Extracted Text Comparison & Diff Inspector Dialog` | M4 | TASK-22, TASK-25 | ⏳ Pending |
| **TASK-27** | [#53](https://github.com/OpenizePtyLtd/fileformat-studio/issues/53) | 9. Benchmark & Comparison | `test(benchmark): Add comprehensive unit and integration tests for BenchmarkRunner and Metrics` | M4 | TASK-22, TASK-23 | ⏳ Pending |

---

## 3. Execution Sequence & Dependency Graph

Follow the dependency flow below to know which task can be picked next without waiting on unresolved prerequisites:

```mermaid
flowchart TD
    subgraph Spikes ["Phase 1: Architectural Spikes & Contracts"]
        T01["TASK-01 (#24)<br/>Vector DB Decision"]
        T02["TASK-02 (#25)<br/>Embedding Strategy"]
        T03["TASK-03 (#26)<br/>Node.js IPC Architecture"]
        T06["TASK-06 (#29)<br/>IDocumentParser Contract"]
    end

    subgraph DataAndParsers ["Phase 2 & 3: Data & Parsers"]
        T04["TASK-04 (#27)<br/>EF Core Entities"]
        T05["TASK-05 (#28)<br/>EF Core Migration"]
        T07["TASK-07 (#30)<br/>Aspose Parser"]
        T08["TASK-08 (#31)<br/>DotNet OSS Parser"]
        T09["TASK-09 (#32)<br/>NodeJs officeparser"]
        T10["TASK-10 (#33)<br/>Text Chunker"]
    end

    subgraph VectorEngine ["Phase 4: Vector Storage"]
        T11["TASK-11 (#34)<br/>SIMD VectorStoreService"]
    end

    subgraph IngestionAndUI ["Phase 5 & 6: Ingestion & WinUI 3"]
        T12["TASK-12 (#35)<br/>KnowledgebaseService Pipeline"]
        T13["TASK-13 (#36)<br/>KnowledgebasePage UI"]
        T14["TASK-14 (#37)<br/>Creation Wizard & File Picker"]
        T15["TASK-15 (#38)<br/>Indexing Progress UI"]
    end

    subgraph ChatRAG ["Phase 7: Grounded Chat & Citations"]
        T16["TASK-16 (#39)<br/>Multi-KB Chat Attachment"]
        T17["TASK-17 (#40)<br/>Grounded Retrieval & Citations"]
    end

    subgraph BenchmarkM4 ["Phase 9: Extraction Benchmark & Comparison (M4)"]
        T20["TASK-20 (#46)<br/>Category & Metric Abstractions"]
        T21["TASK-21 (#47)<br/>EF Core Benchmark Schema"]
        T22["TASK-22 (#48)<br/>BenchmarkRunnerService"]
        T23["TASK-23 (#49)<br/>Core Metric Evaluators"]
        T24["TASK-24 (#50)<br/>BenchmarkPage View & Upload"]
        T25["TASK-25 (#51)<br/>Comparison Matrix UI"]
        T26["TASK-26 (#52)<br/>Side-by-Side Diff Dialog"]
        T27["TASK-27 (#53)<br/>Automated Benchmark Tests"]
    end

    %% Dependencies
    T01 --> T04 --> T05
    T03 --> T09
    T06 --> T07
    T06 --> T08
    T06 --> T09
    T06 --> T10

    T01 --> T11
    T05 --> T11
    T10 --> T11

    T02 --> T12
    T05 --> T12
    T06 --> T12
    T10 --> T12
    T11 --> T12

    T12 --> T13
    T13 --> T14
    T14 --> T15

    T12 --> T16
    T13 --> T16
    T11 --> T17
    T16 --> T17

    %% M4 Benchmark Dependencies
    T20 --> T21 --> T22
    T20 --> T23 --> T25
    T20 --> T24
    T22 --> T24
    T22 --> T25
    T22 --> T26
    T25 --> T26
    T22 --> T27
    T23 --> T27
```

---

## 4. Engineering & Commit Conventions

When working on any task from this tracker:

1. **Branch Naming**:
   - `feat/task-XX-<short-name>` (e.g. `feat/task-06-document-parser-contract`)
   - `spike/task-XX-<short-name>` (e.g. `spike/task-01-vector-db-eval`)
2. **Conventional Commits**:
   - `feat(parser): implement IDocumentParser contract and factory (#29)`
   - `feat(data): add KB and chunk entities (#27)`
   - `spike(rag): benchmark SQLite BLOB vs sqlite-vec (#24)`
   - `fix(chat): ...`
3. **Closing Issues**:
   - Reference the issue in PR descriptions and commit messages using `Resolves #XX` or `Closes #XX`.
4. **Updating the Tracker**:
   - Update the status icon in `docs/master_tasks.md` when starting (`🔄 In Progress`) and upon completion (`✅ Completed`).

