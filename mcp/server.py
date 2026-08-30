from __future__ import annotations

import json
import os
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any

from mcp.server import MCPServer
from mcp.types import ToolAnnotations
from starlette.responses import JSONResponse

from auth import (
  COGNITO_AUTHORIZATION_ENDPOINT,
  COGNITO_ISSUER,
  COGNITO_REQUIRED_SCOPES,
  COGNITO_TOKEN_ENDPOINT,
  auth_settings,
  token_verifier,
)

MCP_HTTP_HOST = os.environ.get("MCP_HTTP_HOST", "127.0.0.1")
MCP_HTTP_PORT = int(os.environ.get("MCP_HTTP_PORT", "8000"))
MCP_HTTP_PATH = os.environ.get("MCP_HTTP_PATH", "/mcp")

MANIPULATOR_URL = os.environ.get(
  "MANIPULATOR_URL",
  "http://host.docker.internal:50052",
).rstrip("/")

MANIPULATOR_TIMEOUT_SECONDS = float(
  os.environ.get("MANIPULATOR_TIMEOUT_SECONDS", "10")
)


READ_ONLY_TOOL_ANNOTATIONS = ToolAnnotations(
  read_only_hint=True,
  destructive_hint=False,
  open_world_hint=False,
)


@dataclass(frozen=True)
class DocumentInfo:
  central_topic_text: str
  node_count: int
  leaf_count: int
  max_depth: int
  average_leaf_depth: float


@dataclass(frozen=True)
class OutlineNode:
  id: str
  text: str
  icon_markers: list[str]
  descendant_count: int
  subtree_revision: int
  children: list[OutlineNode]


@dataclass(frozen=True)
class ReadOutlineResult:
  observation_id: str
  revision: int
  root: OutlineNode
  returned_node_count: int
  truncated: bool


mcp = MCPServer(
  "mindmanager-readonly",
  instructions=(
    "Provides read-only access to the logical outline of the one "
    "MindManager document explicitly selected by the user in Windows. "
    "The server cannot choose or open arbitrary documents. "
    "For large maps, prefer get_document_info first, then use bounded "
    "read_outline calls and descendant_count to choose which branches "
    "to inspect. read_outline also returns observation/revision metadata "
    "that can be used to identify branches whose complete logical subtree "
    "changed since an earlier read within the same observation_id."
  ),
  token_verifier=token_verifier,
  auth=auth_settings,
)


def _decode_json(raw: bytes) -> dict[str, Any]:
  try:
    value = json.loads(raw.decode("utf-8"))
  except (UnicodeDecodeError, json.JSONDecodeError) as exc:
    raise RuntimeError(
      "MindManager Manipulator returned invalid JSON."
    ) from exc

  if not isinstance(value, dict):
    raise RuntimeError(
      "MindManager Manipulator returned a non-object JSON response."
    )

  return value


def _request_json(
  method: str,
  path: str,
  body: dict[str, Any] | None = None,
) -> dict[str, Any]:
  data = None
  headers = {
    "Accept": "application/json",
  }

  if body is not None:
    data = json.dumps(
      body,
      ensure_ascii=False,
      separators=(",", ":"),
    ).encode("utf-8")
    headers["Content-Type"] = "application/json"

  request = urllib.request.Request(
    MANIPULATOR_URL+path,
    data=data,
    headers=headers,
    method=method,
  )

  try:
    with urllib.request.urlopen(
      request,
      timeout=MANIPULATOR_TIMEOUT_SECONDS,
    ) as response:
      return _decode_json(response.read())

  except urllib.error.HTTPError as exc:
    raw = exc.read()

    try:
      error = _decode_json(raw)
      code = str(error.get("error", "http_error"))
      message = str(error.get("message", ""))
    except RuntimeError:
      code = "http_error"
      message = raw.decode("utf-8", errors="replace")

    raise RuntimeError(
      f"MindManager Manipulator returned HTTP {exc.code}: "
      f"{code}: {message}"
    ) from exc

  except urllib.error.URLError as exc:
    raise RuntimeError(
      f"MindManager Manipulator is unavailable: {exc.reason}"
    ) from exc

  except TimeoutError as exc:
    raise RuntimeError(
      "MindManager Manipulator request timed out."
    ) from exc


def _require_str(
  value: dict[str, Any],
  key: str,
) -> str:
  result = value.get(key)
  if not isinstance(result, str):
    raise RuntimeError(
      f"MindManager Manipulator returned invalid {key}."
    )
  return result


def _require_int(
  value: dict[str, Any],
  key: str,
) -> int:
  result = value.get(key)
  if isinstance(result, bool) or not isinstance(result, int):
    raise RuntimeError(
      f"MindManager Manipulator returned invalid {key}."
    )
  return result


def _require_number(
  value: dict[str, Any],
  key: str,
) -> float:
  result = value.get(key)
  if isinstance(result, bool) or not isinstance(result, (int, float)):
    raise RuntimeError(
      f"MindManager Manipulator returned invalid {key}."
    )
  return float(result)


def _require_bool(
  value: dict[str, Any],
  key: str,
) -> bool:
  result = value.get(key)
  if not isinstance(result, bool):
    raise RuntimeError(
      f"MindManager Manipulator returned invalid {key}."
    )
  return result


