using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services;
using ProjectsSearchRAG.Data.Models;
using Xunit;
using Xunit.Abstractions;

namespace ProjectsSearchRAG.Tests;

public class CodeChunkerServiceTests
{
    private readonly CodeChunkerService _chunker;
    private readonly ITestOutputHelper _output;

    public CodeChunkerServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var options = Microsoft.Extensions.Options.Options.Create(new IndexerOptions());
        _chunker = new CodeChunkerService(options);
    }

    [Fact]
    public void ChunkFile_CSharpFile_ExtractsClassesAndMethodsWithMetadata()
    {
        var csCode = @"using System;
using System.Threading.Tasks;

namespace MyApp.Services;

public class UserService
{
    private readonly IUserRepository _repo;

    public UserService(IUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        return await _repo.FindByIdAsync(id);
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _repo.FindByIdAsync(id);
        if (user == null) return false;
        await _repo.RemoveAsync(user);
        return true;
    }
}
";
        var chunks = _chunker.ChunkFile("src/Services/UserService.cs", csCode, Guid.NewGuid());

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c =>
        {
            Assert.Equal("csharp", c.Language);
            Assert.Equal("src/Services/UserService.cs", c.FilePath);
            Assert.True(c.StartLine > 0);
            Assert.True(c.EndLine >= c.StartLine);
        });

        Assert.Contains(chunks, c => c.ClassName == "UserService" && c.MethodName == "GetUserByIdAsync");
        Assert.Contains(chunks, c => c.ClassName == "UserService" && c.MethodName == "DeleteUserAsync");
    }

    [Fact]
    public void ChunkFile_TsxFile_ExtractsComponentsAndFunctions()
    {
        var tsxCode = @"import React, { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';

interface UserProfileProps {
    userId: string;
    onUpdate: () => void;
}

export function UserProfile({ userId, onUpdate }: UserProfileProps) {
    const [user, setUser] = useState(null);

    useEffect(() => {
        fetchUser(userId);
    }, [userId]);

    const handleSave = async () => {
        await saveUserData(user);
        onUpdate();
    };

    return (
        <div className=""profile-card"">
            <h1>User Profile</h1>
            <Button onClick={handleSave}>Save</Button>
        </div>
    );
}

export const useUserStats = (userId: string) => {
    const [stats, setStats] = useState(null);
    return stats;
};
";
        var chunks = _chunker.ChunkFile("src/components/UserProfile.tsx", tsxCode, Guid.NewGuid());

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("typescriptreact", c.Language));
        Assert.Contains(chunks, c => c.ClassName == "UserProfile" || c.MethodName == "UserProfile");
    }

    [Fact]
    public void ChunkFile_TsFile_ExtractsFunctionsWithTypeScriptLanguage()
    {
        var tsCode = @"import { useState } from 'react';

export const fetchUser = async (id: string) => {
    const res = await fetch(`/users/${id}`);
    return res.json();
};

export function formatDate(date: Date): string {
    return date.toISOString();
}
";
        var chunks = _chunker.ChunkFile("src/api/user.ts", tsCode, Guid.NewGuid());

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("typescript", c.Language));
        Assert.Contains(chunks, c => c.MethodName == "fetchUser");
        Assert.Contains(chunks, c => c.MethodName == "formatDate");
    }

    [Fact]
    public void ChunkFile_SqlFile_ExtractsTablesAndStatements()
    {
        var sqlCode = @"CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username TEXT NOT NULL UNIQUE,
    email TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE user_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_user_tokens_user_id ON user_tokens (user_id);
";
        var chunks = _chunker.ChunkFile("db/migrations/001_init.sql", sqlCode, Guid.NewGuid());

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("sql", c.Language));
        Assert.Contains(chunks, c => c.ClassName == "users");
        Assert.Contains(chunks, c => c.ClassName == "user_tokens");
    }

    [Fact]
    public void ChunkFile_JsonFile_ExtractsRootProperties()
    {
        var jsonCode = @"{
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Host=localhost;Database=mydb;Username=postgres""
  },
  ""Gemini"": {
    ""ApiKey"": ""secret"",
    ""EmbeddingModel"": ""gemini-embedding-001"",
    ""ChatModel"": ""gemini-2.5-flash""
  },
  ""Indexer"": {
    ""DefaultSourcePath"": ""/source"",
    ""SupportedExtensions"": [ "".cs"", "".tsx"", "".sql"", "".json"" ]
  }
}
";
        var chunks = _chunker.ChunkFile("appsettings.json", jsonCode, Guid.NewGuid());

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("json", c.Language));
    }

    [Fact]
    public void ChunkRealFiles_DemonstratesExtractionOutput()
    {
        var baseDir = AppContext.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\.."));

        // 1. Real .cs file (ProjectScannerService.cs)
        var csPath = Path.Combine(solutionRoot, @"src\ProjectsSearchRAG.Core\Services\ProjectScannerService.cs");
        if (File.Exists(csPath))
        {
            var csContent = File.ReadAllText(csPath);
            var csChunks = _chunker.ChunkFile("src/ProjectsSearchRAG.Core/Services/ProjectScannerService.cs", csContent);
            _output.WriteLine($"=== Real File 1: ProjectScannerService.cs ({csChunks.Count} chunks) ===");
            foreach (var chunk in csChunks)
            {
                _output.WriteLine($"  [CHUNK] Class: {chunk.ClassName ?? "(none)"}, Method: {chunk.MethodName ?? "(none)"}, Lines: {chunk.StartLine}-{chunk.EndLine}");
            }
            Assert.NotEmpty(csChunks);
        }

        // 2. Real .tsx file (AuthModal.tsx)
        var tsxPath = Path.Combine(solutionRoot, @"src\ProjectsSearchRAG.Data\Scripts\AuthModal.tsx");
        if (File.Exists(tsxPath))
        {
            var tsxContent = File.ReadAllText(tsxPath);
            var tsxChunks = _chunker.ChunkFile("src/ProjectsSearchRAG.Data/Scripts/AuthModal.tsx", tsxContent);
            _output.WriteLine($"=== Real File 2: AuthModal.tsx ({tsxChunks.Count} chunks) ===");
            foreach (var chunk in tsxChunks)
            {
                _output.WriteLine($"  [CHUNK] Component/Class: {chunk.ClassName ?? "(none)"}, Function: {chunk.MethodName ?? "(none)"}, Lines: {chunk.StartLine}-{chunk.EndLine}");
            }
            Assert.NotEmpty(tsxChunks);
        }

        // 3. Real .sql file (schema.sql)
        var sqlPath = Path.Combine(solutionRoot, @"src\ProjectsSearchRAG.Data\Scripts\schema.sql");
        if (File.Exists(sqlPath))
        {
            var sqlContent = File.ReadAllText(sqlPath);
            var sqlChunks = _chunker.ChunkFile("src/ProjectsSearchRAG.Data/Scripts/schema.sql", sqlContent);
            _output.WriteLine($"=== Real File 3: schema.sql ({sqlChunks.Count} chunks) ===");
            foreach (var chunk in sqlChunks)
            {
                _output.WriteLine($"  [CHUNK] Table/Entity: {chunk.ClassName ?? "(none)"}, Index/Stmt: {chunk.MethodName ?? "(none)"}, Lines: {chunk.StartLine}-{chunk.EndLine}");
            }
            Assert.NotEmpty(sqlChunks);
        }

        // 4. Real .json file (appsettings.json)
        var jsonPath = Path.Combine(solutionRoot, @"src\ProjectsSearchRAG.App\appsettings.json");
        if (File.Exists(jsonPath))
        {
            var jsonContent = File.ReadAllText(jsonPath);
            var jsonChunks = _chunker.ChunkFile("src/ProjectsSearchRAG.App/appsettings.json", jsonContent);
            _output.WriteLine($"=== Real File 4: appsettings.json ({jsonChunks.Count} chunks) ===");
            foreach (var chunk in jsonChunks)
            {
                _output.WriteLine($"  [CHUNK] Property: {chunk.ClassName ?? "(none)"}, Lines: {chunk.StartLine}-{chunk.EndLine}");
            }
            Assert.NotEmpty(jsonChunks);
        }
    }
}
