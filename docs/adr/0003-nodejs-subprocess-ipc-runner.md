# ADR 0003: Multi-Library Node.js Subprocess IPC Architecture & OfficeParser Foundation

> **Status:** Accepted  
> **Date:** September 2026  
> **Author:** Architecture & Parser Engineering Team  
> **Context Issue:** [TASK-26 (#134056)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/26)  
> **Target Framework:** .NET 10 (WinUI 3 Windows App SDK) & Node.js 18+  
> **Related:** [TASK-29 (#134059)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/29), [TASK-32 (#134062)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/32), [TASK-66 (#134357)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/66)

---

## 1. Context and Problem Statement

FileFormat AI Studio features a pluggable, format-centric parser subsystem (`IDocumentParser`) that currently extracts structured text from documents using .NET native libraries (Aspose and Open-Source .NET libraries like OpenXML, PdfPig, ExcelDataReader, and CsvHelper).

However, the broader document engineering ecosystem contains popular and capable Node.js libraries—including `officeparser` (universal Office/OpenDocument/PDF parser), `mammoth` (.docx to clean semantic HTML/Markdown), `xlsx` / SheetJS (Excel), and `pdf-parse`.

To support these libraries alongside .NET parsers in a WinUI 3 desktop application:
1. **Crash Isolation**: Complex third-party JavaScript libraries or native Node addons must never destabilize or crash the WinUI 3 desktop host process.
2. **Multi-Library Extensibility**: The system must not hardcode one-off subprocess scripts for single libraries. Adding a new Node.js library should require zero modifications to the core IPC layer or UI.
3. **Graceful Degradation**: Users without Node.js installed should experience zero crashes; Node engines must report `IsAvailable = false` and cleanly defer to .NET engines.
4. **Subprocess Lifecycle & Orphan Prevention**: Child `node.exe` processes must terminate immediately on cancellation, timeouts, or unexpected app exit (no lingering background zombies).

---

## 2. Decision

We will adopt a **Decoupled Node.js Subprocess IPC Architecture with a Unified NodeHost Dispatcher, Base64/File Request Envelopes, and Windows Job Object Process Containment**.

Specifically:

### 2.1 NodeHost Subsystem Structure (`FileFormatAIStudio/NodeHost/`)
The Node.js environment is contained within a dedicated sub-project with its own `package.json`:
- **Unified Dispatcher (`index.js`)**: Entrypoint handling CLI args, Base64 payload decoding, JSON-RPC streaming, and global uncaught exception trapping.
- **Engine Registry (`lib/registry.js`)**: Dynamically discovers and routes requests to registered engines by `engineId` or file extension.
- **Engine Contract (`lib/baseEngine.js`)**: Abstract base class defining `extractText(filePath, options)` and `extractMetadata(filePath)`.
- **Protocol Serialization (`lib/protocol.js`)**: Enforces structured JSON response envelopes `{ requestId, success, text, characterCount, executionMs, metadata, error }`.
- **Concrete Engines (`engines/`)**: Pluggable modules extending `BaseEngine` (e.g. `OfficeParserEngine` wrapping `officeparser`).

### 2.2 C# Integration & Process Containment (`Services/Parsing/Node/`)
- **Runtime Discovery (`NodeJsRuntimeService`)**:
  - Resolves `node.exe` via: (1) user-configured path in Settings, (2) system `PATH` scanning, (3) standard Windows directories (`Program Files\nodejs`, `%LOCALAPPDATA%\Programs\node`, NVM).
  - Probes Node.js version and registered engines via `node index.js --probe`.
  - Discovers `NodeHost/index.js` in development trees and packaged outputs.
- **IPC Execution & Job Objects (`NodeJsHostService` & `JobObjectHelper`)**:
  - Executes requests with `ProcessStartInfo` (UTF-8 encoding, hidden window).
  - Automatically binds each spawned `node.exe` process to a Windows Job Object (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`), ensuring that if FileFormatAIStudio closes or crashes, all child Node processes terminate instantly.
  - Supports `CancellationToken` and configurable timeouts (60s default).
- **First-Class .NET Integration (`NodeJsDocumentParserBase : IDocumentParser`)**:
  - Exposes `Category`, `EngineId`, `DisplayName`, `Priority`, and `SupportedExtensions`.
  - Integrates transparently with `IDocumentParserFactory`, the Benchmark Suite comparison matrix, and Document Engine Preferences.

---

## 3. Options Considered & Evaluation

| Evaluation Criteria | Option 1: Ad-hoc Single Scripts per Library | Option 2: Embedded Edge.js / ClearScript V8 | Option 3: Decoupled NodeHost Subprocess (Chosen) |
| :--- | :--- | :--- | :--- |
| **Crash Isolation** | Moderate (subprocess) | ❌ In-process V8 crash kills WinUI 3 | ✅ Complete OS process isolation |
| **Multi-Library Support** | ❌ Sprawling, fragmented scripts | ❌ Difficult to load full npm ecosystem | ✅ Clean plugin registry (`BaseEngine`) |
| **Zombie Process Risk** | ❌ High (orphaned on app crash) | N/A | ✅ Zero (Windows Job Object containment) |
| **Node.js Compatibility** | Limited | ❌ Stale Node/V8 APIs | ✅ 100% native Node.js ecosystem |
| **Extensibility Cost** | High (new C# runner per tool) | High (C++/C# marshaling) | ✅ Low (drop in a JS engine & C# wrapper) |

---

## 4. Consequences and Validation

### Positive
- **Future-Proof**: Adding any new Node.js tool (e.g. `mammoth`, `xlsx`, `pdf-parse`) takes under 30 lines of JavaScript and a thin C# subclass of `NodeJsDocumentParserBase`.
- **Bulletproof Stability**: Any parser crash, memory leak, or infinite loop in a Node library is isolated from WinUI 3.
- **Seamless Benchmarking**: Node.js parsers participate directly in the Benchmark Suite alongside Aspose and OpenXML.

### Negative & Mitigation
- **Cold Start Latency**: Spawning a new `node.exe` process incurs ~100–200ms startup latency.  
  *Mitigation:* Negligible for document ingestion (chunking & embedding take much longer); NodeHost supports interactive NDJSON streaming mode for future high-throughput batching.
- **Node.js Dependency**: Requires Node.js installed on the user's computer.  
  *Mitigation:* App gracefully detects missing Node runtime (`IsAvailable = false`), displays actionable instructions in Settings, and automatically falls back to .NET native parsers.
