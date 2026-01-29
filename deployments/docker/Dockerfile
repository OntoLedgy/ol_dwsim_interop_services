FROM python:3.11-slim AS builder

WORKDIR /artifacts
COPY mcp_service/server/prebuilt/DwsimWorker/ /artifacts/

FROM python:3.11-slim AS runtime

ENV DEBIAN_FRONTEND=noninteractive \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    WINEARCH=win64 \
    WINEDEBUG=-all \
    WINEPREFIX=/opt/wineprefix

# Enable 32-bit architecture and install Wine
RUN dpkg --add-architecture i386 \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        cabextract \
        curl \
        gnupg2 \
        wget \
        xauth \
        xvfb \
    && mkdir -pm755 /etc/apt/keyrings \
    && wget -O /etc/apt/keyrings/winehq-archive.key https://dl.winehq.org/wine-builds/winehq.key \
    && wget -NP /etc/apt/sources.list.d/ https://dl.winehq.org/wine-builds/debian/dists/bookworm/winehq-bookworm.sources \
    && apt-get update \
    && apt-get install -y --install-recommends winehq-stable \
    && rm -rf /var/lib/apt/lists/*

# Install winetricks
RUN wget -O /usr/local/bin/winetricks https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks \
    && chmod +x /usr/local/bin/winetricks

# Initialize Wine and install .NET Framework 4.8
ENV XDG_RUNTIME_DIR=/tmp/runtime-root
RUN mkdir -p "$WINEPREFIX" "$XDG_RUNTIME_DIR" \
    && chmod 700 "$XDG_RUNTIME_DIR" \
    && wineboot --init \
    && xvfb-run --auto-servernum winetricks -q dotnet48 \
    && rm -rf /tmp/.wine* /tmp/winetricks* "$XDG_RUNTIME_DIR"

RUN useradd --create-home --shell /bin/bash app \
    && mkdir -p /app /opt/dwsim \
    && chown -R app:app /app /opt/dwsim "$WINEPREFIX"

COPY --from=builder /artifacts/ /opt/dwsim/
COPY mcp_service/server/ /app/

WORKDIR /app
RUN python -m pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir .

# Wine/.NET limitations:
# - .NET Framework 4.8 on Wine is best-effort and may fail for some Windows APIs.
# - GUI/COM components and some DWSIM features are not supported under Wine.
# - For full compatibility, prefer Windows containers or a Windows host runtime.

USER app

EXPOSE 9090

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD dwsim-mcp doctor --quiet || exit 1

CMD ["dwsim-mcp"]
