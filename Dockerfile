FROM python:3.12-slim

RUN useradd -m -u 10001 mcp

WORKDIR /app

COPY mcp/requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir -r /tmp/requirements.txt

COPY bin/tunnel-client-runtime /usr/local/bin/tunnel-client
RUN chmod +x /usr/local/bin/tunnel-client ;\
    install -d -o mcp -m 750 "$HOME/.config"

RUN apt update \
    && apt install -y --no-install-recommends curl jq \
    && rm -rf /var/lib/apt/lists/*

USER mcp

ENV MCP_ROOT=/workspace

ENTRYPOINT ["/bin/sh", "/app/entrypoint.sh"]
