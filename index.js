import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { execFileSync } from "child_process";

const BRIDGE = "C:\\tsd-mcp\\bridge\\ConsoleApp1\\ConsoleApp1\\bin\\x64\\Debug\\net8.0\\ConsoleApp1.exe";

function runBridge(command, ...args) {
  try {
    const output = execFileSync(BRIDGE, [command, ...args], {
      encoding: "utf8",
      timeout: 30000
    });
    const lines = output.trim().split("\n");
    const jsonLine = lines.find(l => l.trim().startsWith("[") || l.trim().startsWith("{"));
    return jsonLine ? JSON.parse(jsonLine) : { raw: output };
  } catch (err) {
    return { error: err.message };
  }
}

const server = new Server(
  { name: "tsd-mcp", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "list_tsd_members",
      description: "List all members in the open TSD model with their names and inferred types",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_takeoff_by_member_type",
      description: "Get steel takeoff summary grouped by member type with total length and weight",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_official_material_quantities",
      description: "Get official material quantities reported by TSD, including steel mass, connectors count, volume, surface area, and embodied carbon",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "list_tsd_members_with_sections",
      description: "List all members in the open TSD model with names, types, and section sizes",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_design_summary",
      description: "Get total member count grouped by inferred member type",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_model_overview",
      description: "Get an overview of the open TSD model including total members, member types, sections, and materials",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_steel_takeoff",
      description: "Get steel material takeoff with total length and calculated weight grouped by section and member type",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_validation_errors",
      description: "Get validation errors and warnings from the open TSD model",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_takeoff_by_section_type",
      description: "Get steel takeoff summary grouped by section type with total length and weight",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_heaviest_sections",
      description: "Get the sections contributing the most total steel weight in the open TSD model",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_design_status_summary",
      description: "Get a summary of design check statuses in the open TSD model",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_load_combinations",
      description: "List load combinations in the open TSD model with IDs, names, reference indexes, and strength/service status",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_members_near_limit",
      description: "Get members in the open TSD model with utilization ratio between 0.90 and 1.0",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_failing_members",
      description: "Get all members in the open TSD model with utilization ratio greater than or equal to 1.0",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_solver_warnings",
      description: "Get solver warnings and errors for the selected analysis type in the open TSD model",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_top_utilized_members",
      description: "Get the top utilized members in the open TSD model sorted by utilization ratio descending",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_member_forces",
      description: "Get line element end forces for a specific member under a load combination. The combination can be provided by reference index, full name, or partial name.",
      inputSchema: {
        type: "object",
        properties: {
          member: {
            type: "string",
            description: "Member name, for example B4869"
          },
          combination: {
            type: "string",
            description: "Load combination reference index, full name, or partial name. Examples: 18, '18 LRFD_{4.1}-1.2D+1.6L+0.5S', or LRFD"
          }
        },
        required: ["member", "combination"]
      }
    },
    {
      name: "get_tsd_model_cost_estimate",
      description: "Estimate total steel material cost using calculated model tonnage and a user-provided cost per ton",
      inputSchema: {
        type: "object",
        properties: {
          cost_per_ton: {
            type: "number",
            description: "Steel cost per ton, for example 4200"
          }
        },
        required: ["cost_per_ton"]
      }
    },
    {
      name: "get_tsd_members_by_section",
      description: "Get all members in the open TSD model matching a specific section size",
      inputSchema: {
        type: "object",
        properties: {
          section: {
            type: "string",
            description: "Section size, for example W 33x130"
          }
        },
        required: ["section"]
      }
    },
    {
      name: "get_tsd_members_by_type",
      description: "Get all members in the open TSD model matching a specific member type",
      inputSchema: {
        type: "object",
        properties: {
          type: {
            type: "string",
            description: "Member type: Beam, Column, Brace, or Column Base Plate"
           }
         },
         required: ["type"]
       }
    },
    {
      name: "get_tsd_analysis_status",
      description: "Get the selected analysis type and confirm whether TSD is running with a model open",
      inputSchema: { type: "object", properties: {} }
    },
    {
      name: "get_tsd_member_force_envelope",
      description: "Returns the governing force envelope for a member across load combinations, including maximum positive, maximum negative, and governing axial force, shear, moment, torsion, and deflection values.",
      inputSchema: {
        type: "object",
        properties: {
          member: {
            type: "string",
            description: "Member name, for example B4869"
          },
          mode: {
            type: "string",
            description: "Optional. Use 'active_strength_combinations' (default) or 'all'.",
            enum: ["active_strength_combinations", "all"]
          }
        },
        required: ["member"]
      }
    },
    {
      name: "get_tsd_member_details",
      description: "Get section, material, and design check details for a specific TSD member",
      inputSchema: {
        type: "object",
        properties: {
          member: {
            type: "string",
            description: "TSD member name, for example B4869"
          }
        },
        required: ["member"]
      }
    }
  ]
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name } = request.params;
  let result;

  if (name === "list_tsd_members") {
    result = runBridge("list_members");
  } else if (name === "list_tsd_members_with_sections") {
    result = runBridge("list_members_with_sections");
  } else if (name === "get_tsd_design_summary") {
    result = runBridge("get_design_summary");
  } else if (name === "get_tsd_model_overview") {
    result = runBridge("get_model_overview");
  } else if (name === "get_tsd_top_utilized_members") {
    result = runBridge("get_top_utilized_members");
  } else if (name === "get_tsd_failing_members") {
    result = runBridge("get_failing_members");
  } else if (name === "get_tsd_members_near_limit") {
    result = runBridge("get_members_near_limit");
  } else if (name === "get_tsd_member_details") {
    const member = request.params.arguments?.member;
    result = runBridge("get_member_details", member);
  } else if (name === "get_tsd_members_by_section") {
    const section = request.params.arguments?.section;
    result = runBridge("get_members_by_section", section);
  } else if (name === "get_tsd_members_by_type") {
    const type = request.params.arguments?.type;
    result = runBridge("get_members_by_type", type);
  } else if (name === "get_tsd_validation_errors") {
    result = runBridge("get_validation_errors");
  } else if (name === "get_tsd_load_combinations") {
    result = runBridge("get_load_combinations");
  } else if (name === "get_tsd_steel_takeoff") {
    result = runBridge("get_steel_takeoff");
  } else if (name === "get_tsd_takeoff_by_section_type") {
    result = runBridge("get_takeoff_by_section_type");
  } else if (name === "get_tsd_takeoff_by_member_type") {
    result = runBridge("get_takeoff_by_member_type");
  } else if (name === "get_tsd_heaviest_sections") {
      result = runBridge("get_heaviest_sections");
  } else if (name === "get_tsd_solver_warnings") {
      result = runBridge("get_solver_warnings");
  } else if (name === "get_tsd_official_material_quantities") {
      result = runBridge("get_official_material_quantities");
  } else if (name === "get_tsd_analysis_status") {
      result = runBridge("get_analysis_status");
  } else if (name === "get_tsd_member_force_envelope") {
      const member = request.params.arguments?.member;
      const mode =
        request.params.arguments?.mode ?? "active_strength_combinations";

      result = runBridge(
        "get_member_force_envelope",
        String(member),
        String(mode)
      );
  } else if (name === "get_tsd_member_forces") {
      const member = request.params.arguments?.member;
      const combo = request.params.arguments?.combination;
      result = runBridge("get_member_forces", String(member), String(combo));
  } else if (name === "get_tsd_model_cost_estimate") {
    const costPerTon = request.params.arguments?.cost_per_ton;
    result = runBridge("get_model_cost_estimate", String(costPerTon));
  } else if (name === "get_tsd_design_status_summary") {
    result = runBridge("get_design_status_summary");
  } else {
    return { content: [{ type: "text", text: `Unknown tool: ${name}` }] };
  }

  return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
});

const transport = new StdioServerTransport();
await server.connect(transport);
