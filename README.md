# TSD MCP Server

> An AI-powered engineering interface for Tekla Structural Designer. Query, analyse, review, and estimate structural models using natural language through the Model Context Protocol (MCP).

Built by a structural engineer, for structural engineers. This MCP server lets you talk to your open TSD models in plain English — query members, review design status, run steel takeoffs, and estimate material costs without clicking through the TSD interface.

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![TSD](https://img.shields.io/badge/Tekla%20Structural%20Designer-2025-green)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Node.js](https://img.shields.io/badge/Node.js-20+-brightgreen)
![License](https://img.shields.io/badge/License-MIT-yellow)
![MCP](https://img.shields.io/badge/MCP-Compatible-red)

---

## Demo

> *"List all members in my TSD model"*
> *"Which members are failing design checks?"*
> *"Show me the top utilized members"*
> *"Generate a steel takeoff"*
> *"Estimate steel cost at $4200 per ton"*
> *"How many tons of HSS are in this model?"*
> *"Are there any validation errors?"*
> *"Tell me about member B4869"*

Once connected, Claude queries your live TSD model and returns results instantly.

---

## What Is This?

MCP (Model Context Protocol) is an open standard that lets AI assistants connect to external tools and data sources. This server bridges Claude Desktop and Tekla Structural Designer 2025 via the official Remoting API — giving Claude live read access to your open structural model.

No file exports. No copy-paste. Claude talks directly to whatever model you have open in TSD.

---

## What You Can Do

**Model exploration**
- List all members with type classification
- Filter members by type or section size
- Inspect individual member details including section, material, and design checks
- Get a full model overview

**Design review**
- Extract utilization ratios across the model
- Identify failing members (UC ≥ 1.0)
- Flag members near the limit (0.90 ≤ UC < 1.0)
- Pull the top utilized members sorted by criticality
- Get validation errors and design status summaries

**Steel takeoff and estimating**
- Generate a full steel takeoff with length, weight per foot, total weight, and total tonnage
- Break down tonnage by member type, section type, or individual section
- Identify the heaviest contributing sections
- Estimate material cost at any cost-per-ton rate

---

## Why This Project Exists

Structural engineers spend significant time navigating through menus, reports, and spreadsheets just to answer straightforward questions about a model.

This project brings AI directly into the structural engineering workflow by exposing the Tekla Structural Designer Remoting API through the Model Context Protocol (MCP).

Instead of manually searching through dialogues, engineers can simply ask:

• Which members are failing?

• Show me all W33x130 beams.

• Generate a steel takeoff.

• Estimate the structural steel cost.

• Which sections contribute the most tonnage?

The goal is to make engineering data conversational.

---

## Requirements

- Windows PC with **Tekla Structural Designer 2025** installed and licensed
- TSD must be **running** with a model **open** when tools are called
- [Claude Desktop](https://claude.ai/download)
- [Node.js](https://nodejs.org) (LTS version)
- **Visual Studio 2022 or later** (Community edition is free)
- .NET 8.0
- A Claude account (Pro plan recommended)

---

## Architecture
Claude Desktop

↓ MCP

Node.js MCP Server  (server/index.js)

↓ subprocess

C# Bridge           (bridge/Program.cs)

↓ Remoting API

Tekla Structural Designer 2025

↓

Open TSD Model

The Node.js MCP server receives tool calls from Claude Desktop and passes commands to the C# bridge as a subprocess. The bridge connects to the running TSD instance via the official Remoting API and returns JSON results.

---

## Installation

### Step 1 — Build the C# Bridge

1. Open **Visual Studio**
2. Create a new **Console App** (C#, .NET — not .NET Framework), name it `TsdBridge`, location `C:\tsd-mcp\bridge`
3. Set Framework to **.NET 8.0**
4. Go to **Tools → NuGet Package Manager → Manage NuGet Packages for Solution**, search for `TeklaStructuralDesigner.RemotingAPI` and install version **25.3.0**
5. Go to **Build → Configuration Manager** and set the platform to **x64**
6. Replace all contents of `Program.cs` with the contents of `bridge/Program.cs` from this repository
7. Press **Ctrl+Shift+B** to build

The compiled bridge will be at:
C:\tsd-mcp\bridge\ConsoleApp1\ConsoleApp1\bin\x64\Debug\net8.0\ConsoleApp1.exe

---

### Step 2 — Set Up the Node.js MCP Server

Open Command Prompt and run these one at a time:

```bash
mkdir C:\tsd-mcp\server
cd C:\tsd-mcp\server
npm init -y
npm install @modelcontextprotocol/sdk
```

Open `C:\tsd-mcp\server\package.json` in Notepad and add `"type": "module"`:

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

Open TSD with any model, then run in Command Prompt:
"C:\tsd-mcp\bridge\ConsoleApp1\ConsoleApp1\bin\x64\Debug\net8.0\ConsoleApp1.exe" list_members

You should see a JSON array of all members in the model. If you see `"error": "TSD is not running"` — make sure TSD is open with a model loaded.

---

### Step 4 — Connect to Claude Desktop

Open `%APPDATA%\Claude\claude_desktop_config.json` and add the `tsd-mcp` entry inside `mcpServers`:

```json
{
  "mcpServers": {
    "tsd-mcp": {
      "command": "node",
      "args": ["C:\\tsd-mcp\\server\\index.js"]
    }
  }
}
```

Fully quit Claude Desktop and reopen it.

---

### Step 5 — Verify It's Running

Go to **Settings → Developer** and confirm `tsd-mcp` shows as **running**.

---

## Example Prompts

**Model exploration**
```
List all members in my TSD model
```

```
Show me all braces
```
```
Show me all W 33x130 members
```
```
Tell me about member B4869
```

**Design review**
```
Which members are failing?
```

```
Show me members near failure
```

```
Show me the most critical members
```

```
Give me a design status summary
```

```
Are there any validation errors?
```

**Steel takeoff**
```
Generate a steel takeoff
```

```
How many tons of steel are in this model?
```

```
How many tons are W-shapes?
```

```
Show me the heaviest sections
```

**Estimating**
```
Estimate steel cost at $4200 per ton
```

```
What's the total tonnage?
```

---

## Sample Output

```json
{
  "member": "B4869",
  "section": "W 33x130",
  "material": "Steel",
  "utilization_ratio": 1.093,
  "status": "Fail"
}
```

---

## Available Tools

| Tool | Description |
|---|---|
| `list_tsd_members` | Lists all members with inferred type (Beam, Column, Brace, Column Base Plate) |
| `list_tsd_members_with_sections` | Lists all members with section size and type |
| `get_tsd_member_details` | Returns section, material, and design check details for a specific member |
| `get_tsd_members_by_type` | Filters members by type |
| `get_tsd_members_by_section` | Returns all members using a given section size |
| `get_tsd_design_summary` | Member counts grouped by type |
| `get_tsd_model_overview` | Full model inventory including sections and materials |
| `get_tsd_validation_errors` | Validation warnings and errors from the open model |
| `get_tsd_design_status_summary` | Pass/Fail/Warning counts across the model |
| `get_tsd_failing_members` | All members with UC ≥ 1.0 |
| `get_tsd_members_near_limit` | Members with 0.90 ≤ UC < 1.0 |
| `get_tsd_top_utilized_members` | Highest utilization members sorted descending |
| `get_tsd_steel_takeoff` | Full steel takeoff with length, weight per foot, total weight, and tonnage grouped by section, section type, and member type |
| `get_tsd_takeoff_by_member_type` | Tonnage broken down by Beam, Column, Brace, Column Base Plate |
| `get_tsd_takeoff_by_section_type` | Tonnage broken down by W shapes, HSS, angles, and other section types |
| `get_tsd_heaviest_sections` | Sections contributing the most tonnage to the model |
| `get_tsd_model_cost_estimate` | Estimated material cost given a cost-per-ton input |
| `get_tsd_official_material_quantities` | Returns official material quantities directly from Tekla Structural Designer, including total mass, volume, surface area, connectors, reinforcement, and embodied carbon |
| `get_tsd_load_combinations` | Lists all load combinations including strength/service classification, activity status, and metadata |


---

## Current Limitations

- Read-only access
- Tekla Structural Designer 2025 only
- Windows only
- Requires an active TSD session
- Currently supports one open model at a time

---

## Technical Notes

### Connection Method

The bridge discovers running TSD instances using:

```csharp
ApplicationFactory.GetRunningApplicationsAsync()
```

TSD must already be running. The bridge does not launch TSD.

---

### Object Hierarchy

The Remoting API follows:
Application → Document → Model → Member → Span → ElementSection → PhysicalSection

Section sizes are extracted from `PhysicalSection.LongName`:
W 14x193

HSS 10x10x1/2

W 24x76

Steel takeoff calculations are derived directly from TSD section mass and member length properties — no external AISC database required.

---

### Member Type Classification

The Remoting API exposes all members as a generic `Member` type. Types are inferred from TSD's own naming convention:

| Prefix | Type |
|---|---|
| B | Beam |
| C | Column |
| BR | Brace |
| CBase | Column Base Plate |

---

### x64 Requirement

The TSD Remoting API targets AMD64. The bridge must be compiled for x64. Building as `Any CPU` may compile successfully but will fail at runtime.

---

## Roadmap

**Completed**
- [x] Live TSD connection via Remoting API
- [x] Member listing and type classification
- [x] Section and material extraction
- [x] Member filtering by type and section
- [x] Individual member detail inspection
- [x] Model overview
- [x] Validation error extraction
- [x] Design status summary
- [x] Utilization ratio extraction
- [x] Failing members (UC ≥ 1.0)
- [x] Near-limit members (0.90 ≤ UC < 1.0)
- [x] Top utilized members
- [x] Steel takeoff by section, section type, and member type
- [x] Heaviest sections analysis
- [x] Material cost estimation

### Phase 1 — Analysis Discovery

- [x] Load combinations
- [ ] Solver warnings
- [ ] Analysis status

### Phase 2 — Structural Forces

- [ ] Member forces
- [ ] Foundation reactions
- [ ] Governing load combinations

### Phase 3 — Design Intelligence

- [ ] Explain why a member fails
- [ ] Optimization recommendations
- [ ] Member design summaries

### Phase 4 — Reporting

- [ ] Excel export
- [ ] CSV export
- [ ] PDF reports

### Phase 5 — AI Workflows

- [ ] Automated QA/QC
- [ ] Design review assistant
- [ ] Fabrication package generation

---

## About

Built by **Vibhanshu Mishra, PE** — Structural Engineer at AG&E Structural Engineers, Austin TX.

Specializing in steel and mission-critical structures. Building AI tools for a niche where none existed.

---

## License

MIT License — free to use, modify, and share.
