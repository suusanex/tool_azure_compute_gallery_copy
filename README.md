# Azure Compute Gallery クロスサブスクリプションコピーツール

[日本語版README](./README.ja.md)

## Overview

**Azure Compute Gallery (ACG) Cross-Subscription Copy Tool** is a Windows-based command-line utility that copies image definitions and versions from a source Azure Compute Gallery to a target gallery within the same Azure AD tenant but different subscription.

### Key Features

- ✅ **Bulk Copy**: Copy all image definitions and versions at once
- ✅ **Idempotent**: Safe to run multiple times - already copied resources are automatically skipped
- ✅ **Filtering**: Include/exclude specific images and versions using prefix or contains matching
- ✅ **Dry Run**: Preview all planned operations without making any changes
- ✅ **Detailed Logging**: Comprehensive logs with operation IDs and error codes for troubleshooting
- ✅ **Cross-Subscription**: Copy between different subscriptions in the same tenant
- ✅ **WebView2 Authentication**: Embedded interactive authentication (no browser dependency)

### Constraints

- ❌ Cross-tenant operations are not supported
- ❌ CMK (Customer-Managed Keys) encrypted images are automatically skipped
- ❌ Immutable attribute mismatches (OS type, generation, architecture) cause errors

---

## Prerequisites

### 1. Environment

