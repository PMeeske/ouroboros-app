# Qdrant Sync Impact on Conversational Memory Retrieval

**Date:** 2026-02-25
**Branch:** `claude/check-qdrant-sync-memory-wgSwt`

## Executive Summary

**The Qdrant sync features do NOT compromise local conversational memory retrieval.** The sync is a one-way push from local Qdrant to Qdrant Cloud and never modifies local vector data. However, there are architectural concerns around the verify path and potential future risks if the cloud endpoint were ever used as a retrieval source.

---

## Architecture Overview

### Memory Retrieval Path (unaffected by sync)

```
User query
  -> PersonalityEngine.RecallConversationsAsync()
     -> IEmbeddingModel.CreateEmbeddingsAsync(query)
     -> QdrantClient.SearchAsync("ouroboros_conversations", embedding)  // LOCAL Qdrant only
     -> Returns ConversationMemory[]

User query
  -> PersistentConversationMemory.SearchMemoryAsync()
     -> IEmbeddingModel.CreateEmbeddingsAsync(query)
     -> QdrantClient.SearchAsync(collectionName, embedding)            // LOCAL Qdrant only
     -> Falls back to SearchMemoryLocal() on failure
```

Both retrieval paths (`PersonalityEngine` at `PersonalityEngine.cs:443` and `PersistentConversationMemory` at `PersistentConversationMemory.cs:194`) use the **local** `QdrantClient` (gRPC, port 6334). Neither references any cloud endpoint.

### Sync Path (write-only to cloud)

```
QdrantSyncService.SyncAsync() / QdrantSyncTool.SyncAsync()
  -> ScrollPointsAsync(localClient, ...)          // READ from local
  -> EcVectorCrypto.EncryptPerIndex(floats, id)   // Encrypt plaintext vectors
  -> EcVectorCrypto.ComputeVectorHmac(floats, id) // HMAC of plaintext
  -> UpsertEncryptedPointsAsync(cloudClient, ...)  // WRITE to cloud
```

The sync reads from local, encrypts, and pushes to cloud. **No local data is modified.**

---

## Findings

### 1. Local Memory Retrieval Is Intact

| Component | Collection | Endpoint | Verdict |
|-----------|-----------|----------|---------|
| `PersonalityEngine.RecallConversationsAsync` | `ouroboros_conversations` | Local (gRPC) | Safe |
| `PersistentConversationMemory.SearchMemoryAsync` | Configurable (default `ouroboros_conversations`) | Local (gRPC) | Safe |
| `PersistentConversationMemory.GetActiveHistory` | N/A (in-memory) | N/A | Safe |
| `RecallHandler` (MediatR) | Via PersonalityEngine | Local (gRPC) | Safe |

### 2. Cloud Vectors Are Encrypted (By Design)

The sync feature encrypts vectors using EC P-256 per-index keystream encryption before uploading to cloud. This means:

- **Cloud vectors cannot be used for similarity search** - cosine similarity on encrypted floats is meaningless
- The cloud acts purely as an **encrypted backup**, not a queryable store
- Payload metadata (role, content, timestamps) is preserved in plaintext alongside encrypted vectors

### 3. Potential Concern: Verify Logic (Needs Engine Submodule)

**Location:** `QdrantSyncService.cs:266` and `QdrantSyncTool.cs:639`

The verify path reads encrypted vectors from cloud and passes them to `VerifyVectorHmac()`:

```csharp
// During SYNC (QdrantSyncService.cs:461-462):
var encrypted = _crypto.EncryptPerIndex(floats, pointId);  // encrypt plaintext
var hmac = _crypto.ComputeVectorHmac(floats, pointId);      // HMAC of PLAINTEXT

// During VERIFY (QdrantSyncService.cs:266):
// floats here are ENCRYPTED (read from cloud)
if (_crypto.VerifyVectorHmac(floats, pointId, storedHmac))  // verify on ENCRYPTED
```

The comment at `QdrantSyncTool.cs:638` says *"Verify: decrypt + recompute HMAC"*, suggesting `VerifyVectorHmac` internally decrypts before comparing. Since `EcVectorCrypto` lives in the `libs/engine` submodule (not initialized in this clone), this cannot be confirmed directly. **If `VerifyVectorHmac` does NOT decrypt internally, verification would always report false corruption.**

### 4. Collections Included in Sync

The sync targets collections matching `ouroboros_*` prefix OR in the hardcoded `IsKnownCollection` list:

```
core, fullcore, codebase, prefix_cache, tools,
qdrant_documentation, pipeline_vectors, distinction_states,
episodic_memory, network_state_snapshots, network_learnings
```

This includes `episodic_memory` (used for long-term recall) and all `ouroboros_*` collections including `ouroboros_conversations`. These collections' **local data is only read, never modified** during sync.

### 5. No Tests for Sync or Memory Retrieval Interaction

There are no test files matching `*Qdrant*`, `*Sync*`, or `*Memory*` in the `tests/` directory that cover the interaction between sync and memory retrieval. This is a gap.

---

## Risk Matrix

| Risk | Severity | Likelihood | Mitigation |
|------|----------|------------|------------|
| Local memory retrieval broken by sync | N/A | None | Paths are fully isolated |
| Cloud used as retrieval source (future) | High | Low | Encrypted vectors break similarity search |
| Verify reports false corruption | Medium | Unknown | Depends on `EcVectorCrypto.VerifyVectorHmac` internal implementation |
| Silent failure in memory indexing | Low | Medium | Both paths catch and swallow exceptions |
| Cloud sync overwrites without conflict resolution | Medium | Low | Sync is full overwrite (upsert), no merge |

---

## Recommendations

1. **Initialize the engine submodule** and audit `EcVectorCrypto.VerifyVectorHmac` to confirm it decrypts before HMAC comparison
2. **Add integration tests** covering:
   - Memory retrieval works correctly before and after a sync operation
   - Verify correctly identifies intact vs corrupted vectors
3. **Add a guard** preventing cloud endpoints from being used as retrieval sources (or add decryption to the retrieval path if cloud retrieval is desired)
4. **Reduce exception swallowing** in `PersistentConversationMemory` and `PersonalityEngine` to improve observability

---

## Conclusion

The Qdrant sync features operate on a completely separate data path from conversational memory retrieval. Local Qdrant data is read-only during sync, and all memory retrieval uses local Qdrant exclusively. **Conversational memory retrieval is not compromised by the sync features.**
