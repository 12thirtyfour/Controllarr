#!/usr/bin/env bash

# Color codes
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo "Starting Environment Verification..."

# Track overall status
STATUS=0

# 1. Verify .NET 10 SDK
echo -n "Checking .NET 10 SDK... "
if dotnet --list-sdks | grep -q "^10\."; then
    echo -e "${GREEN}[PASS]${NC}"
else
    echo -e "${RED}[FAIL]${NC}"
    echo "  -> Remediation: Install .NET 10 SDK from https://dotnet.microsoft.com/"
    STATUS=1
fi

# 2. Verify Node.js (version 20+)
echo -n "Checking Node.js (v20+)... "
if command -v node >/dev/null 2>&1; then
    NODE_VERSION=$(node -v | cut -d'v' -f2 | cut -d'.' -f1)
    if [ "$NODE_VERSION" -ge 20 ]; then
        echo -e "${GREEN}[PASS]${NC}"
    else
        echo -e "${RED}[FAIL]${NC}"
        echo "  -> Remediation: Upgrade Node.js. Current version: $(node -v). Requires v20+."
        STATUS=1
    fi
else
    echo -e "${RED}[FAIL]${NC}"
    echo "  -> Remediation: Install Node.js v20+ from https://nodejs.org/"
    STATUS=1
fi

# 3. Verify package managers (npm or yarn)
echo -n "Checking Package Manager (npm or yarn)... "
if command -v npm >/dev/null 2>&1 || command -v yarn >/dev/null 2>&1; then
    echo -e "${GREEN}[PASS]${NC}"
else
    echo -e "${RED}[FAIL]${NC}"
    echo "  -> Remediation: Install npm or yarn."
    STATUS=1
fi

# 4. Verify ROM processing tools (chdman, 7z/p7zip)
echo -n "Checking chdman... "
if command -v chdman >/dev/null 2>&1; then
    echo -e "${GREEN}[PASS]${NC}"
else
    echo -e "${RED}[FAIL]${NC}"
    echo "  -> Remediation: macOS: 'brew install rom-tools' | Debian/Ubuntu: 'apt-get install mame-tools'"
    STATUS=1
fi

echo -n "Checking 7z/p7zip... "
if command -v 7z >/dev/null 2>&1 || command -v 7za >/dev/null 2>&1; then
    echo -e "${GREEN}[PASS]${NC}"
else
    echo -e "${RED}[FAIL]${NC}"
    echo "  -> Remediation: macOS: 'brew install p7zip' | Debian/Ubuntu: 'apt-get install p7zip-full'"
    STATUS=1
fi

# 5. Verify API port availability (8989)
echo -n "Checking Port 8989 availability... "
if command -v lsof >/dev/null 2>&1; then
    if lsof -i :8989 >/dev/null 2>&1; then
        echo -e "${YELLOW}[WARN]${NC}"
        echo "  -> Port 8989 is currently in use. Controllarr API may fail to start."
    else
        echo -e "${GREEN}[PASS]${NC}"
    fi
elif command -v netstat >/dev/null 2>&1; then
    if netstat -tuln | grep -q ":8989 "; then
        echo -e "${YELLOW}[WARN]${NC}"
        echo "  -> Port 8989 is currently in use. Controllarr API may fail to start."
    else
        echo -e "${GREEN}[PASS]${NC}"
    fi
else
    echo -e "${YELLOW}[WARN]${NC}"
    echo "  -> Could not verify port availability (lsof/netstat not found)."
fi

[ $STATUS -eq 0 ] && echo "Environment check passed." || echo "Environment check failed."
