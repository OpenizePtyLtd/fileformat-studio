# Master Tasks Tracker: FileFormat AI Studio

> **Repository:** OpenizePtyLtd/fileformat-studio  
> **Issue Tracking:** Company Redmine Instance  
> **Tracker Purpose:** Single source of truth for all granular engineering tasks, architectural spikes, and feature implementation across project milestones.  
> **Issue Management Policy:** All engineering tasks are tracked in the company Redmine instance. Project connection details and credentials (`REDMINE_URL`, `REDMINE_API_KEY`, `REDMINE_PROJECT_ID`, `REDMINE_ASSIGN_TO_ID`, `REDMINE_USER`, `REDMINE_CATEGORY_ID`) are read dynamically from the untracked `.env` file to ensure private endpoints remain secure and separate from public source control.

---

## 1. Milestones Overview

| Milestone | Goal & Description | Target Deliverable |
| :--- | :--- | :--- |
| **M0: Project Foundation & Core Chat UI** | Initial solution setup, EF Core SQLite database layer, Universal LLM integration via `Microsoft.Extensions.AI`, DPAPI credential encryption, and multi-session streaming chat UI. | Production-grade local WinUI 3 foundation with secure LLM chat. |
| **M1: Document Parsing & Vector Engine Core** | Core architectural spikes, EF Core models, pluggable parsers (Aspose, DotNet OSS, Node.js officeparser), chunking, and in-process SIMD vector store. | Zero-config in-process parsing and vector similarity engine. |
| **M2: Knowledgebase Management & Ingestion UI** | Service orchestration, multi-file browsing, Knowledgebase manager and creation wizard UI with real-time indexing progress. | Full WinUI 3 Knowledgebase management workflow. |
| **M3: Multi-KB Chat Attachment & Grounded RAG** | Multi-knowledgebase attachment to chat sessions, grounded similarity retrieval, prompt augmentation, and source citations. | End-to-end multi-turn RAG chat with document citations. |
| **M4: Text Extraction Benchmark & Comparison Suite** | Multi-library extraction benchmark grouped by document categories (Word, Excel, PowerPoint, PDF, Text), multi-metric scoring (character volume, latency, memory, cleanliness), and side-by-side comparison UI. | Full in-app benchmark runner, scorecard comparison matrix, and side-by-side text diff inspector. |
| **M5: Format-Centric Parser Architecture & Modular Engine Decoupling** | Decouple monolithic parser suites into granular, format-centric engines (Word, Excel, PowerPoint, PDF, PlainText) with independent competitors, legacy fallback aliases, and pluggable runtime support. | Clean format-first parser architecture with granular scorecard ranking and seamless extensibility for future libraries. |
| **M6: Document Engine Configuration & Per-Category Preferences** | Dedicated Document Engine Configuration in Settings with per-category preference selection (Word, Excel, PowerPoint, PDF, Text), dynamic benchmark winner resolution, Auto selection, and ingestion pipeline integration replacing legacy broad dropdowns. | Fully configurable category-based document parser preference engine with automatic benchmark champion integration. |

---

## 2. Master Tasks Matrix

