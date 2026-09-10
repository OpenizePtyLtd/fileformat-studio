# ADR 0002: Unified Embedding Generation Pipeline via Microsoft.Extensions.AI

> **Status:** Accepted  
> **Date:** September 2026  
> **Author:** Architecture & Core RAG Team  
> **Context Issue:** [TASK-02 (#25)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/25)  
> **Target Framework:** .NET 10 (WinUI 3 Windows App SDK)  
> **Supersedes / Related:** Relates to [TASK-01 (#24)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/24), [TASK-04 (#27)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/27), [TASK-11 (#34)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/34), and [TASK-12 (#35)](https://github.com/OpenizePtyLtd/fileformat-studio/issues/35)

---

## 1. Context and Problem Statement

In FileFormat AI Studio, Retrieval-Augmented Generation (RAG) requires converting text chunks into high-dimensional numerical vectors (embeddings). Users should have the freedom to select:
1. **Cloud Embedding Models**: e.g., OpenAI `text-embedding-3-small` or `text-embedding-3-large` for high precision without local hardware consumption.
2. **Local Embedding Models**: e.g., Ollama running `nomic-embed-text`, `bge-m3`, or `all-minilm` for 100% offline, private, zero-cost embedding generation.

Different embedding models output vectors with differing dimensionality (e.g. 1536 for OpenAI, 768 for Nomic, 1024 for BGE-M3, 384 for MiniLM). Additionally, local vs cloud endpoints have varying connection requirements, token rate limits, and latency profiles.

We need an embedding generation architecture that:
1. Provides a single, uniform abstraction across cloud and local providers.
2. Standardizes vector dimension discovery, validation, and storage.
3. Decouples chunk ingestion (`KnowledgebaseService`) from provider-specific SDK idiosyncrasies.
4. Allows testing and validating embedding model health before indexing large collections.

---

## 2. Decision

We will standardize on **`Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>`** as the core embedding interface, resolved via **`AIClientFactory.CreateEmbeddingGenerator`**.

Specifically:

1. **Standardized Contract:**
   All embedding generation will consume `IEmbeddingGenerator<string, Embedding<float>>`. This provides:
   - Batch embedding generation (`GenerateAsync(IEnumerable<string>, ...)`).
   - Single vector generation (`GenerateEmbeddingVectorAsync(string, ...)`).
   - Built-in cancellation token support and standardized metadata extraction (`Embedding.Vector`).

2. **Unified OpenAI-Compatible Transport:**
   Both OpenAI cloud and local providers (Ollama, LM Studio, LocalAI) expose OpenAI-compatible HTTP REST endpoints (`/v1/embeddings`).
   - We utilize `OpenAIClient.GetEmbeddingClient(modelId).AsIEmbeddingGenerator()` from `Microsoft.Extensions.AI.OpenAI`.
   - For cloud OpenAI, the client targets `https://api.openai.com/v1` with the user's DPAPI-secured API key.
   - For local Ollama, the client targets the configured endpoint (default `http://localhost:11434/v1`) with a dummy or local token.

3. **Dimensionality Registry & Discovery:**
   - A central `EmbeddingModelMetadata` registry cataloging known models and default dimensions:
     - `text-embedding-3-small`: 1,536 dimensions
     - `text-embedding-3-large`: 3,072 dimensions
     - `text-embedding-ada-002`: 1,536 dimensions
     - `nomic-embed-text`: 768 dimensions
     - `bge-m3`: 1,024 dimensions
     - `bge-small-en-v1.5`: 384 dimensions
     - `all-minilm`: 384 dimensions
     - `mxbai-embed-large`: 1,024 dimensions
   - For custom or unlisted models, `TestEmbeddingGenerationAsync` sends a ping request to dynamically inspect the returned `vector.Length`.

4. **Ingestion & Search Dimension Validation Guards:**
   - When a Knowledgebase is created, its `VectorDimensions` is recorded in `KnowledgebaseEntity`.
   - During chunk indexing in `KnowledgebaseService` (TASK-12), the dimensionality of generated vectors is verified against `KnowledgebaseEntity.VectorDimensions`. Any mismatch triggers a hard failure before corrupt vectors are written to SQLite.
   - During query execution in `VectorStoreService` (TASK-11), the query vector dimension is matched against candidate vectors.

---

## 3. Options Considered & Evaluation Summary

| Criteria | Microsoft.Extensions.AI (Chosen) | Raw OpenAI .NET SDK | Custom HttpClient / REST |
| :--- | :--- | :--- | :--- |
| **Abstractions** | Official Microsoft AI standard | Locked to OpenAI types | Proprietary custom wrapper |
| **Local Ollama Support** | Native via `/v1` endpoint | Native via custom `Endpoint` | Manual JSON serialization |
| **Batching & Pipelines** | Native `GenerateAsync` batching | Requires manual batch loop | Manual implementation |
| **Extensibility** | Middleware, caching, telemetry | Custom decorators required | Manual |
| **Maintenance Burden** | Maintained by Microsoft .NET team | Maintained by OpenAI | High internal maintenance |

---

## 4. Downstream Impact

- **TASK-12 (`KnowledgebaseService`):** Uses `AIClientFactory.CreateEmbeddingGenerator` to batch-generate chunk embeddings during document indexing.
- **TASK-14 (`Knowledgebase Creation Wizard UI`):** Uses `EmbeddingModelMetadata.RecommendedModels` to present a unified model selection dropdown with provider badges and dimension previews.
- **TASK-17 (`Grounded RAG Query Pipeline`):** Uses the embedding generator to embed user search prompts before executing SIMD cosine similarity search in `VectorStoreService`.

