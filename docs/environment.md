# Local Environment Prerequisites

Before developing locally or submitting changes, please verify your environment meets the prerequisites by running our automated script.

From the root of the repository, execute:

```bash
./scripts/verify-env.sh
```

This script will check for the following:
- .NET 10 SDK (`dotnet --list-sdks`)
- Node.js (version 20+)
- Package managers (`npm` or `yarn`)
- ROM processing tools (`chdman`, `7z`/`p7zip`)
- API Port (8989) availability

If any requirement is missing, the script will provide clear remediation instructions (such as `brew install rom-tools` for `chdman` on macOS or `apt-get install mame-tools` on Debian/Ubuntu). Please ensure all checks `[PASS]` to avoid unexpected issues during local compilation or testing.