| Task ID | Issue | Group / Phase | Task Title | Milestone | Prerequisites | Status |
| :---: | :---: | :--- | :--- | :---: | :--- | :---: |
| **TASK-01** | #134034 | 0. Foundation & Architecture | `[RnD] Project Ideation & High-Level Architecture Specification` | M0 | None | ✅ Completed |
| **TASK-02** | #134035 | 0. Foundation & Architecture | `[Core] Solution Setup & Zero-Config Database Layer with EF Core & SQLite` | M0 | TASK-01 | ✅ Completed |
| **TASK-03** | #134036 | 0. Foundation & Architecture | `[AI] Universal LLM Integration with Microsoft.Extensions.AI & Provider Settings` | M0 | TASK-02 | ✅ Completed |
| **TASK-04** | #134037 | 0. Foundation & Architecture | `[UI/Chat] Multi-Session Chat UI & Real-Time Token Streaming in WinUI 3` | M0 | TASK-03 | ✅ Completed |
| **TASK-05** | #134038 | 0. Foundation & Architecture | `[Bugfix] Resolve Navigation Pane Click Collisions & Empty Session Redundancy` | M0 | TASK-04 | ✅ Completed |
| **TASK-09** | #134039 | 0. Foundation & Architecture | `[Core] Upgrade Target Framework to .NET 10 & Update NuGet Packages` | M0 | TASK-02 | ✅ Completed |
| **TASK-10** | #134040 | 0. Foundation & Architecture | `[Security] Secure LLM API Key Storage Using Windows DPAPI or Credential Locker` | M0 | TASK-02 | ✅ Completed |
| **TASK-11** | #134041 | 0. Foundation & Architecture | `[UI/Settings] Add Confirmation Dialog for Destructive Provider Deletion` | M0 | TASK-03 | ✅ Completed |
| **TASK-12** | #134042 | 0. Foundation & Architecture | `[UI/Settings] Settings Back Navigation, Frame History & Provider Safeguards` | M0 | TASK-04 | ✅ Completed |
| **TASK-13** | #134043 | 0. Foundation & Architecture | `feat(security): Implement Windows DPAPI DataProtectionService with legacy key compatibility` | M0 | TASK-10 | ✅ Completed |
| **TASK-14** | #134044 | 0. Foundation & Architecture | `feat(data): Add EF Core ValueConverter for ProviderConfigEntity.ApiKey` | M0 | TASK-13 | ✅ Completed |
| **TASK-15** | #134045 | 0. Foundation & Architecture | `feat(ui): Mask API Key input in SettingsPage with PasswordBox and reveal toggle` | M0 | TASK-14 | ✅ Completed |
| **TASK-16** | #134046 | 0. Foundation & Architecture | `[UI/Settings] Validate Provider URL and API Key Connectivity Before Saving` | M0 | TASK-03 | ✅ Completed |
| **TASK-17** | #134047 | 0. Foundation & Architecture | `[Data] Adopt EF Core Migrations for schema evolution without data loss` | M0 | TASK-02 | ✅ Completed |
| **TASK-18** | #134048 | 0. Foundation & Architecture | `[Data] Prevent accidental re-seeding of default providers when table is empty` | M0 | TASK-17 | ✅ Completed |
| **TASK-19** | #134049 | 0. Foundation & Architecture | `[Settings/UI] Allow saving provider configurations without mandatory live validation` | M0 | TASK-16 | ✅ Completed |
| **TASK-20** | #134050 | 0. Foundation & Architecture | `[UI/Chat] Send message on Enter key press in chat prompt textbox` | M0 | TASK-04 | ✅ Completed |
| **TASK-21** | #134051 | 0. Foundation & Architecture | `[UI/Chat] AI replies do not use full width and wrap into narrow lines` | M0 | TASK-04 | ✅ Completed |
| **TASK-22** | #134052 | 0. Foundation & Architecture | `[UI] Home Screen, Navigation & App Launch Experience` | M0 | TASK-05 | ✅ Completed |
| **TASK-23** | #134053 | 0. Foundation & Architecture | `[Build/UI] Automated date-based versioning and high-contrast version badge` | M0 | None | ✅ Completed |
| **TASK-24** | #134054 | 1. Architectural Decisions | `spike(rag): Evaluate & decide Vector DB engine (In-Process SQLite BLOB + SIMD vs sqlite-vec vs Embedded)` | M1 | None | ✅ Completed |
| **TASK-25** | #134055 | 1. Architectural Decisions | `spike(rag): Evaluate & decide Embedding Generation pipeline via Microsoft.Extensions.AI` | M1 | None | ✅ Completed |
| **TASK-26** | #134056 | 1. Architectural Decisions | `spike(parser): Design Node.js IPC runner for officeparser / node scripts in WinUI 3` | M1 | None | ✅ Completed |
| **TASK-27** | #134057 | 2. Data & Persistence | `feat(data): Implement Knowledgebase and Document Chunk EF Core Entities` | M1 | TASK-24 | ✅ Completed |
| **TASK-28** | #134058 | 2. Data & Persistence | `feat(data): Add EF Core migration for Knowledgebase & Vector schema` | M1 | TASK-27 | ✅ Completed |
| **TASK-29** | #134059 | 3. Parser Subsystem | `feat(parser): Implement IDocumentParser contract, metadata models, and DocumentParserFactory` | M1 | None | ✅ Completed |
| **TASK-30** | #134060 | 3. Parser Subsystem | `feat(parser): Implement AsposeDocumentParser (.NET Words, Cells, Slides, PDF)` | M1 | TASK-29 | ✅ Completed |
| **TASK-31** | #134061 | 3. Parser Subsystem | `feat(parser): Implement DotNetOssDocumentParser (OpenXML, PdfPig, ExcelDataReader)` | M1 | TASK-29 | ✅ Completed |
| **TASK-32** | #134062 | 3. Parser Subsystem | `feat(parser): Implement NodeJsDocumentParser runner with officeparser & JSON IPC` | M1 | TASK-26, TASK-29 | ✅ Completed |
| **TASK-33** | #134063 | 4. Chunking & Vector Search | `feat(rag): Implement TextChunker with token window and overlap` | M1 | TASK-29 | ✅ Completed |
| **TASK-34** | #134064 | 4. Chunking & Vector Search | `feat(rag): Implement VectorStoreService with SIMD Cosine Similarity search` | M1 | TASK-24, TASK-28, TASK-33 | ✅ Completed |
| **TASK-35** | #134065 | 5. Knowledgebase Services | `feat(service): Implement KnowledgebaseService & multi-file ingestion pipeline` | M2 | TASK-25, TASK-28, TASK-29, TASK-33, TASK-34 | ✅ Completed |
| **TASK-36** | #134066 | 6. Knowledgebase UI | `feat(ui): Build KnowledgebasePage and KnowledgebaseViewModel` | M2 | TASK-35 | ✅ Completed |
| **TASK-37** | #134067 | 6. Knowledgebase UI | `feat(ui): Build Knowledgebase Creation Wizard dialog with multi-file picker` | M2 | TASK-36 | ✅ Completed |
| **TASK-38** | #134068 | 6. Knowledgebase UI | `feat(ui): Implement real-time indexing progress UI for Knowledgebase creation` | M2 | TASK-37 | ✅ Completed |
| **TASK-39** | #134069 | 7. Multi-KB Chat & Citations | `feat(chat): Add Multi-KB selector chips to ChatPage and ChatViewModel` | M3 | TASK-35, TASK-36 | ✅ Completed |
| **TASK-40** | #134070 | 7. Multi-KB Chat & Citations | `feat(rag): Implement Grounded RAG query pipeline and source citations UI` | M3 | TASK-34, TASK-39 | ✅ Completed |
| **TASK-41** | #134071 | 8. Settings & Model Management | `feat(settings): Auto-validate models on add with dynamic embedding dimension probe` | M2 | None | ✅ Completed |
| **TASK-42** | #134072 | 6. Knowledgebase UI | `feat(ui): Allow adding documents to existing knowledgebase from KnowledgebasePage` | M2 | TASK-35, TASK-36 | ✅ Completed |
| **TASK-43** | #134073 | 6. Knowledgebase UI | `feat(kb): In-app document extracted plain text viewer and SQLite text persistence` | M2 | TASK-35, TASK-36 | ✅ Completed |
| **TASK-44** | #134074 | 6. Knowledgebase UI | `fix(ui): Fix extracted text viewer right-side clipping and add horizontal scrollbar & wrap toggle` | M2 | TASK-43 | ✅ Completed |
| **TASK-45** | #134075 | 9. Benchmark & Comparison | `[EPIC] Document Text Extraction Benchmark & Multi-Library Comparison Suite` | M4 | None | ✅ Completed |
| **TASK-46** | #134076 | 9. Benchmark & Comparison | `feat(benchmark): Design Document Category & Format Registry with IBenchmarkMetric abstraction` | M4 | None | ✅ Completed |
| **TASK-47** | #134077 | 9. Benchmark & Comparison | `feat(data): Implement EF Core entities and migrations for Benchmark sessions, runs, and metric results` | M4 | TASK-46 | ✅ Completed |
| **TASK-48** | #134078 | 9. Benchmark & Comparison | `feat(benchmark): Implement BenchmarkRunnerService for multi-parser parallel execution and scoring engine` | M4 | TASK-46, TASK-47 | ✅ Completed |
| **TASK-49** | #134079 | 9. Benchmark & Comparison | `feat(benchmark): Implement Core Benchmark Metrics (Character Count, Speed, Memory, Text Cleanliness)` | M4 | TASK-46 | ✅ Completed |
| **TASK-50** | #134080 | 9. Benchmark & Comparison | `feat(ui): Build BenchmarkPage with Category & Format filter and document upload runner` | M4 | TASK-46, TASK-48 | ✅ Completed |
| **TASK-51** | #134081 | 9. Benchmark & Comparison | `feat(ui): Implement Benchmark Results Comparison Matrix and Winner Scorecard` | M4 | TASK-48, TASK-49, TASK-50 | ✅ Completed |
| **TASK-52** | #134082 | 9. Benchmark & Comparison | `feat(ui): Implement Side-by-Side Extracted Text Comparison & Diff Inspector Dialog` | M4 | TASK-48, TASK-51 | ✅ Completed |
| **TASK-53** | #134083 | 9. Benchmark & Comparison | `test(benchmark): Add comprehensive unit and integration tests for BenchmarkRunner and Metrics` | M4 | TASK-48, TASK-49 | ✅ Completed |
| **TASK-65** | #134084 | 9. Benchmark & Comparison | `feat(ui): implement Benchmark Dashboard with Overall Champion & Category-Wise Winners` | M4 | TASK-50, TASK-51 | ✅ Completed |
| **TASK-66** | #134357 | 10. Format-Centric Parser Architecture | `refactor(parser): enrich IDocumentParser with DocumentCategory and update factory for category queries and legacy alias resolution` | M5 | TASK-29 | ✅ Completed |
| **TASK-67** | #134359 | 10. Format-Centric Parser Architecture | `feat(parser): implement granular AsposeWordsParser and OpenXmlWordParser engines for Word category` | M5 | TASK-66 | ✅ Completed |
| **TASK-68** | #134360 | 10. Format-Centric Parser Architecture | `feat(parser): implement granular AsposePdfParser and PdfPigParser engines for PDF category` | M5 | TASK-66 | ✅ Completed |
| **TASK-69** | #134361 | 10. Format-Centric Parser Architecture | `feat(parser): implement granular AsposeCellsParser, ExcelDataReaderParser, and CsvHelperParser engines for Excel category` | M5 | TASK-66 | ✅ Completed |
| **TASK-70** | #134362 | 10. Format-Centric Parser Architecture | `feat(parser): implement granular AsposeSlidesParser engine for PowerPoint category` | M5 | TASK-66 | ✅ Completed |
| **TASK-71** | #134363 | 10. Format-Centric Parser Architecture | `refactor(benchmark): register granular category parsers in DI and update Benchmark UI scorecards` | M5 | TASK-67, TASK-68, TASK-69, TASK-70 | ✅ Completed |
| **TASK-72** | #134364 | 10. Format-Centric Parser Architecture | `test(parser): add unit tests for granular category parsers, factory category queries, and legacy alias fallbacks` | M5 | TASK-71 | ✅ Completed |
| **TASK-73** | #134375 | 11. Document Engine Configuration & Preferences | `feat(parser): implement IDocumentEnginePreferenceService with category preferences and benchmark winner resolution` | M6 | TASK-66 | ✅ Completed |
| **TASK-74** | #134376 | 11. Document Engine Configuration & Preferences | `feat(benchmark): integrate benchmark winner recording with document engine preferences` | M6 | TASK-73, TASK-65 | ✅ Completed |
| **TASK-75** | #134377 | 11. Document Engine Configuration & Preferences | `refactor(kb): remove parser engine selector from Knowledgebase creation wizard and clean up KB header UI` | M6 | TASK-37 | ✅ Completed |
| **TASK-76** | #134378 | 11. Document Engine Configuration & Preferences | `feat(kb): integrate category-based document engine resolution into multi-file knowledgebase ingestion pipeline` | M6 | TASK-73, TASK-75 | ✅ Completed |
| **TASK-77** | #134379 | 11. Document Engine Configuration & Preferences | `feat(settings): implement Document Engine Configuration UI and ViewModel for category parser preferences` | M6 | TASK-73, TASK-74 | ✅ Completed |
| **TASK-78** | #134380 | 11. Document Engine Configuration & Preferences | `test(parser): add unit and integration tests for document engine preferences, KB ingestion, and Settings ViewModel` | M6 | TASK-76, TASK-77 | ✅ Completed |
| **TASK-79** | #134480 | 9. Benchmark & Comparison | `fix(ui): resolve blank benchmark scorecard cards by correcting ErrorMessage visibility binding` | M4 | TASK-65, TASK-71 | ✅ Completed |
| **TASK-80** | #134481 | 9. Benchmark & Comparison | `feat(benchmark): implement dual-pane in-text search and navigation in benchmark extracted text diff dialog` | M4 | TASK-52 | ✅ Completed |
| **TASK-81** | #134482 | 9. Benchmark & Comparison | `feat(benchmark): add in-text search highlighting and scrollbar match markers in diff inspector` | M4 | TASK-80 | ✅ Completed |
| **TASK-82** | #134483 | 9. Benchmark & Comparison | `fix(benchmark): enable dynamic word wrap by binding ScrollViewer horizontal scroll mode and visibility in diff inspector` | M4 | TASK-81 | ✅ Completed |
| **TASK-83** | #134488 | 9. Benchmark & Comparison | `feat(benchmark): rebalance benchmark metrics to extraction-dominant weight distribution` | M4 | TASK-49 | ✅ Completed |
| **TASK-84** | #134489 | 11. Document Engine Configuration & Preferences | `feat(license): display Aspose evaluation mode warnings and license status indicators across document workflows` | M6 | TASK-77, TASK-83 | ✅ Completed |
| **TASK-85** | #134490 | 11. Document Engine Configuration & Preferences | `feat(settings): implement in-app Aspose license file upload and dynamic activation in Settings` | M6 | TASK-84 | ✅ Completed |
| **TASK-86** | #134491 | 11. Document Engine Configuration & Preferences | `feat(parser): dynamic Aspose priority based on active license mode` | M6 | TASK-84 | ✅ Completed |
| **TASK-87** | #134497 | 12. Document Libraries Catalog & Stats | `feat(libraries): dedicated document libraries catalog with package stats and license management` | M6 | TASK-85, TASK-86 | ✅ Completed |
| **TASK-88** | #134498 | 12. Document Libraries Catalog & Stats | `feat(libraries): persist refreshed package stats to local app data cache` | M6 | TASK-87 | ✅ Completed |

