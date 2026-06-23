import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { execFileSync } from "child_process";

const BRIDGE = "C:\\tsd-mcp\\bridge\\ConsoleApp1\\ConsoleApp1\\bin\\Debug\\net8.0\\win-x64\\ConsoleApp1.dll";

function runBridge(command) {
  try {
    const output = execFileSync("dotnet", [BRIDGE, command], {
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
  {
    name: "tsd-mcp",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {}
    }
  }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "list_tsd_members",
      description: "List all members in the open TSD model with their names and inferred types",
      inputSchema: {
        type: "object",
        properties: {}
      }
    },
    {
      name: "list_tsd_members_with_sections",
      description: "List all members in the open TSD model with names, types, and section sizes",
      inputSchema: {
        type: "object",
        properties: {}
      }
    },
    {
      name: "get_tsd_design_summary",
      description: "Get total member count grouped by inferred member type",
      inputSchema: {
        type: "object",
        properties: {}
      }
    },
    {
      name: "get_tsd_model_overview",
      description: "Get an overview of the open TSD model including total members, member types, sections, and materials",
      inputSchema: {
        type: "object",
        properties: {}
      }
    },	
    {
      name: "get_tsd_validation_errors",
      description: "Get validation errors and warnings from the open TSD model",
      inputSchema: {
        type: "object",
        properties: {}
      }
    },
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
  } else if (name === "get_tsd_validation_errors") {
    result = runBridge("get_validation_errors");
  } else {
    return {
      content: [
        {
          type: "text",
          text: `Unknown tool: ${name}`
        }
      ]
    };
  }

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(result, null, 2)
      }
    ]
  };
});

const transport = new StdioServerTransport();
await server.connect(transport);
