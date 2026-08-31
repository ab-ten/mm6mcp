import json
import logging
import time
from collections.abc import Mapping
from typing import Any

from mcp.server.auth.middleware.auth_context import get_access_token
from mcp.server.context import CallNext, HandlerResult, ServerRequestContext
from mcp_types import CLIENT_INFO_META_KEY


ACCESS_LOGGER = logging.getLogger("mcp.access")
ACCESS_LOG_STRING_LIMIT = 500
ACCESS_LOG_SEQUENCE_LIMIT = 50

SENSITIVE_LOG_KEYS = frozenset({
  "authorization",
  "access_token",
  "api_key",
  "apikey",
  "password",
  "secret",
  "token",
})


def _sanitize_log_value(value: Any, key: str = "") -> Any:
  if key.casefold() in SENSITIVE_LOG_KEYS:
    return "<redacted>"

  if value is None or isinstance(value, (bool, int, float)):
    return value

  if isinstance(value, str):
    if len(value) <= ACCESS_LOG_STRING_LIMIT:
      return value
    return f"{value[:ACCESS_LOG_STRING_LIMIT]}...<{len(value)} chars>"

  if isinstance(value, Mapping):
    return {
      str(child_key): _sanitize_log_value(child_value, str(child_key))
      for child_key, child_value in value.items()
    }

  if isinstance(value, (list, tuple)):
    values = [
      _sanitize_log_value(child_value)
      for child_value in value[:ACCESS_LOG_SEQUENCE_LIMIT]
    ]

    if len(value) > ACCESS_LOG_SEQUENCE_LIMIT:
      values.append(f"<{len(value)-ACCESS_LOG_SEQUENCE_LIMIT} more items>")

    return values

  return _sanitize_log_value(repr(value))


def _read_client_info(
  ctx: ServerRequestContext,
) -> tuple[str | None, str | None]:
  if ctx.meta is None:
    return None, None

  value = ctx.meta.get(CLIENT_INFO_META_KEY)
  if not isinstance(value, Mapping):
    return None, None

  name = value.get("name")
  version = value.get("version")

  return (
    name if isinstance(name, str) else None,
    version if isinstance(version, str) else None,
  )


async def access_log_middleware(
  ctx: ServerRequestContext,
  call_next: CallNext,
) -> HandlerResult:
  if ctx.method != "tools/call":
    return await call_next(ctx)

  started_at = time.perf_counter()
  params = ctx.params or {}
  tool = params.get("name")
  arguments = params.get("arguments")
  mcp_client_name, mcp_client_version = _read_client_info(ctx)
  access_token = get_access_token()

  event: dict[str, Any] = {
    "event": "mcp_tool_call",
    "tool": tool if isinstance(tool, str) else "<unknown>",
    "request_id": ctx.request_id,
    "protocol_version": ctx.protocol_version,
    "arguments": _sanitize_log_value(arguments if isinstance(arguments, Mapping) else {}),
  }

  if mcp_client_name is not None:
    event["mcp_client_name"] = mcp_client_name

  if mcp_client_version is not None:
    event["mcp_client_version"] = mcp_client_version

  if access_token is not None:
    event["oauth_client_id"] = access_token.client_id

  try:
    result = await call_next(ctx)
  except Exception as exc:
    event["status"] = "error"
    event["error_type"] = type(exc).__name__
    raise
  else:
    event["status"] = "ok"
    event["result_type"] = type(result).__name__
    return result
  finally:
    event["duration_ms"] = round((time.perf_counter()-started_at)*1000, 3)
    ACCESS_LOGGER.info("%s", json.dumps(event, ensure_ascii=False, separators=(",", ":")))
