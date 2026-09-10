using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Splits raw document text into semantically coherent chunks using sliding-window token budgeting,
    /// paragraph and sentence boundary preservation, and deterministic chunk ID generation.
    /// </summary>
    public sealed class TextChunker : ITextChunker
    {
        private static readonly Regex SentenceDelimiterRegex = new(
            @"(?<=[.!?])\s+|(?<=\r?\n\r?\n)|(?<=\r?\n)",
            RegexOptions.Compiled
        );

        /// <inheritdoc />
        public IReadOnlyList<TextChunk> ChunkText(string text, ChunkingOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<TextChunk>();
            }

            options ??= new ChunkingOptions();

            int targetTokens = Math.Max(1, options.TargetChunkSizeTokens);
            int overlapTokens = Math.Max(0, options.OverlapTokens);
            if (overlapTokens >= targetTokens)
            {
                overlapTokens = Math.Max(0, targetTokens - 1);
            }

            Func<string, int> tokenEstimator = options.TokenEstimator ?? DefaultTokenEstimator;

            // 1. Break text into atomic semantic segments (sentences/paragraphs) with exact character boundaries
            List<TextSegment> segments = SegmentText(text, targetTokens, tokenEstimator);
            if (segments.Count == 0)
            {
                return Array.Empty<TextChunk>();
            }

            // 2. Accumulate segments into chunks using sliding window with overlap
            List<TextChunk> chunks = new();
            int currentIndex = 0;
            int sequenceIndex = 0;

            while (currentIndex < segments.Count)
            {
                int startSegmentIndex = currentIndex;
                int accumulatedTokens = 0;
                int endSegmentIndex = currentIndex;

                while (endSegmentIndex < segments.Count)
                {
                    var seg = segments[endSegmentIndex];
                    if (accumulatedTokens + seg.TokenCount > targetTokens && endSegmentIndex > startSegmentIndex)
                    {
                        break;
                    }

                    accumulatedTokens += seg.TokenCount;
                    endSegmentIndex++;
                }

                int charStart = segments[startSegmentIndex].StartIndex;
                int charEnd = segments[endSegmentIndex - 1].EndIndex;
                string chunkContent = text.Substring(charStart, charEnd - charStart).Trim();

                if (!string.IsNullOrWhiteSpace(chunkContent))
                {
                    int estimatedTokens = tokenEstimator(chunkContent);
                    Guid chunkId = GenerateDeterministicId(options.DocumentId, sequenceIndex, chunkContent);

                    chunks.Add(new TextChunk
                    {
                        Id = chunkId,
                        SequenceIndex = sequenceIndex,
                        Content = chunkContent,
                        CharacterStart = charStart,
                        CharacterEnd = charEnd,
                        EstimatedTokenCount = estimatedTokens
                    });

                    sequenceIndex++;
                }

                if (endSegmentIndex >= segments.Count)
                {
                    break;
                }

                // Calculate overlap backtracking
                int targetOverlap = Math.Min(overlapTokens, accumulatedTokens);
                int backtrackTokens = 0;
                int nextStartIndex = endSegmentIndex;

                for (int i = endSegmentIndex - 1; i > startSegmentIndex; i--)
                {
                    backtrackTokens += segments[i].TokenCount;
                    nextStartIndex = i;
                    if (backtrackTokens >= targetOverlap)
                    {
                        break;
                    }
                }

                // Guarantee strict forward progress to prevent infinite loops
                if (nextStartIndex <= startSegmentIndex)
                {
                    nextStartIndex = startSegmentIndex + 1;
                }

                currentIndex = nextStartIndex;
            }

            // 3. Handle trailing tiny chunk by merging into previous chunk if below MinChunkSizeTokens
            if (chunks.Count > 1 && options.MinChunkSizeTokens > 0)
            {
                var lastChunk = chunks[^1];
                if (lastChunk.EstimatedTokenCount < options.MinChunkSizeTokens)
                {
                    var prevChunk = chunks[^2];
                    int mergedStart = prevChunk.CharacterStart;
                    int mergedEnd = Math.Max(prevChunk.CharacterEnd, lastChunk.CharacterEnd);
                    string mergedContent = text.Substring(mergedStart, mergedEnd - mergedStart).Trim();
                    int mergedTokens = tokenEstimator(mergedContent);
                    Guid mergedId = GenerateDeterministicId(options.DocumentId, prevChunk.SequenceIndex, mergedContent);

                    chunks[^2] = prevChunk with
                    {
                        Content = mergedContent,
                        CharacterEnd = mergedEnd,
                        EstimatedTokenCount = mergedTokens,
                        Id = mergedId
                    };

                    chunks.RemoveAt(chunks.Count - 1);
                }
            }

            return chunks;
        }

        private static List<TextSegment> SegmentText(string text, int targetTokens, Func<string, int> tokenEstimator)
        {
            var segments = new List<TextSegment>();
            var matches = SentenceDelimiterRegex.Matches(text);

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                int segmentEnd = match.Index + match.Length;
                if (segmentEnd > lastIndex)
                {
                    string rawSegment = text.Substring(lastIndex, segmentEnd - lastIndex);
                    AddSegmentWithSubSplitting(segments, text, rawSegment, lastIndex, targetTokens, tokenEstimator);
                    lastIndex = segmentEnd;
                }
            }

            if (lastIndex < text.Length)
            {
                string remainder = text.Substring(lastIndex);
                AddSegmentWithSubSplitting(segments, text, remainder, lastIndex, targetTokens, tokenEstimator);
            }

            return segments;
        }

        private static void AddSegmentWithSubSplitting(
            List<TextSegment> segments,
            string fullText,
            string rawSegment,
            int startIndex,
            int targetTokens,
            Func<string, int> tokenEstimator)
        {
            if (string.IsNullOrWhiteSpace(rawSegment))
            {
                return;
            }

            int estimatedTokens = tokenEstimator(rawSegment);

            // If the segment fits within target token budget, keep it atomic
            if (estimatedTokens <= targetTokens)
            {
                segments.Add(new TextSegment(rawSegment, startIndex, startIndex + rawSegment.Length, estimatedTokens));
                return;
            }

            // Oversize segment (e.g. massive unbroken block of text or code) -> sub-split by words
            string[] words = rawSegment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1)
            {
                // Single huge unbroken word or symbol string -> hard split by character length
                int maxChars = Math.Max(10, targetTokens * 4);
                for (int offset = 0; offset < rawSegment.Length; offset += maxChars)
                {
                    int length = Math.Min(maxChars, rawSegment.Length - offset);
                    string sub = rawSegment.Substring(offset, length);
                    segments.Add(new TextSegment(sub, startIndex + offset, startIndex + offset + length, tokenEstimator(sub)));
                }
                return;
            }

            // Sub-split by word boundaries
            int currentWordOffset = 0;
            StringBuilder currentWordBuffer = new();
            int subSegmentStart = startIndex;

            foreach (string word in words)
            {
                int wordPosInSegment = rawSegment.IndexOf(word, currentWordOffset, StringComparison.Ordinal);
                if (wordPosInSegment < 0)
                {
                    wordPosInSegment = currentWordOffset;
                }

                string candidate = currentWordBuffer.Length == 0 ? word : $"{currentWordBuffer} {word}";
                int candidateTokens = tokenEstimator(candidate);

                if (candidateTokens > targetTokens && currentWordBuffer.Length > 0)
                {
                    string content = currentWordBuffer.ToString();
                    segments.Add(new TextSegment(content, subSegmentStart, startIndex + wordPosInSegment, tokenEstimator(content)));
                    currentWordBuffer.Clear();
                    currentWordBuffer.Append(word);
                    subSegmentStart = startIndex + wordPosInSegment;
                }
                else
                {
                    if (currentWordBuffer.Length > 0)
                    {
                        currentWordBuffer.Append(' ');
                    }
                    currentWordBuffer.Append(word);
                }

                currentWordOffset = wordPosInSegment + word.Length;
            }

            if (currentWordBuffer.Length > 0)
            {
                string content = currentWordBuffer.ToString();
                segments.Add(new TextSegment(content, subSegmentStart, startIndex + rawSegment.Length, tokenEstimator(content)));
            }
        }

        private static int DefaultTokenEstimator(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            // Common NLP / BPE heuristic: ~4 characters per token in English
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }

        private static Guid GenerateDeterministicId(Guid? documentId, int sequenceIndex, string content)
        {
            string payload = $"{documentId?.ToString("D") ?? Guid.Empty.ToString("D")}:{sequenceIndex}:{content.Trim()}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return new Guid(hash.AsSpan(0, 16));
        }

        private sealed record TextSegment(string Text, int StartIndex, int EndIndex, int TokenCount);
    }
}
