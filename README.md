# TSD MCP Server

> Connect Claude AI directly to live Tekla Structural Designer 2025 models using the Model Context Protocol (MCP).

Built by a structural engineer, for structural engineers.

The TSD MCP Server allows Claude Desktop to query live Tekla Structural Designer models using natural language. Ask questions about members, sections, utilization ratios, design status, steel tonnage, takeoffs, and material costs without clicking through the TSD interface.

---

# Features

## Model Exploration

* List all members
* Member type classification (Beam, Column, Brace, Column Base Plate)
* Section extraction
* Material extraction
* Model overview summaries
* Member filtering by type
* Member filtering by section
* Detailed member inspection

## Design Review

* Utilization ratio extraction
* Top utilized members
* Failing member identification
* Near-failing member identification
* Design status summaries
* Validation error extraction

## Quantity Takeoff & Estimating

* Steel takeoff generation
* Total model tonnage
* Weight per foot calculations
* Takeoff grouped by section
* Takeoff grouped by member type
* Takeoff grouped by section type
* Heaviest section analysis
* Material cost estimation

---

# Requirements

* Windows
* Tekla Structural Designer 2025
* Active TSD license
* TSD running with a model open
* Claude Desktop
* Node.js
* Visual Studio 2022+
* .NET 8

---

# Architecture

Claude Desktop

↓

MCP

↓

Node.js MCP Server

↓

C# Bridge

↓

Tekla Structural Designer Remoting API

↓

Open TSD Model

---

# Installation

## Build the C# Bridge

Build as x64:

```bash
dotnet build -f net8.0 -p:Platform=x64
```

Compiled executable:

```text
bridge\ConsoleApp1\ConsoleApp1\bin\x64\Debug\net8.0\ConsoleApp1.exe
```

---

## Test the Bridge

```bash
"C:\tsd-mcp\bridge\ConsoleApp1\ConsoleApp1\bin\x64\Debug\net8.0\ConsoleApp1.exe" list_members
```

TSD must be open with a model loaded.

---

## Configure Claude Desktop

Edit:

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

# Available Tools

## Member Tools

### list_tsd_members

Lists all members in the open model.

### list_tsd_members_with_sections

Lists all members with:

* Member Name
* Member Type
* Section Size

### get_tsd_member_details

Returns:

* Member Name
* Member Type
* Section
* Material
* Design Checks
* Utilization Ratios

Example:

```text
Tell me about member B4869
```

### get_tsd_members_by_type

Returns all members matching:

* Beam
* Column
* Brace
* Column Base Plate

Example:

```text
Show me all braces
```

### get_tsd_members_by_section

Returns all members using a given section.

Example:

```text
Show me all W 33x130 members
```

---

## Model Summary Tools

### get_tsd_design_summary

Returns total member counts grouped by type.

### get_tsd_model_overview

Returns:

* Total Members
* Member Types
* Sections
* Materials

---

## Design Review Tools

### get_tsd_validation_errors

Returns model validation warnings and errors.

### get_tsd_design_status_summary

Returns:

* Pass
* Warning
* Fail
* Not Required
* Unknown

counts across the model.

### get_tsd_failing_members

Returns members with:

```text
Utilization Ratio >= 1.0
```

### get_tsd_members_near_limit

Returns members with:

```text
0.90 <= Utilization Ratio < 1.0
```

### get_tsd_top_utilized_members

Returns members sorted by utilization ratio descending.

---

## Steel Takeoff Tools

### get_tsd_steel_takeoff

Generates:

* Total Length
* Weight per Foot
* Total Weight
* Total Tons

grouped by:

* Section
* Section Type
* Member Type

Example:

```text
Generate a steel takeoff
```

### get_tsd_takeoff_by_member_type

Returns takeoff grouped by:

* Beam
* Column
* Brace
* Column Base Plate

Example:

```text
How many tons are beams?
```

### get_tsd_takeoff_by_section_type

Returns takeoff grouped by:

* W Shapes
* HSS
* Angles
* Other Section Types

Example:

```text
How many tons are HSS?
```

### get_tsd_heaviest_sections

Returns sections contributing the most tonnage.

Example:

```text
Show me the heaviest sections in the model
```

---

## Estimating Tools

### get_tsd_model_cost_estimate

Input:

```text
Cost Per Ton
```

Example:

```text
Estimate steel cost at $4200 per ton
```

Output:

```json
{
  "total_weight_tons": 3471.04,
  "cost_per_ton": 4200,
  "estimated_material_cost": 14578368
}
```

---

# Example Prompts

## Model Exploration

```text
List all members
```

```text
Show me all beams
```

```text
Show me all braces
```

```text
Show me all W 33x130 members
```

```text
Tell me about B4869
```

---

## Design Review

```text
Show me all failing members
```

```text
Show me members near failure
```

```text
Show me the most critical members
```

```text
Give me a design status summary
```

```text
Are there any validation errors?
```

---

## Steel Takeoff

```text
Generate a steel takeoff
```

```text
How many tons of steel are in this model?
```

```text
How many tons are W-shapes?
```

```text
Show me the heaviest sections
```

```text
What section contributes the most weight?
```

---

## Estimating

```text
Estimate steel cost at $4200 per ton
```

```text
How much steel is in this building?
```

```text
What's the total tonnage?
```

---

# Important Notes

## TSD Must Be Running

The bridge connects to an active TSD process.

If TSD is not running or no model is open, tools will fail.

## x64 Required

The Tekla Structural Designer Remoting API is AMD64 only.

Build the bridge as:

```text
x64
```

Do not use:

```text
Any CPU
```

## Single Model

The bridge connects to the first active TSD instance.

---

# Technical Notes

## Member Type Inference

TSD exposes all members generically as "Member".

The server infers:

```text
B*       → Beam
C*       → Column
BR*      → Brace
CBase*   → Column Base Plate
```

using TSD naming conventions.

## Section Extraction

Section names are extracted directly from:

```text
ElementSection
→ PhysicalSection
→ LongName
```

Examples:

```text
W 33x130
W 24x76
HSS 10x10x1/2
```

## Steel Weight Calculations

Steel takeoff calculations are derived directly from TSD section properties:

```text
Mass
Length
```

No external AISC database is required.

---

# Roadmap

## Completed

* Live TSD Connection
* Member Listing
* Member Type Classification
* Section Extraction
* Material Extraction
* Model Overview
* Validation Errors
* Design Status Summary
* Member Details
* Members By Type
* Members By Section
* Utilization Ratios
* Failing Members
* Near-Failing Members
* Top Utilized Members
* Steel Takeoff
* Takeoff By Member Type
* Takeoff By Section Type
* Heaviest Sections
* Material Cost Estimation

## Planned

* Load Combination Extraction
* Solver Warnings
* Analysis Results
* Member Forces
* Reactions
* Governing Load Combinations
* Connection Design Summaries
* Excel Export
* CSV Export

---

# License

MIT License

---

# Author

Vibhanshu Mishra, PE

Structural Engineer

Building AI tools for structural engineers.
