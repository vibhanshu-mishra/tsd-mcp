# TSD MCP Server

MCP server for Tekla Structural Designer (TSD) 2025, connecting Claude Desktop to live TSD models.

## Architecture
- **Bridge**: C# console app (`bridge/`) → talks to TSD via Remoting API
- **Server**: Node.js MCP server (`server/`) → called by Claude Desktop

## Tools
- `list_tsd_members` — all members with name and type (Beam/Column/Brace/Column Base Plate)
- `get_tsd_design_summary` — total member count by type
- `get_tsd_validation_errors` — validation errors from open TSD model

## Requirements
- Tekla Structural Designer 2025 (must be running with a model open)
- .NET 8.0
- Node.js
- Claude Desktop

## Files
- `bridge/Program.cs` — C# bridge built with Visual Studio 2026, targets x64
- `server/index.js` — Node.js MCP server
- `server/package.json` — Node.js dependencies

## Status
- Section size extraction: in progress
- Utilization ratios: planned
- Failing members filter: planned
