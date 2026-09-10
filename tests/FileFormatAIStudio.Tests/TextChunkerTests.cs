using System;
using System.Linq;
using FileFormatAIStudio.Services.Knowledgebase;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class TextChunkerTests
    {
        private readonly TextChunker _chunker = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \t\r\n   ")]
        public void ChunkText_NullOrWhiteSpace_ReturnsEmptyList(string? input)
        {
            var chunks = _chunker.ChunkText(input!);
            chunks.Should().BeEmpty();
        }

        [Fact]
        public void ChunkText_ShortText_ReturnsSingleChunkWithZeroIndex()
        {
            string text = "This is a brief single sentence document.";
            var options = new ChunkingOptions { TargetChunkSizeTokens = 500, OverlapTokens = 50 };

            var chunks = _chunker.ChunkText(text, options);

            chunks.Should().HaveCount(1);
            var chunk = chunks[0];
            chunk.SequenceIndex.Should().Be(0);
            chunk.Content.Should().Be(text);
            chunk.CharacterStart.Should().Be(0);
            chunk.CharacterEnd.Should().Be(text.Length);
            chunk.EstimatedTokenCount.Should().BeGreaterThan(0);
            chunk.Id.Should().NotBeEmpty();
        }

        [Fact]
        public void ChunkText_MultiSentenceParagraph_ProducesSequentialChunks()
        {
            // Build text with multiple sentences
            var sentences = Enumerable.Range(1, 40)
                .Select(i => $"Sentence {i} contains important business and financial facts.")
                .ToList();
            string text = string.Join(" ", sentences);

            var options = new ChunkingOptions
            {
                TargetChunkSizeTokens = 30, // Small window to force multiple chunks
                OverlapTokens = 10,
                MinChunkSizeTokens = 5
            };

            var chunks = _chunker.ChunkText(text, options);

            chunks.Should().HaveCountGreaterThan(1);
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].SequenceIndex.Should().Be(i);
                chunks[i].Content.Should().NotBeNullOrWhiteSpace();
                chunks[i].CharacterStart.Should().BeGreaterThanOrEqualTo(0);
                chunks[i].CharacterEnd.Should().BeGreaterThan(chunks[i].CharacterStart);
            }
        }

        [Fact]
        public void ChunkText_PreservesSentenceBoundaries_WithoutCuttingWordsMidway()
        {
            string s1 = "Alpha beta gamma delta epsilon.";
            string s2 = "Zeta eta theta iota kappa.";
            string s3 = "Lambda mu nu xi omicron.";
            string text = $"{s1} {s2} {s3}";

            var options = new ChunkingOptions
            {
                TargetChunkSizeTokens = 10, // each sentence is ~7-8 tokens
                OverlapTokens = 0,
                MinChunkSizeTokens = 1
            };

            var chunks = _chunker.ChunkText(text, options);

            foreach (var chunk in chunks)
            {
                // Each chunk should cleanly contain entire sentences and end with period
                chunk.Content.TrimEnd().Should().EndWith(".");
            }
        }

        [Fact]
        public void ChunkText_WithOverlap_SharesOverlappingContent()
        {
            var sentences = Enumerable.Range(1, 20)
                .Select(i => $"Section {i} establishes protocol details.")
                .ToList();
            string text = string.Join(" ", sentences);

            var options = new ChunkingOptions
            {
                TargetChunkSizeTokens = 25,
                OverlapTokens = 12,
                MinChunkSizeTokens = 5
            };

            var chunks = _chunker.ChunkText(text, options);

            chunks.Count.Should().BeGreaterThan(2);

            // Chunk 1 should contain content from the end of Chunk 0
            bool hasOverlap = false;
            for (int i = 1; i < chunks.Count; i++)
            {
                var prevWords = chunks[i - 1].Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var lastWordsOfPrev = string.Join(" ", prevWords.TakeLast(3));

                if (chunks[i].Content.Contains(lastWordsOfPrev))
                {
                    hasOverlap = true;
                    break;
                }
            }

            hasOverlap.Should().BeTrue("overlapping chunks should share sentences from previous chunk");
        }

        [Fact]
        public void ChunkText_DeterministicId_SameInputsProduceIdenticalGuids()
        {
            string text = "Consistent document content that must yield repeatable hashes.";
            var docId = Guid.NewGuid();
            var options = new ChunkingOptions { DocumentId = docId, TargetChunkSizeTokens = 50 };

            var chunksFirst = _chunker.ChunkText(text, options);
            var chunksSecond = _chunker.ChunkText(text, options);

            chunksFirst.Should().HaveCount(1);
            chunksSecond.Should().HaveCount(1);
            chunksFirst[0].Id.Should().Be(chunksSecond[0].Id);
        }

        [Fact]
        public void ChunkText_DeterministicId_DifferentDocumentOrIndexProducesDifferentGuids()
        {
            string text = "Same sentence content.";
            var docA = Guid.NewGuid();
            var docB = Guid.NewGuid();

            var chunkA = _chunker.ChunkText(text, new ChunkingOptions { DocumentId = docA })[0];
            var chunkB = _chunker.ChunkText(text, new ChunkingOptions { DocumentId = docB })[0];

            chunkA.Id.Should().NotBe(chunkB.Id);
        }

        [Fact]
        public void ChunkText_MinChunkSize_MergesSmallTrailingFragment()
        {
            // Create text where the end has a small fragment
            string longPart = "This is a substantial first sentence with many words that easily fills up the main chunk quota.";
            string tinyPart = "Tiny tail.";
            string text = $"{longPart} {tinyPart}";

            var options = new ChunkingOptions
            {
                TargetChunkSizeTokens = 24,
                OverlapTokens = 0,
                MinChunkSizeTokens = 10 // tinyPart is ~3 tokens, so should be merged
            };

            var chunks = _chunker.ChunkText(text, options);

            // Without merging it would be 2 chunks; with merging it stays merged or the last chunk has >= 10 tokens
            if (chunks.Count > 1)
            {
                chunks.Last().EstimatedTokenCount.Should().BeGreaterThanOrEqualTo(options.MinChunkSizeTokens);
            }
            else
            {
                chunks[0].Content.Should().Contain(tinyPart);
            }
        }

        [Fact]
        public void ChunkText_OversizeUnbrokenString_DoesNotLoopInfinitelyAndSplitsSafely()
        {
            // 500 characters unbroken string with no spaces or punctuation
            string unbroken = new('X', 500);

            var options = new ChunkingOptions
            {
                TargetChunkSizeTokens = 20, // ~80 characters max
                OverlapTokens = 5
            };

            var chunks = _chunker.ChunkText(unbroken, options);

            chunks.Should().HaveCountGreaterThan(1);
            foreach (var chunk in chunks)
            {
                chunk.Content.Should().NotBeNullOrWhiteSpace();
            }
        }

        [Fact]
        public void ChunkText_CustomTokenEstimator_IsRespected()
        {
            string text = "Word one. Word two. Word three.";
            // Custom estimator: count words directly
            int customCalled = 0;
            Func<string, int> estimator = s =>
            {
                customCalled++;
                return s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            };

            var options = new ChunkingOptions
            {
                TokenEstimator = estimator,
                TargetChunkSizeTokens = 2,
                OverlapTokens = 0
            };

            var chunks = _chunker.ChunkText(text, options);

            customCalled.Should().BeGreaterThan(0);
            chunks.Count.Should().BeGreaterThan(1);
        }

        [Fact]
        public void ChunkText_CharacterOffsets_MatchOriginalTextSubstring()
        {
            string text = "Paragraph one starts here. Paragraph two follows. And here is paragraph three with more details.";
            var options = new ChunkingOptions
            {
                TargetChunkSizeTokens = 8,
                OverlapTokens = 2
            };

            var chunks = _chunker.ChunkText(text, options);

            foreach (var chunk in chunks)
            {
                string slice = text.Substring(chunk.CharacterStart, chunk.CharacterEnd - chunk.CharacterStart);
                slice.Trim().Should().Be(chunk.Content);
            }
        }
    }
}
