using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using ProjectsSearchRAG.Core.Clients.IClients;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectsSearchRAG.Core.Clients
{
    public class GeminiChatClient : IGeminiChatClient
    {
        private static readonly string SystemInstruction =
            "You are an expert in source code analysis. " +
            "You answer questions about the code of a specific project " +
            "by relying EXCLUSIVELY on the context (code fragments) provided. " +
            "Rules:\n" +
            "1. Provide a concise answer: the main implementation guidelines, NOT the complete code.\n" +
            "2. Mention the files, classes and methods involved, as they appear in the context.\n" +
            "3. Never invent code, files or behavior that is not in the context.\n" +
            "4. If the context is not sufficient to answer, state it explicitly instead of guessing.";

        private static readonly int[] RetryableStatusCodes = { 408, 429, 500, 502, 503, 504 };

        private string _selectedChatModel;

        private readonly IOptionsMonitor<GeminiOptions> _options;
        private Client? _client;
        public GeminiChatClient(IOptionsMonitor<GeminiOptions> options)
        {
            _options = options;
            _selectedChatModel = _options.CurrentValue.ChatModel;
        }

        private Client Client => _client ??= BuildClient();

        private Client BuildClient()
        {
            var retryOptions = new HttpRetryOptions
            {
                Attempts = 5,
                InitialDelay = 1.0,
                MaxDelay = 30.0,
                ExpBase = 2.0,
                Jitter = 0.5,
                HttpStatusCodes = new List<int>(RetryableStatusCodes)
            };

            var httpOptions = new HttpOptions
            {
                RetryOptions = retryOptions
            };

            return new Client(apiKey: _options.CurrentValue.ApiKey, httpOptions: httpOptions);
        }

        public Task<string> GenerateAnswerAsync(
            string userQuestion,
            IReadOnlyList<CodeChunk> contextChunks,
            string selectedChatModel,
            CancellationToken cancellationToken = default)
        {
            _selectedChatModel = selectedChatModel;
            return GenerateAnswerWithRetryAsync(userQuestion, contextChunks, cancellationToken);
        }

        private async Task<string> GenerateAnswerWithRetryAsync(
            string userQuestion,
            IReadOnlyList<CodeChunk> contextChunks,
            CancellationToken cancellationToken = default)
        {
            var userPrompt = BuildUserPrompt(userQuestion, contextChunks);

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = new List<Part> { new Part { Text = SystemInstruction } }
                },
                Temperature = 0.2,
                MaxOutputTokens = 1024
            };

            const int maxAttempts = 5;
            const int baseDelayMs = 1000;
            const int maxDelayMs = 32000;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await CallGeminiAsync(userPrompt, config, cancellationToken);
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    var delayMs = ComputeExponentialBackoffDelay(attempt, baseDelayMs, maxDelayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (Exception ex) when (!IsTransient(ex))
                {
                    throw;
                }
            }

            throw new InvalidOperationException(
                $"Gemini request failed after {maxAttempts} attempts due to a transient error (HTTP 503 or similar).");
        }

        private async Task<string> CallGeminiAsync(
            string userPrompt,
            GenerateContentConfig config,
            CancellationToken cancellationToken)
        {
            var response = await Client.Models.GenerateContentAsync(
                model: _selectedChatModel,
                contents: userPrompt,
                config: config,
                cancellationToken: cancellationToken);

            var answer = response.Candidates?.FirstOrDefault()?
                    .Content?.Parts?.FirstOrDefault()?.Text;

            return string.IsNullOrWhiteSpace(answer)
                ? throw new InvalidOperationException("Gemini didn't return a valid answer")
                : answer;
        }

        private static int ComputeExponentialBackoffDelay(int attempt, int baseDelayMs, int maxDelayMs)
        {
            var exp = Math.Pow(2, attempt - 1);
            var delay = (int)Math.Min(baseDelayMs * exp, maxDelayMs);
            var jitter = System.Random.Shared.Next(0, delay / 2 + 1);
            return Math.Min(delay + jitter, maxDelayMs);
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is null) return false;

            var message = ex.Message ?? string.Empty;

            foreach (var code in RetryableStatusCodes)
            {
                if (message.Contains(code.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var keyword in TransientKeywords)
            {
                if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return ex.InnerException != null && IsTransient(ex.InnerException);
        }

        private static readonly string[] TransientKeywords =
        {
            "too many requests",
            "rate limit",
            "resource has been exhausted",
            "high demand",
            "temporarily unavailable",
            "overloaded",
            "server error",
            "please retry",
            "internal server error",
            "service unavailable"
        };

        private static string BuildUserPrompt(string userQuestion, IReadOnlyList<CodeChunk> contextChunks)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Context (code fragments of the indexed project):");
            sb.AppendLine();

            if (contextChunks.Count == 0)
            {
                sb.AppendLine("(No relevant fragments found.)");
            }
            else
            {
                for (var i = 0; i < contextChunks.Count; i++)
                {
                    var chunk = contextChunks[i];
                    sb.AppendLine($"--- Fragment {i + 1} ---");
                    sb.AppendLine($"File: {chunk.FilePath}");
                    if (!string.IsNullOrWhiteSpace(chunk.ClassName))
                        sb.AppendLine($"Class: {chunk.ClassName}");
                    if (!string.IsNullOrWhiteSpace(chunk.MethodName))
                        sb.AppendLine($"Method: {chunk.MethodName}");
                    if (chunk.StartLine.HasValue && chunk.EndLine.HasValue)
                        sb.AppendLine($"Lines: {chunk.StartLine}-{chunk.EndLine}");
                    sb.AppendLine("Code:");
                    sb.AppendLine(chunk.Content);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("USER QUESTION:");
            sb.AppendLine(userQuestion);

            return sb.ToString();
        }
    }
}