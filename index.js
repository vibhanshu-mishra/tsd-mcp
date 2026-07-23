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
      name: "get_tsd_top_utilized_members",
      description: "Get the top utilized members in the open TSD model sorted by utilization ratio descending",
      inputSchema: { type: "object", properties: {} }
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
  } else if (name === "get_tsd_steel_takeoff") {
    result = runBridge("get_steel_takeoff");
  } else if (name === "get_tsd_takeoff_by_section_type") {
    result = runBridge("get_takeoff_by_section_type");
  } else if (name === "get_tsd_takeoff_by_member_type") {
    result = runBridge("get_takeoff_by_member_type");
  } else if (name === "get_tsd_heaviest_sections") {
    result = runBridge("get_heaviest_sections");
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
