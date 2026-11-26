"""Exercise the published API's private MCP stdio server without Azure access."""
import json, os, pathlib, selectors, subprocess
root = pathlib.Path(__file__).resolve().parents[1]
dll = root / "src/TelecomIQ.Api/bin/Release/net10.0/TelecomIQ.Api.dll"
with subprocess.Popen(["dotnet", str(dll), "--mcp-review-tools", "00000000-0000-0000-0000-000000000001"],
                      stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, bufsize=1) as p:
    sel = selectors.DefaultSelector()
    sel.register(p.stdout, selectors.EVENT_READ)
    def send(method, params, ident=None):
        msg = {"jsonrpc":"2.0", "method":method, "params":params}
        if ident is not None: msg["id"] = ident
        p.stdin.write(json.dumps(msg) + "\n")
        p.stdin.flush()
        if ident is None: return
        if not sel.select(30): raise RuntimeError("MCP response timeout")
        response = json.loads(p.stdout.readline())
        assert response.get("id") == ident and "error" not in response, response
        return response["result"]
    try:
        send("initialize", {"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"CI","version":"1"}}, 1)
        send("notifications/initialized", {})
        tools = send("tools/list", {}, 2)["tools"]
        assert {t["name"] for t in tools} == {"get_policy_requirement","get_review_decisions","calculate_liability"}
        result = send("tools/call", {"name":"calculate_liability","arguments":{"annualContractValue":120000000,"months":3}}, 3)
        assert not result.get("isError"), result
        value = json.loads(next(x["text"] for x in result["content"] if x["type"] == "text"))
        assert value["liabilityCap"] == 30000000, value
        print("MCP discovery and liability calculation passed")
    finally:
        p.terminate()
        p.wait(timeout=10)
