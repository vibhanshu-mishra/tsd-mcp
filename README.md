# TSD MCP Server

> Connect Claude, ChatGPT, and any MCP-compatible AI assistant to your live Tekla Structural Designer models using the Model Context Protocol (MCP).

Built by a structural engineer, for structural engineers.

This MCP server allows AI assistants to connect directly to an open Tekla Structural Designer model and answer questions about members, sections, utilization ratios, design failures, model composition, and validation status without manually navigating the TSD interface.

---

# Demo

Once connected, you can ask:

> "List all members in my TSD model"

> "Show me the section size for every member"

> "What are the most common sections in this model?"

> "Which members are failing design checks?"

> "Show me the top utilized members"

> "Which members are close to failing?"

> "Give me a complete overview of the model"

> "Are there any validation errors in the model?"

Claude or ChatGPT will query your live TSD model and return results instantly.

---

# What Is This?

MCP (Model Context Protocol) is an open standard that allows AI assistants to connect to external tools and data sources.

This project acts as a bridge between:

- Claude Desktop
- ChatGPT Desktop
- Any MCP-compatible AI client

and

- Tekla Structural Designer 2025

The MCP server communicates with a C# bridge which connects to Tekla Structural Designer through the official Remoting API.

---

# Current Features

## Member Discovery

Retrieve all members in the active model:

- Beams
- Columns
- Braces
- Column Base Plates

Tool:

```text
list_tsd_members
```

Example:

```json
[
  {
    "name": "B4982",
    "type": "Beam"
  },
  {
    "name": "C4418",
    "type": "Column"
  }
]
```

---

## Section Extraction

Retrieve:

- Section size
- Section type
- Material type

Tool:

```text
list_tsd_members_with_sections
```

Example:

```json
{
  "name": "B4982",
  "type": "Beam",
  "section": "W 18x35",
  "section_type": "W",
  "material_type": "Steel"
}
```

Supported section families include:

- W Shapes
- HSS Shapes
- Double Angles
- Column Base Plates
- Other physical sections exposed through the TSD API

---

## Design Summary

Returns member counts grouped by type.

Tool:

```text
get_tsd_design_summary
```

Example:

```json
{
  "total_members": 2269,
  "by_type": [
    {
      "type": "Beam",
      "count": 1902
    },
    {
      "type": "Brace",
      "count": 160
    },
    {
      "type": "Column Base Plate",
      "count": 125
    },
    {
      "type": "Column",
      "count": 82
    }
  ]
}
```

---

## Model Overview

Returns:

- Total members
- Member type counts
- Section usage summary
- Material summary

Tool:

```text
get_tsd_model_overview
```

Example:

```json
{
  "total_members": 2269,
  "by_type": [...],
  "by_section": [...],
  "by_material": [...]
}
```

---

## Validation Information

Retrieve model validation information.

Tool:

```text
get_tsd_validation_errors
```

---

## Utilization Review

Get the most highly utilized members in the model.

Tool:

```text
get_tsd_top_utilized_members
```

Example:

```json
[
  {
    "member": "B4869",
    "type": "Beam",
    "section": "W 33x130",
    "status": "Fail",
    "utilization_ratio": 1.093
  }
]
```

---

## Failing Members

Return all members with utilization ratios greater than or equal to 1.0.

Tool:

```text
get_tsd_failing_members
```

Example:

```json
[
  {
    "member": "B4869",
    "type": "Beam",
    "section": "W 33x130",
    "utilization_ratio": 1.093
  }
]
```

---

## Members Near Limit

Return members with utilization ratios between 0.90 and 1.00.

Tool:

```text
get_tsd_members_near_limit
```

Example:

```json
[
  {
    "member": "BR4.137",
    "type": "Brace",
    "section": "2L 6x6x3/8x3/8",
    "utilization_ratio": 0.999
  }
]
```

This is useful for identifying members that technically pass but may need review.

---

# Requirements

- Windows PC
- Tekla Structural Designer 2025 installed and licensed
- TSD must be running with a model open
- Node.js (LTS recommended)
- .NET 8
- Claude Desktop, ChatGPT Desktop, or another MCP-compatible client

---

# Architecture

```text
AI Client
    ↓
MCP Server (Node.js)
    ↓
C# Bridge (.NET 8)
    ↓
Tekla Structural Designer Remoting API
    ↓
Live TSD Model
```

The MCP server is written in Node.js.

The bridge is written in C# and communicates directly with Tekla Structural Designer through the official Remoting API.

---

# Installation

## Step 1 — Clone Repository

