SimpleSpaceMongerCS - minimal C# rewrite of the core scanning logic from SpaceMonger

What it does:
- Recursively scans a directory tree and computes sizes per-directory.
- Prints total size and the top N largest directories (default top 20).

Build and run (Windows PowerShell):

1. Build:
   dotnet build

2. Run:
   dotnet run -- "C:\path\to\scan" -- -n 10

Usage:
   dotnet run -- <path> [-n|--max <N>]

Notes:
- This is a simple console app intended as a small rewrite of the main scanning logic only.
- It skips directories and files that are inaccessible due to permissions.