---

## 3. Execution Sequence & Dependency Graph

Follow the dependency flow below to know which task can be picked next without waiting on unresolved prerequisites:

```mermaid
flowchart TD
    subgraph Foundation ["Phase 0: Project Foundation & Core Chat (M0)"]
        T01["TASK-01 (#134034)<br/>Ideation & Arch"]
        T02["TASK-02 (#134035)<br/>EF Core & SQLite"]
        T03["TASK-03 (#134036)<br/>Universal LLM"]
        T04["TASK-04 (#134037)<br/>Multi-Session Chat UI"]
        T10["TASK-10 (#134040)<br/>DPAPI Secret Key"]
        T13["TASK-13 (#134043)<br/>DPAPI Service"]
        T14["TASK-14 (#134044)<br/>ApiKey Converter"]
        T15["TASK-15 (#134045)<br/>Masked Key UI"]
        T17["TASK-17 (#134047)<br/>EF Core Migrations"]

        T01 --> T02 --> T03 --> T04
        T02 --> T10 --> T13 --> T14 --> T15
        T02 --> T17
    end

    subgraph Spikes ["Phase 1: Architectural Spikes & Contracts (M1)"]
        T24["TASK-24 (#134054)<br/>Vector DB Decision"]
        T25["TASK-25 (#134055)<br/>Embedding Strategy"]
        T26["TASK-26 (#134056)<br/>Node.js IPC Arch"]
        T29["TASK-29 (#134059)<br/>IDocumentParser Contract"]
    end

    subgraph DataAndParsers ["Phase 2 & 3: Data & Parsers (M1)"]
        T27["TASK-27 (#134057)<br/>EF Core KB & Chunks"]
        T28["TASK-28 (#134058)<br/>Vector Schema Migration"]
        T30["TASK-30 (#134060)<br/>Aspose Parser"]
        T31["TASK-31 (#134061)<br/>DotNet OSS Parser"]
        T32["TASK-32 (#134062)<br/>NodeJs officeparser"]
        T33["TASK-33 (#134063)<br/>Text Chunker"]
    end

    subgraph VectorEngine ["Phase 4: Vector Storage (M1)"]
        T34["TASK-34 (#134064)<br/>SIMD VectorStoreService"]
    end

    subgraph IngestionAndUI ["Phase 5 & 6: Ingestion & WinUI 3 (M2)"]
        T35["TASK-35 (#134065)<br/>KnowledgebaseService Pipeline"]
        T36["TASK-36 (#134066)<br/>KnowledgebasePage UI"]
        T37["TASK-37 (#134067)<br/>Creation Wizard & File Picker"]
        T38["TASK-38 (#134068)<br/>Indexing Progress UI"]
        T42["TASK-42 (#134072)<br/>Add Docs to KB"]
        T43["TASK-43 (#134073)<br/>Extracted Text Viewer"]
        T44["TASK-44 (#134074)<br/>Text Viewer Layout Fix"]
    end

    subgraph ChatRAG ["Phase 7: Grounded Chat & Citations (M3)"]
        T39["TASK-39 (#134069)<br/>Multi-KB Chat Attachment"]
        T40["TASK-40 (#134070)<br/>Grounded Retrieval & Citations"]
    end

    subgraph BenchmarkM4 ["Phase 9: Extraction Benchmark & Comparison (M4)"]
        T45["TASK-45 (#134075)<br/>[EPIC] Benchmark Suite"]
        T46["TASK-46 (#134076)<br/>Category & Metric Abstractions"]
        T47["TASK-47 (#134077)<br/>EF Core Benchmark Schema"]
        T48["TASK-48 (#134078)<br/>BenchmarkRunnerService"]
        T49["TASK-49 (#134079)<br/>Core Metric Evaluators"]
        T50["TASK-50 (#134080)<br/>BenchmarkPage View & Upload"]
        T51["TASK-51 (#134081)<br/>Comparison Matrix UI"]
        T52["TASK-52 (#134082)<br/>Side-by-Side Diff Dialog"]
        T53["TASK-53 (#134083)<br/>Automated Benchmark Tests"]
        T65["TASK-65 (#134084)<br/>Benchmark Dashboard & Winner"]
        T79["TASK-79 (#134480)<br/>Scorecard ErrorMessage Binding Fix"]
        T80["TASK-80 (#134481)<br/>Dual-Pane Search &amp; Nav in Diff"]
        T81["TASK-81 (#134482)<br/>Search Highlighting &amp; Scrollbar Markers"]
        T82["TASK-82 (#134483)<br/>Dynamic Word Wrap &amp; ScrollMode Fix"]
        T83["TASK-83 (#134488)<br/>Extraction-Dominant Weights"]
    end

    %% M1 Dependencies
    T24 --> T27 --> T28
    T26 --> T32
    T29 --> T30
    T29 --> T31
    T29 --> T32
    T29 --> T33

    T24 --> T34
    T28 --> T34
    T33 --> T34

    %% M2 Dependencies
    T25 --> T35
    T28 --> T35
    T29 --> T35
    T33 --> T35
    T34 --> T35

    T35 --> T36
    T36 --> T37
    T37 --> T38
    T36 --> T42
    T36 --> T43 --> T44

    %% M3 Dependencies
    T35 --> T39
    T36 --> T39
    T34 --> T40
    T39 --> T40

    %% M4 Benchmark Dependencies
    T45 --> T46
    T46 --> T47 --> T48
    T46 --> T49 --> T51
    T46 --> T50
    T48 --> T50
    T48 --> T51
    T48 --> T52
    T51 --> T52
    T48 --> T53
    T49 --> T53
    T50 --> T65
    T51 --> T65
    T65 --> T79
    T52 --> T80
    T80 --> T81
    T81 --> T82
    T49 --> T83
    end

    subgraph ModularParsersM5 ["Phase 10: Format-Centric Parser Architecture (M5)"]
        T66["TASK-66 (#134357)<br/>IDocumentParser Category &amp; Alias"]
        T67["TASK-67 (#134359)<br/>Word: Aspose.Words &amp; OpenXML"]
        T68["TASK-68 (#134360)<br/>PDF: Aspose.PDF &amp; PdfPig"]
        T69["TASK-69 (#134361)<br/>Excel: Cells, ExcelDataRdr, CsvHelper"]
        T70["TASK-70 (#134362)<br/>PowerPoint: Aspose.Slides"]
        T71["TASK-71 (#134363)<br/>DI &amp; Benchmark UI Scorecards"]
        T72["TASK-72 (#134364)<br/>Comprehensive Tests &amp; Verification"]
    end

    subgraph DocEngineConfigM6 ["Phase 11: Document Engine Preferences (M6)"]
        T73["TASK-73 (#134375)<br/>IDocumentEnginePreferenceService"]
        T74["TASK-74 (#134376)<br/>Benchmark Winner Recording"]
        T75["TASK-75 (#134377)<br/>Remove KB Wizard Parser Dropdown"]
        T76["TASK-76 (#134378)<br/>Category Parser Ingestion Pipeline"]
        T77["TASK-77 (#134379)<br/>Settings Document Engine UI &amp; VM"]
        T78["TASK-78 (#134380)<br/>Preferences &amp; Pipeline Tests"]
        T84["TASK-84 (#134489)<br/>Aspose Evaluation Mode Warnings"]
        T85["TASK-85 (#134490)<br/>Settings In-App License Upload"]
        T86["TASK-86 (#134491)<br/>Dynamic Aspose Priority"]
        T87["TASK-87 (#134497)<br/>Document Libraries Catalog &amp; Stats"]
        T88["TASK-88 (#134498)<br/>Package Stats Cache Persistence"]
    end

    %% M5 Dependencies
    T29 --> T66
    T66 --> T67
    T66 --> T68
    T66 --> T69
    T66 --> T70
    T67 --> T71
    T68 --> T71
    T69 --> T71
    T70 --> T71
    T71 --> T72

    %% M6 Dependencies
    T66 --> T73
    T73 --> T74
    T65 --> T74
    T37 --> T75
    T73 --> T76
    T75 --> T76
    T73 --> T77
    T74 --> T77
    T76 --> T78
    T77 --> T78
    T77 --> T84
    T83 --> T84
    T84 --> T85
    T84 --> T86
    T85 --> T87
    T86 --> T87
    T87 --> T88
```