- **Operating System**: Windows 10/11 or Windows Server 2019/2022 (Windows-only)
- **.NET Runtime**: .NET 10 or later
- **WebView2 Runtime**: Microsoft Edge WebView2 Runtime
  - Pre-installed on Windows 11
  - Required on Windows 10: Download from [WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- **Network**: HTTPS access (port 443) to Azure management APIs

### 2. Azure Requirements

- Source Azure Compute Gallery with image definitions and versions
- Target resource group (target gallery can be auto-created or pre-existing)
- RBAC: `Reader` on source subscription, `Contributor` on target subscription

### 3. Azure AD Application Registration

See [Quick Start Guide](./specs/001-acg-gallery-copy/quickstart.md#4-azure-ad-appplication-registration) for detailed setup instructions.

---

## Quick Start

### 1. Installation

Download the latest release from [Releases](../../releases) or build from source.

### 2. Configuration

Create `appsettings.json`:

```json
{
  "source": {
    "subscriptionId": "your-source-subscription-id",
    "resourceGroupName": "source-rg",
    "galleryName": "source-gallery"
  },
  "target": {
    "subscriptionId": "your-target-subscription-id",
    "resourceGroupName": "target-rg",
    "galleryName": "target-gallery"
  },
  "authentication": {
    "tenantId": "your-tenant-id",
    "clientId": "your-app-registration-client-id"
  },
  "logLevel": "Information"
}
```

### 3. Validate Configuration

```bash
acg-copy validate --config appsettings.json
```

### 4. Dry Run

Preview the copy plan without making changes:

```bash
acg-copy copy --config appsettings.json --dry-run
```

### 5. Execute Copy

```bash
acg-copy copy --config appsettings.json
```

---

## Commands

### `copy` - Copy images between galleries

```bash
acg-copy copy \
  --source-subscription <id> \
  --source-resource-group <name> \
  --source-gallery <name> \
  --target-subscription <id> \
  --target-resource-group <name> \
  --target-gallery <name> \
  --tenant-id <id> \
  [--include-images <patterns>] \
  [--exclude-images <patterns>] \
  [--include-versions <patterns>] \
  [--exclude-versions <patterns>] \
  [--match-mode prefix|contains] \
  [--dry-run]
```

**Options**:
- `--source-*`: Source gallery location
- `--target-*`: Target gallery location
- `--tenant-id`: Azure AD tenant ID
- `--include-images`: Comma-separated patterns to include (e.g., "ubuntu,windows")
- `--exclude-images`: Comma-separated patterns to exclude
- `--include-versions`: Comma-separated version patterns to include
- `--exclude-versions`: Comma-separated version patterns to exclude
- `--match-mode`: Pattern matching mode (`prefix` or `contains`, default: `prefix`)
- `--dry-run`: Preview without making changes

### `list galleries` - List all galleries

```bash
acg-copy list galleries \
  --subscription <id> \
  --resource-group <name> \
  --tenant-id <id>
```

### `list images` - List all image definitions

```bash
acg-copy list images \
  --subscription <id> \
  --resource-group <name> \
  --gallery <name> \
  --tenant-id <id>
```

### `list versions` - List all versions of an image

```bash
acg-copy list versions \
  --subscription <id> \
  --resource-group <name> \
  --gallery <name> \
  --image <name> \
  --tenant-id <id>
```

### `validate` - Validate configuration and connectivity

```bash
acg-copy validate [--config <path>]
```

---

## Examples

### Copy all images from source to target

```bash
acg-copy copy --config appsettings.json
```

### Copy with dry run

```bash
acg-copy copy --config appsettings.json --dry-run
```

### Copy only Ubuntu images (prefix match)

```bash
acg-copy copy \
  --config appsettings.json \
  --include-images "ubuntu" \
  --match-mode prefix
```

### Copy images but exclude test versions

```bash
acg-copy copy \
  --config appsettings.json \
  --exclude-versions "0.0,test"
```

### List galleries in a subscription

```bash
acg-copy list galleries \
  --subscription "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" \
  --resource-group "my-rg" \
  --tenant-id "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy"
```

---

## Troubleshooting

### Common Issues

1. **WebView2 Runtime not found**
   - Install from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/

2. **Authentication failed**
   - Verify Azure AD app registration is configured correctly
   - Check that public client flow is enabled
   - Ensure required permissions are granted

3. **Cross-tenant error**
   - This tool only supports same-tenant, cross-subscription copy
   - Verify both subscriptions belong to the same Azure AD tenant

4. **Permission denied (403)**
   - Ensure your account has `Contributor` role on target subscription
   - Check resource group and gallery permissions

For detailed troubleshooting, see [Quick Start Guide](./specs/001-acg-gallery-copy/quickstart.md#troubleshooting).

---

## Documentation

- [Quick Start Guide](./specs/001-acg-gallery-copy/quickstart.md) - Comprehensive setup and usage guide
- [Specification](./specs/001-acg-gallery-copy/spec.md) - Feature specification and requirements
- [Architecture & Design](./specs/001-acg-gallery-copy/plan.md) - Technical design and architecture
- [Data Model](./specs/001-acg-gallery-copy/data-model.md) - Data structures and domain models

---

## Exit Codes

- `0`: Success
- `1`: Dry run completed (no changes made)
- `2`: Configuration validation error
- `3`: Operation failed with recoverable error
- `4`: Unexpected error or unrecoverable failure

---

## Building from Source

### Requirements

- Visual Studio 2022 or later
- .NET 10 SDK
- MSBuild

### Build

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -products * -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\MSBuild.exe"

& $msbuild "src/AzureComputeGalleryCopy/AzureComputeGalleryCopy.csproj" /m /p:Configuration=Release
```

Output: `src/AzureComputeGalleryCopy/bin/Release/net10.0/AzureComputeGalleryCopy.exe`

---

## Testing

```bash
# Run all tests
dotnet test "tests/AzureComputeGalleryCopy.Tests/AzureComputeGalleryCopy.Tests.csproj"

# Run specific test
dotnet test "tests/AzureComputeGalleryCopy.Tests/AzureComputeGalleryCopy.Tests.csproj" -k "TestName"
```

---

## Support & Contributing

For issues, questions, or contributions, please open an issue or pull request on GitHub.

---

## License

MIT License - See LICENSE file for details

---

## Version

**Current Version**: 1.0.0  
**Release Date**: November 2025
**Branch**: `001-acg-gallery-copy`
