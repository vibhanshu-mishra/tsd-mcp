# Tekla Structural Designer MCP Server

> An AI-powered engineering interface for Tekla Structural Designer. Query, analyse, review, and estimate structural models using natural language through the Model Context Protocol (MCP).

Built by a structural engineer, for structural engineers. This MCP server lets you talk to your open TSD models in plain English — query members, review design status, inspect load combinations, retrieve member forces, generate governing force envelopes, run steel takeoffs, estimate material costs, and interact with your live structural model without leaving Claude.

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![TSD](https://img.shields.io/badge/Tekla%20Structural%20Designer-2025-green)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Node.js](https://img.shields.io/badge/Node.js-20+-brightgreen)
![License](https://img.shields.io/badge/License-MIT-yellow)
![MCP](https://img.shields.io/badge/MCP-Compatible-red)

---

## Demo

- "List all members in my TSD model"
- "Generate a steel takeoff"
- "Which members are failing?"
- "Show me the governing forces for B4869"
- "Which load combination governs this beam?"
- "Why is this member failing?"
- "Estimate steel cost at $4200 per ton"
- "How many tons of W33x130 are in this model?"
- "Review my model"
- "Show me the design dashboard"
- "Show me support reactions for load combination 18"
- "What support has the largest vertical reaction?"
- "Give me a design summary for B4869"

Once connected, Claude queries your live TSD model and returns results instantly.

---

## What Is This?

MCP (Model Context Protocol) is an open standard that lets AI assistants connect to external tools and data sources. This server bridges Claude Desktop and Tekla Structural Designer 2025 via the official Remoting API — giving Claude live read access to your open structural model.

No file exports. No copy-paste. Claude talks directly to whatever model you have open in TSD.

Instead of navigating dozens of dialogs and reports, engineers can ask questions in plain English and receive engineering-focused answers in seconds. The vision is not to replace engineering judgment; it is to remove the friction of accessing engineering information.

---

## Design Philosophy

Each MCP tool is designed around an engineering workflow rather than the underlying Tekla API.

Instead of exposing hundreds of low-level properties, tools answer practical engineering questions such as:

- Which members are critical?
- Why is this member failing?
- Which load combination governs?
- What should I review first?
- Is the overall model healthy?

Whenever possible, the bridge summarizes, classifies, prioritizes, and explains engineering information so AI assistants can reason about structural models more naturally.

---

## What You Can Do

**Model exploration**
- List all members with type classification
- Filter members by type or section size
- Inspect individual member details, including section, material, and design checks
- Get a full model overview

**Design review**
- Extract utilization ratios across the model
- Identify failing members (UC ≥ 1.0)
- Flag members near the limit (0.90 ≤ UC < 1.0)
- Pull the top utilized members sorted by criticality
- Get validation errors and design status summaries
- Retrieve member end forces for any load combination
- Generate governing force envelopes across active strength load combinations
- Identify the governing load combination for each force component
- Get engineering significance for governing axial force, shear, moment, torsion, and deflection
- Generate a project-level design dashboard with model health, risk distribution, section statistics, check-type statistics, observations, priorities, and recommended next action
- Explain why a specific member is failing
- Get member-specific design summaries
- Review support/foundation reactions for any load combination

**Steel takeoff and estimating**
- Generate a full steel takeoff with length, weight per foot, total weight, and total tonnage
- Break down tonnage by member type, section type, or individual section
- Identify the heaviest contributing sections
- Estimate material cost at any cost-per-ton rate

---

## Engineering Intelligence

Unlike a traditional API wrapper, this MCP server does not simply expose raw Tekla Structural Designer objects.

Many tools perform engineering interpretation before returning results, allowing AI assistants to answer engineering questions rather than simply retrieving data.

Current engineering intelligence includes:

- Automatic member classification (Beam, Column, Brace, Column Base Plate)
- Governing force envelope extraction across active strength load combinations
- Governing load combination identification for axial force, shear, moment, torsion, and deflection
- Engineering significance for governing force components
- Design failure interpretation based on governing utilization and design checks
- Project-level design health assessment
- Risk distribution based on utilization ranges
- Automatic identification of critical members, near-limit members, and utilization trends
- Section statistics including the most common, highest average utilization, and highest maximum utilization
- Engineering observations and recommended next actions generated from model-wide statistics

The objective is to expose engineering insight rather than raw software data.

---

## Why This Project Exists

Structural engineers spend significant time navigating through menus, reports, and spreadsheets just to answer straightforward questions about a model.

This project brings AI directly into the structural engineering workflow by exposing the Tekla Structural Designer Remoting API through the Model Context Protocol (MCP).

Instead of manually searching through dialogues, engineers can simply ask:

- Which members are failing?
- Show me all W33x130 beams.
- Generate a steel takeoff.
- Estimate the structural steel cost.
- Which sections contribute the most tonnage?

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

```text
┌────────────────────────────┐
│      Claude Desktop        │
└─────────────┬──────────────┘
              │ MCP
              ▼
┌────────────────────────────┐
│     Node.js MCP Server     │
└─────────────┬──────────────┘
              │ subprocess
              ▼
┌────────────────────────────┐
│     C# Bridge (.NET 8)     │
└─────────────┬──────────────┘
              │ Remoting API
              ▼
┌────────────────────────────┐
│ Tekla Structural Designer  │
│      2025 (Live Model)     │
└────────────────────────────┘
```

The Node.js MCP server receives tool calls from Claude Desktop and passes commands to the C# bridge as a subprocess. The bridge connects to the running TSD instance via the official Remoting API and returns JSON results. The bridge also performs engineering interpretation, aggregating multiple Remoting API calls into higher-level engineering insights before returning structured JSON to Claude.

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

**Structural Analysis**
```
Show me the end forces for member B4869 under load combination 18
```

```
Show me the end forces for B4869 under LRFD
```

```
Generate the force envelope for member B4869
```

```
What is the governing major shear on B4869?
```

```
What load combination governs B4869?
```

```
Show me the governing torsion on B4869
```

```
Generate the force envelope using all load combinations
```

**Estimating**
```
Estimate steel cost at $4200 per ton
```

```
What's the total tonnage?
```

**Design Intelligence**
```
Review my model
```

```
Generate a design dashboard
```

```
Why is member B4869 failing?
```

```
Give me a design summary for B4869
```

```
Show support reactions for load combination 18
```

```
Which support has the largest vertical reaction?
```

---

## Sample Output

```json
{
  "member":"B4869",

  "shear_major":{
      "governing":{
          "value":739859.7,
          "combination":"28 LRFD...",
          "end":"End",
          "engineering_significance":"Governing major-axis shear. Review member shear capacity and connection shear demand."
      }
  }
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
| `get_tsd_load_combinations` | Lists all load combinations including strength/service classification, activity status, metadata, reference index, name, class, factoring type, and active/design status |
| `get_tsd_member_forces` | Returns end forces for a member under a specified load combination. Supports lookup by reference index, full combination name, or partial name |
| `get_tsd_member_force_envelope` | Returns the governing force envelope for a member across load combinations, including maximum positive, maximum negative, governing value, governing load combination, position, and engineering significance for axial force, shear, moment, torsion, and deflection. |
| `get_tsd_analysis_status` | Returns the selected analysis type and confirms whether TSD is running with a model open |
| `get_tsd_solver_warnings` | Returns solver/analysis warnings and informational items from the current TSD model |
| `get_tsd_support_reactions` | Returns support/foundation reactions for a specified load combination, including optional support filtering, reaction summaries, engineering significance, and global coordinate system reactions |
| `get_tsd_governing_load_combo` | Returns the governing load combination for each force component of a member, plus a governing summary and overall governing combination |
| `get_tsd_member_design_summary` | Returns section, material, design status, governing utilization, engineering summary, and span-level design checks for a specific member |
| `get_tsd_why_is_member_failing` | Explains why a member is failing based on TSD design check results, utilization, engineering interpretation, and recommended next steps |
| `get_tsd_design_dashboard` | Returns a project-level design dashboard with model health, utilization distribution, risk distribution, key metrics, section statistics, check-type statistics, observations, priorities, and recommended next action |

---

## Current Limitations

- Read-only access
- Tekla Structural Designer 2025 only
- Requires an active TSD session
- Currently supports one open model at a time

---

## Engineering Principles

This project follows several core principles:

- Engineering-first rather than API-first
- Return summarized engineering insights whenever possible
- Preserve traceability back to Tekla Structural Designer
- Avoid hiding raw engineering data
- Keep every result explainable and deterministic
- Build reusable engineering tools rather than one-off queries

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
- [x] Load combination extraction

### Phase 1 — Model Intelligence

- [x] Load combinations
- [x] Solver warnings
- [x] Analysis status

### Phase 2 — Structural Analysis

- [x] Member forces
- [x] Member force envelopes
- [x] Foundation/support reactions
- [x] Governing load combinations

### Phase 3 — Design Intelligence

- [x] Explain why a member fails
- [x] Member design summaries
- [x] Project-level design dashboard
- [ ] Optimization recommendations

### Phase 4 — Engineering Reports

- [ ] Excel export
- [ ] CSV export
- [ ] PDF reports

### Phase 5 — AI Engineering Assistant

- [ ] Automated QA/QC
- [ ] Design review assistant
- [ ] Fabrication package generation

---

## About

Built by **Vibhanshu Mishra, PE** — Structural Engineer at AG&E Structural Engineers, Austin TX.

Specialising in steel and mission-critical structures. Building AI-native tools for structural engineers.
