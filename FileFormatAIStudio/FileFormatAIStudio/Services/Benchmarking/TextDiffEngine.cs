using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFormatAIStudio.Services.Benchmarking
{
    public enum TextDiffKind
    {
        Unchanged,
        UniqueToA,
        UniqueToB
    }

    public class TextDiffItem
    {
        public int? LineNumberA { get; set; }
        public int? LineNumberB { get; set; }
        public string Content { get; set; } = string.Empty;
        public TextDiffKind Kind { get; set; }

        public string Prefix => Kind switch
        {
            TextDiffKind.UniqueToA => "+ [A] ",
            TextDiffKind.UniqueToB => "- [B] ",
            _ => "      "
        };

        public string LineNumberDisplay
        {
            get
            {
                string a = LineNumberA.HasValue ? LineNumberA.Value.ToString() : "-";
                string b = LineNumberB.HasValue ? LineNumberB.Value.ToString() : "-";
                return $"{a,5} | {b,5}";
            }
        }
    }

    public class TextDiffSummary
    {
        public long CharactersA { get; set; }
        public long CharactersB { get; set; }
        public long CharacterDelta => CharactersA - CharactersB;

        public long WordsA { get; set; }
        public long WordsB { get; set; }
        public long WordDelta => WordsA - WordsB;

        public int LinesA { get; set; }
        public int LinesB { get; set; }
        public int LineDelta => LinesA - LinesB;

        public int UniqueLinesA { get; set; }
        public int UniqueLinesB { get; set; }

        public double CharacterAdvantagePercentage => CharactersB == 0
            ? (CharactersA > 0 ? 100.0 : 0.0)
            : ((double)(CharactersA - CharactersB) / CharactersB) * 100.0;
    }

    /// <summary>
    /// Line-by-line diff engine implementing Longest Common Subsequence (LCS)
    /// to compare extracted text streams across competing parsers.
    /// </summary>
    public static class TextDiffEngine
    {
        public static (List<TextDiffItem> DiffItems, TextDiffSummary Summary) Compare(string textA, string textB)
        {
            textA ??= string.Empty;
            textB ??= string.Empty;

            var linesA = SplitLines(textA);
            var linesB = SplitLines(textB);

            long wordsA = CountWords(textA);
            long wordsB = CountWords(textB);

            var diffItems = ComputeLcsDiff(linesA, linesB);

            int uniqueA = diffItems.Count(d => d.Kind == TextDiffKind.UniqueToA);
            int uniqueB = diffItems.Count(d => d.Kind == TextDiffKind.UniqueToB);

            var summary = new TextDiffSummary
            {
                CharactersA = textA.Length,
                CharactersB = textB.Length,
                WordsA = wordsA,
                WordsB = wordsB,
                LinesA = linesA.Count,
                LinesB = linesB.Count,
                UniqueLinesA = uniqueA,
                UniqueLinesB = uniqueB
            };

            return (diffItems, summary);
        }

                                        private static List<string> SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        }

        private static long CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length;
        }

        private static List<TextDiffItem> ComputeLcsDiff(List<string> linesA, List<string> linesB)
        {
            int n = linesA.Count;
            int m = linesB.Count;

            // Cap LCS matrix size for performance on massive outputs (max 1200 x 1200)
            if (n > 1200 || m > 1200)
            {
                return ComputeFastLineDiff(linesA, linesB);
            }

            int[,] dp = new int[n + 1, m + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (string.Equals(linesA[i], linesB[j], StringComparison.Ordinal))
                    {
                        dp[i + 1, j + 1] = dp[i, j] + 1;
                    }
                    else
                    {
                        dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    }
                }
            }

            var result = new List<TextDiffItem>();
            int currI = n, currJ = m;

            while (currI > 0 || currJ > 0)
            {
                if (currI > 0 && currJ > 0 && string.Equals(linesA[currI - 1], linesB[currJ - 1], StringComparison.Ordinal))
                {
                    result.Add(new TextDiffItem
                    {
                        LineNumberA = currI,
                        LineNumberB = currJ,
                        Content = linesA[currI - 1],
                        Kind = TextDiffKind.Unchanged
                    });
                    currI--;
                    currJ--;
                }
                else if (currJ > 0 && (currI == 0 || dp[currI, currJ - 1] >= dp[currI - 1, currJ]))
                {
                    result.Add(new TextDiffItem
                    {
                        LineNumberA = null,
                        LineNumberB = currJ,
                        Content = linesB[currJ - 1],
                        Kind = TextDiffKind.UniqueToB
                    });
                    currJ--;
                }
                else if (currI > 0 && (currJ == 0 || dp[currI, currJ - 1] < dp[currI - 1, currJ]))
                {
                    result.Add(new TextDiffItem
                    {
                        LineNumberA = currI,
                        LineNumberB = null,
                        Content = linesA[currI - 1],
                        Kind = TextDiffKind.UniqueToA
                    });
                    currI--;
                }
            }

            result.Reverse();
            return result;
        }

        private static List<TextDiffItem> ComputeFastLineDiff(List<string> linesA, List<string> linesB)
        {
            var result = new List<TextDiffItem>();
            int max = Math.Max(linesA.Count, linesB.Count);

            for (int i = 0; i < max; i++)
            {
                string? a = i < linesA.Count ? linesA[i] : null;
                string? b = i < linesB.Count ? linesB[i] : null;

                if (a != null && b != null)
                {
                    if (string.Equals(a, b, StringComparison.Ordinal))
                    {
                        result.Add(new TextDiffItem
                        {
                            LineNumberA = i + 1,
                            LineNumberB = i + 1,
                            Content = a,
                            Kind = TextDiffKind.Unchanged
                        });
                    }
                    else
                    {
                        result.Add(new TextDiffItem
                        {
                            LineNumberA = i + 1,
                            LineNumberB = null,
                            Content = a,
                            Kind = TextDiffKind.UniqueToA
                        });
                        result.Add(new TextDiffItem
                        {
                            LineNumberA = null,
                            LineNumberB = i + 1,
                            Content = b,
                            Kind = TextDiffKind.UniqueToB
                        });
                    }
                }
                else if (a != null)
                {
                    result.Add(new TextDiffItem
                    {
                        LineNumberA = i + 1,
                        LineNumberB = null,
                        Content = a,
                        Kind = TextDiffKind.UniqueToA
                    });
                }
                else if (b != null)
                {
                    result.Add(new TextDiffItem
                    {
                        LineNumberA = null,
                        LineNumberB = i + 1,
                        Content = b,
                        Kind = TextDiffKind.UniqueToB
                    });
                }
            }

            return result;
        }
    }
}
