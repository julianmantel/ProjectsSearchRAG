# ProjectsSearchRAG

Aplicación de escritorio **WPF (.NET 10)** que permite buscar y consultar código fuente de proyectos con **RAG** (Retrieval-Augmented Generation). Escanea proyectos, genera embeddings con **Gemini** y los almacena en **PostgreSQL** con la extensión **pgvector** para búsqueda semántica.

## Características

- Escaneo e indexación de proyectos (C#, TypeScript/TSX, JavaScript, SQL y JSON).
- Vectorización de chunks de código con `gemini-embedding-001` (dimensión 768).
- Búsqueda semántica por similitud coseno vía `pgvector`.
- Chat con contexto recuperado usando `gemini-2.5-flash`.
- Migraciones de base de datos aplicadas automáticamente al iniciar la app.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (o superior).
- [PostgreSQL](https://www.postgresql.org/download/) con la extensión [pgvector](https://github.com/pgvector/pgvector) instalada.
- Una clave de API de [Google Gemini](https://aistudio.google.com/apikey).

## Configuración

1. Crea tu archivo `.env` copiando la plantilla:

   ```bash
   copy src\ProjectsSearchRAG.App\.env.example src\ProjectsSearchRAG.App\.env
   ```

2. Completa `.env` con tus credenciales:

   ```
   ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=projects_search_rag;Username=postgres;Password=mi_password
   Gemini__ApiKey=TU_CLAVE_GEMINI
   Gemini__EmbeddingModel=gemini-embedding-001
   Gemini__EmbeddingDimension=768
   Gemini__ChatModel=gemini-2.5-flash
   ```

   > El archivo `.env` está ignorado por git y **nunca se sube** al repositorio.

   También puedes configurar las mismas variables como variables de entorno de tu sistema.

   > **Nota**: si `ConnectionStrings__DefaultConnection` queda vacío, la app usa como respaldo `Host=localhost;Port=5432;Database=projects_search_rag;Username=postgres;Password=postgres`. Define tu propia conexión para usar tu servidor real.

3. Aplica la migración inicial (opcional: la app la aplica sola al arrancar):

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/ProjectsSearchRAG.Data\ProjectsSearchRAG.Data.csproj
   ```

## Compilar y ejecutar

```bash
dotnet restore
dotnet run --project src\ProjectsSearchRAG.App
```

## Ejecutar pruebas

```bash
dotnet test
```

## Estructura del proyecto

```
src/
  ProjectsSearchRAG.App            # Aplicación WPF (Vista, ViewModels, Resources)
  ProjectsSearchRAG.Core           # Lógica de dominio: escaneo, chunking, embeddings, RAG
  ProjectsSearchRAG.Data           # EF Core, contexto, modelos y repositorios (pgvector)
  ProjectsSearchRAG.Infrastructure # Configuración de inyección de dependencias
tests/
  ProjectsSearchRAG.Tests          # Pruebas unitarias (xUnit)
```

## Tecnologías

- .NET 10 / WPF (MVVM con CommunityToolkit.Mvvm)
- Google.GenAI (Gemini: embeddings + chat)
- EF Core + Npgsql + pgvector
- xUnit