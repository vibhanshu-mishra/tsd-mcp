# TSD MCP Server

> Connect Claude AI to your live Tekla Structural Designer models using the Model Context Protocol (MCP).

Built by a structural engineer, for structural engineers. This MCP server lets you talk to your open TSD models in plain English — query members, review design status, and surface validation errors without clicking through the UI.

---

## Demo

> *"List all members in my TSD model"*
> *"How many beams, columns, and braces are in this model?"*
> *"Are there any validation errors in the open model?"*

Once connected, Claude talks directly to whatever model you have open in TSD.

---

## What Is This?

MCP (Model Context Protocol) is an open standard that lets AI assistants like Claude connect to external tools and data sources. This server acts as a bridge between Claude Desktop and Tekla Structural Designer 2025. It connects to TSD's Remoting API via a C# bridge process, giving Claude live read access to your open structural model.

Unlike the RISA-3D MCP server which reads static `.tsm` files, this server connects to a running TSD instance — so it always reflects the current state of your model.

---

## What You Can Do

Once connected, you can ask Claude things like:

- *"List all members in my TSD model"*
- *"How many beams, columns, and braces are in this model?"*
- *"Give me a design summary for the open model"*
- *"Are there any validation errors I should know about?"*
- *"How many members are in the model total?"*

---

## Requirements