```bash
git clone https://github.com/vibhanshu-mishra/tsd-mcp.git
cd tsd-mcp
```

---

## Step 2 — Install Node Dependencies

```bash
npm install
```

---

## Step 3 — Build the Bridge

Open the bridge project in Visual Studio.

Requirements:

- Visual Studio 2022+
- .NET 8 SDK
- TeklaStructuralDesigner.RemotingAPI NuGet package

Build:

```bash
dotnet build -f net8.0 -p:Platform=x64
```

The resulting bridge DLL will be located at:

```text
bridge\ConsoleApp1\ConsoleApp1\bin\x64\Debug\net8.0\ConsoleApp1.exe
```

---

## Step 4 — Test the Bridge

Open Tekla Structural Designer and load any model.

Run:

```bash
"C:\tsd-mcp\bridge\ConsoleApp1\ConsoleApp1\bin\x64\Debug\net8.0\ConsoleApp1.exe" list_members
```

You should receive JSON output.

If you see:

```json
{
  "error": "TSD is not running"
}
```

Ensure TSD is open and a model is loaded.

---

## Step 5 — Connect to Claude Desktop

Open:

```text
%APPDATA%\Claude\claude_desktop_config.json
```

Add:

```json
{
  "mcpServers": {
    "tsd-mcp": {
      "command": "node",
      "args": [
        "C:\\tsd-mcp\\server\\index.js"
      ]
    }
  }
}
```

Restart Claude Desktop.

---

# Available MCP Tools

| Tool | Description |
|--------|--------|
| list_tsd_members | Lists all members and inferred member types |
| list_tsd_members_with_sections | Lists members with section size, section type, and material |
| get_tsd_design_summary | Returns member counts grouped by type |
| get_tsd_model_overview | Returns complete model inventory and section summary |
| get_tsd_validation_errors | Returns validation warnings and errors |
| get_tsd_top_utilized_members | Returns highest utilization members sorted descending |
| get_tsd_failing_members | Returns members with UC ≥ 1.0 |
| get_tsd_members_near_limit | Returns members with 0.90 ≤ UC < 1.0 |

---

# Example Prompts

Try asking:

- List all members in my TSD model
- Show all member section sizes
- Give me a design summary
- Give me a complete model overview
- What are the most common sections in this model?
- Show me the top utilized members
- Which members are failing?
- Which members are close to failing?
- Are there any validation errors?

---

# Technical Notes

## Connection Method

The bridge connects to a running TSD instance using:

```csharp
ApplicationFactory.GetRunningApplicationsAsync()
```

TSD must already be running.

The bridge does not launch TSD.

---

## Object Hierarchy

The Remoting API follows:

```text
Application
    ↓
Document
    ↓
Model
    ↓
Member
    ↓
Span
    ↓
ElementSection
    ↓
PhysicalSection
```

Section sizes are extracted from:

```text
PhysicalSection.LongName
```

Examples:

```text
W 14x193
HSS 10x10x1/2
W 24x76
```

---

## Design Check Results

Utilization ratios and design statuses are extracted from:

```text
Member
  ↓
Span
  ↓
CheckResults
  ↓
CheckResult
      ├─ CheckStatus
      └─ UtilizationRatio
```

Supported check categories discovered so far:

- Static
- RSA
- Member Stability

---

## Member Type Classification

The Remoting API exposes all members simply as "Member".

Member types are inferred from TSD naming conventions:

| Prefix | Type |
|----------|----------|
| B | Beam |
| C | Column |
| BR | Brace |
| CBase | Column Base Plate |

---

## x64 Requirement

The Tekla Structural Designer Remoting API targets AMD64.

The bridge must be compiled for x64.

Building as Any CPU may compile successfully but can fail at runtime.

---

# Roadmap

## Completed

- [x] Connect to live TSD 2025 instance
- [x] Member discovery
- [x] Member classification
- [x] Section extraction
- [x] Material extraction
- [x] Design summary
- [x] Model overview
- [x] Validation extraction
- [x] Utilization extraction
- [x] Top utilized members
- [x] Failing members
- [x] Near-limit members

## Planned

- [ ] Member details
- [ ] Members by section
- [ ] Members by type
- [ ] Load combination extraction
- [ ] Solver warning extraction
- [ ] Design result extraction
- [ ] Excel export
- [ ] AI-assisted design review workflows

---

# About

Built by **Vibhanshu Mishra, PE**

Structural Engineer specializing in steel and mission-critical structures.

Building AI tools for structural engineers.

---

# License

MIT License

Free to use, modify, and distribute.