---

## 4. Engineering & Commit Conventions

When working on any task from this tracker:

1. **Issue Tracking & Filtering in Redmine**:
   - All active and new engineering issues are maintained in the company **Redmine instance** under category `fileformat-studio`.
   - **Credentials & Settings**: Project configuration and credentials (`REDMINE_URL`, `REDMINE_API_KEY`, `REDMINE_PROJECT_ID`, `REDMINE_ASSIGN_TO_ID`, `REDMINE_USER`, `REDMINE_CATEGORY_ID`) must be read dynamically from `.env` in the repository root (untracked and gitignored).
   - **Issue Creation**: When filing an issue via API, assign to `REDMINE_ASSIGN_TO_ID` (`23`) and set `category_id` to `261` (`fileformat-studio`). Do **not** prefix the subject with project name or task number (use clean titles directly).
2. **Branch Naming**:
   - `feat/rm-XXXXX-<short-name>` (e.g. `feat/rm-134062-nodejs-officeparser-ipc`)
   - `spike/rm-XXXXX-<short-name>` (e.g. `spike/rm-134056-node-runner-design`)
   - `fix/rm-XXXXX-<short-name>`
3. **Conventional Commits**:
   - `feat(parser): implement officeparser IPC runner (refs #134062)`
   - `fix(ui): resolve layout truncation on text inspector (fixes #134074)`
   - `docs(tracker): update sprint tasks (refs #134062)`
4. **Closing Issues**:
   - Update the issue status in Redmine to `Closed` (Status ID: `5`) via REST API or by including `fixes #XXXXX` / `closes #XXXXX` in the commit message.
5. **Updating the Tracker**:
   - Update the status icon in `docs/master_tasks.md` when starting (`🔄 In Progress`) and upon completion (`✅ Completed`).