- Windows PC with **Tekla Structural Designer 2025** installed and licensed
- TSD must be **running** with a model **open** when using the tools
- [Claude Desktop](https://claude.ai/download) (free)
- [Node.js](https://nodejs.org) (LTS version — free)
- **Visual Studio 2022 or later** (Community edition is free) with .NET 8.0 support
- A Claude account (Pro plan recommended)

---

## Architecture

This server has two components:
Claude Desktop

↓ MCP

Node.js MCP Server (server/index.js)

↓ subprocess

C# Bridge (bridge/Program.cs)

↓ Remoting API

Tekla Structural Designer 2025

The C# bridge connects to the running TSD instance using the official `TeklaStructuralDesigner.RemotingAPI` NuGet package. The Node.js MCP server calls the bridge as a subprocess and returns results to Claude.

---

## Installation

### Step 1 — Build the C# Bridge

1. Open **Visual Studio 2022 or later**
2. Create a new project: **Console App** (C#, .NET — not .NET Framework)
3. Name it `TsdBridge`, location `C:\tsd-mcp\bridge`
4. Set Framework to **.NET 8.0**
5. Go to **Tools → NuGet Package Manager → Manage NuGet Packages for Solution**
6. Search for `TeklaStructuralDesigner.RemotingAPI` and install version **25.3.0**
7. Go to **Build → Configuration Manager**, set platform to **x64**
8. Replace all contents of `Program.cs` with the contents of `bridge/Program.cs` from this repository
9. Press **Ctrl+Shift+B** to build

The compiled bridge will be at:
C:\tsd-mcp\bridge\TsdBridge\TsdBridge\bin\x64\Debug\net8.0\TsdBridge.exe

---

### Step 2 — Set Up the Node.js MCP Server

Open Command Prompt and run these one at a time:

```bash
mkdir C:\tsd-mcp\server
cd C:\tsd-mcp\server
npm init -y
npm install @modelcontextprotocol/sdk
```

Then open `C:\tsd-mcp\server\package.json` in Notepad and add `"type": "module"`:

```json
{
  "name": "tsd-mcp-server",
  "version": "1.0.0",
  "type": "module",
  "main": "index.js"
}
```

Copy `server/index.js` from this repository to `C:\tsd-mcp\server\index.js`.

---

### Step 3 — Test the Bridge

Open TSD with any model. Then run in Command Prompt:
"C:\tsd-mcp\bridge\TsdBridge\TsdBridge\bin\x64\Debug\net8.0\TsdBridge.exe" list_members

You should see a JSON array of all members in the model. If you see `"TSD is not running"` — make sure TSD is open with a model loaded.

---

### Step 4 — Connect to Claude Desktop

1. Install [Claude Desktop](https://claude.ai/download) if you haven't already
2. Press **Windows + R**, type `%APPDATA%\Claude` and press Enter
3. Open `claude_desktop_config.json`
4. Add the `tsd-mcp` entry inside `mcpServers`:

```json
{
  "mcpServers": {
    "risa3d": {
      "command": "node",
      "args": ["C:\\risa-mcp\\index.js"]
    },
    "tsd-mcp": {
      "command": "node",
      "args": ["C:\\tsd-mcp\\server\\index.js"]
    }
  }
}
```

5. Save the file
6. Fully quit Claude Desktop (right-click system tray icon → Quit)
7. Reopen Claude Desktop

---

### Step 5 — Verify It's Running

In Claude Desktop:
1. Click the **+** button in the chat input
2. Click **Connectors**
3. You should see **tsd-mcp** listed as connected

Or go to **Settings → Developer** and confirm tsd-mcp shows as **running**.

---

## Example Prompts

Once connected, open TSD with a model and try:
List all members in my TSD model

Give me a design summary for the open TSD model

How many beams, columns, and braces are in this model?

Are there any validation errors in the open model?

---

## Available Tools

| Tool | Description |
|---|---|
| `list_tsd_members` | Lists all members in the open TSD model with their names and inferred types (Beam, Column, Brace, Column Base Plate) |
| `get_tsd_design_summary` | Returns total member count broken down by type |
| `get_tsd_validation_errors` | Returns validation errors and warnings from the open TSD model |

---

## Important Notes

**TSD must be running.** The bridge connects to a live TSD process. If TSD is closed or no model is open, all tools will return an error.

**One model at a time.** The bridge connects to the first running TSD instance found. If you have multiple TSD windows open, results may vary.

**x64 only.** The C# bridge must be compiled targeting x64 to match the TSD Remoting API architecture. Building as `Any CPU` will fail at runtime.

**Analysis not required for member listing.** The `list_tsd_members` and `get_tsd_design_summary` tools work on any open model regardless of whether analysis has been run. Utilization ratios and design results (coming in a future update) require analysis to be completed first.

---

## Technical Notes on the TSD Remoting API

A few non-obvious things about how this server connects to TSD, documented for anyone extending it:

**1. Connection method.** The bridge uses `ApplicationFactory.GetRunningApplicationsAsync()` to discover running TSD instances — not a named pipe or port. TSD must already be running; the bridge does not launch it.

**2. Document vs. Model.** The API separates `IApplication` → `IDocument` → `IModel`. You must call `GetDocumentAsync()` then `GetModelAsync()` to reach the structural model. Skipping either step returns null.

**3. Member type inference.** The `IMember` interface exposes `EntityType` which returns a generic `Member` string for all member types. Actual type (Beam/Column/Brace) is inferred from the member name prefix: `B` = Beam, `C` = Column, `BR` = Brace, `CBase` = Column Base Plate. This matches TSD's own auto-naming convention.

**4. Section names.** `IElementSection` is a property wrapper — the actual section name string is not directly exposed as a named property. Section size extraction is under active development.

**5. Architecture requirement.** The `TeklaStructuralDesigner.RemotingAPI` NuGet package targets AMD64. The C# project must be built as x64 or it will throw an architecture mismatch error at runtime even if it compiles successfully.

---

## Roadmap

- [x] Connect to live TSD 2025 instance via Remoting API
- [x] List all members with inferred type classification
- [x] Member count summary by type
- [x] Validation error extraction
- [ ] Section size per member
- [ ] Utilization ratios (requires analysis results)
- [ ] Failing members filter (UC > 1.0)
- [ ] Solver error extraction
- [ ] Load combination listing
- [ ] Export member schedule to Excel

---

## Contributing

Pull requests are welcome. If you use TSD and have ideas for new tools, open an issue and let's discuss.

---

## About

Built by **Vibhanshu Mishra, PE** — Structural Engineer at AG&E Structural Engineers, Austin TX.

Specializing in steel and mission-critical structures. Building AI tools for a niche where none existed.

---

## License

MIT License — free to use, modify, and share.
