using System;
using System.Collections.Generic;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.IService;

public interface ICodeChunkerService
{
    IReadOnlyList<CodeChunk> ChunkFile(string relativeFilePath, string fileContent, Guid projectId = default);
    IReadOnlyList<CodeChunk> ChunkProject(string projectPath, Guid projectId = default, IReadOnlyList<string>? supportedExtensions = null);
}