def _parse_outline_node(value: Any) -> OutlineNode:
  if not isinstance(value, dict):
    raise RuntimeError(
      "MindManager Manipulator returned an invalid outline node."
    )

  raw_icon_markers = value.get("iconMarkers")
  if (
    not isinstance(raw_icon_markers, list)
    or not all(isinstance(x, str) for x in raw_icon_markers)
  ):
    raise RuntimeError(
      "MindManager Manipulator returned invalid iconMarkers."
    )

  raw_children = value.get("children")
  if not isinstance(raw_children, list):
    raise RuntimeError(
      "MindManager Manipulator returned invalid children."
    )

  return OutlineNode(
    id=_require_str(value, "id"),
    text=_require_str(value, "text"),
    icon_markers=list(raw_icon_markers),
    descendant_count=_require_int(value, "descendantCount"),
    subtree_revision=_require_int(value, "subtreeRevision"),
    children=[
      _parse_outline_node(child)
      for child in raw_children
    ],
  )


@mcp.tool(
  title="Get MindManager document info",
  description=(
    "Return summary statistics for the MindManager document currently "
    "exposed by the Windows user. The Central Topic has depth 0. "
    "Use this before reading a large outline to decide how broadly "
    "or deeply to inspect it."
  ),
  annotations=READ_ONLY_TOOL_ANNOTATIONS,
)
def get_document_info() -> DocumentInfo:
  value = _request_json(
    "GET",
    "/v1/document-info",
  )

  return DocumentInfo(
    central_topic_text=_require_str(
      value,
      "centralTopicText",
    ),
    node_count=_require_int(
      value,
      "nodeCount",
    ),
    leaf_count=_require_int(
      value,
      "leafCount",
    ),
    max_depth=_require_int(
      value,
      "maxDepth",
    ),
    average_leaf_depth=_require_number(
      value,
      "averageLeafDepth",
    ),
  )


@mcp.tool(
  title="Read MindManager outline",
  description=(
    "Read part or all of the logical outline of the MindManager "
    "document currently exposed by the Windows user. "
    "Omit start_id to begin at the Central Topic. "
    "When start_id is supplied, copy an opaque topic id exactly from "
    "an earlier read_outline result; that topic becomes depth 0. "
    "Omit max_depth for the complete subtree. "
    "max_depth=0 returns only the start node. "
    "Each node's descendant_count is the number of all descendants "
    "in its complete original subtree, including descendants omitted "
    "from this response by max_depth. "
    "Each result also contains observation_id and revision. "
    "observation_id changes when the Manipulator observation state is "
    "reset, including when the selected document changes; revision values "
    "must only be compared when observation_id is identical. "
    "Each node's subtree_revision is the latest global revision at which "
    "a change was observed in that node's complete logical subtree, "
    "including descendants omitted by max_depth. "
    "To find branches changed since an earlier read_outline result from "
    "the same observation_id, inspect nodes whose subtree_revision is "
    "greater than the earlier result's revision. "
    "icon_markers contains raw MindManager marker identifiers."
  ),
  annotations=READ_ONLY_TOOL_ANNOTATIONS,
)
def read_outline(
  start_id: str | None = None,
  max_depth: int | None = None,
) -> ReadOutlineResult:
  if start_id == "":
    raise ValueError("start_id must not be empty.")

  if max_depth is not None and max_depth < 0:
    raise ValueError("max_depth must not be negative.")

  body: dict[str, Any] = {}

  if start_id is not None:
    body["startId"] = start_id

  if max_depth is not None:
    body["maxDepth"] = max_depth

  value = _request_json(
    "POST",
    "/v1/read-outline",
    body,
  )

  return ReadOutlineResult(
    observation_id=_require_str(value, "observationId"),
    revision=_require_int(value, "revision"),
    root=_parse_outline_node(value.get("root")),
    returned_node_count=_require_int(value, "returnedNodeCount"),
    truncated=_require_bool(value, "truncated"),
  )


@mcp.custom_route(
  "/healthz",
  methods=["GET"],
  include_in_schema=False,
)
async def healthz(request):
  return JSONResponse({"status": "ok"})


@mcp.custom_route(
  "/.well-known/oauth-authorization-server/oauth",
  methods=["GET"],
  include_in_schema=False,
)
async def oauth_authorization_server_metadata(request):
  return JSONResponse({
    "issuer": COGNITO_ISSUER,
    "authorization_endpoint": COGNITO_AUTHORIZATION_ENDPOINT,
    "token_endpoint": COGNITO_TOKEN_ENDPOINT,
    "response_types_supported": [
      "code",
    ],
    "grant_types_supported": [
      "authorization_code",
      "refresh_token",
    ],
    "token_endpoint_auth_methods_supported": [
      "client_secret_basic",
      "client_secret_post",
    ],
    "code_challenge_methods_supported": [
      "S256",
    ],
    "scopes_supported": COGNITO_REQUIRED_SCOPES,
  })


if __name__ == "__main__":
  mcp.run(
    transport="streamable-http",
    host=MCP_HTTP_HOST,
    port=MCP_HTTP_PORT,
    streamable_http_path=MCP_HTTP_PATH,
    stateless_http=True,
    json_response=True,
  )
