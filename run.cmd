rem @echo off -*- coding: cp932-dos; -*-
setlocal

REM Convert relative path to absolute path
for %%I in (.) do set ABS_PATH=%%~fI

set OPTIONS=-v "%~dp0\mcp:/app:ro"
if exist "%HOME%\mm6mcp.env" (
  set OPTIONS=%OPTIONS% --env-file "%HOME%\mm6mcp.env"
) else if exist "%~dp0\.env" (
  set OPTIONS=%OPTIONS% --env-file "%~dp0\.env"
)

docker run --rm -it --init %OPTIONS% -v "%ABS_PATH%:/workspace:ro" --tmpfs /tmp:rw,nosuid,nodev,noexec,size=64m --name mm6mcp %* mm6mcp
