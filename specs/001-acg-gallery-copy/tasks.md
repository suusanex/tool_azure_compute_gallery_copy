# Tasks: Azure Compute Gallery クロスサブスクリプションコピー

Branch: `001-acg-gallery-copy`

---

## Phase 1: Setup

- [x] T001 Create solution and CLI project scaffolding in `src/AzureComputeGalleryCopy/AzureComputeGalleryCopy.csproj`
- [x] T002 Add central props for .NET 10 and nullable in `src/Directory.Build.props`
- [x] T003 Add NuGet package references (Azure.Identity, Azure.ResourceManager.Compute, Microsoft.Identity.Client.Extensions.Msal, Microsoft.Web.WebView2, Microsoft.Extensions.Configuration.*, Microsoft.Extensions.Logging.*, System.CommandLine) in `src/AzureComputeGalleryCopy/AzureComputeGalleryCopy.csproj`
- [x] T004 Seed example appsettings from contract in `src/AzureComputeGalleryCopy/appsettings.example.json`
- [x] T005 Initialize test projects (NUnit, Moq) in `tests/AzureComputeGalleryCopy.Tests/AzureComputeGalleryCopy.Tests.csproj`

## Phase 2: Foundational

- [x] T006 Implement `AzureContext` model per data model in `src/AzureComputeGalleryCopy/Models/AzureContext.cs`
- [x] T007 Implement `FilterCriteria` and `MatchMode` per data model in `src/AzureComputeGalleryCopy/Models/FilterCriteria.cs`
- [x] T008 Implement configuration models (`ToolConfiguration`, `AuthenticationConfiguration`) in `src/AzureComputeGalleryCopy/Models/ToolConfiguration.cs`
- [x] T009 Implement configuration loading (file/env/CLI precedence) in `src/AzureComputeGalleryCopy/Configuration/ConfigurationLoader.cs`
- [x] T010 Implement configuration validation (same tenant, name rules) in `src/AzureComputeGalleryCopy/Validation/ConfigurationValidator.cs`
- [x] T011 Implement WebView2 MSAL Interactive auth service in `src/AzureComputeGalleryCopy/Services/Authentication/WebView2Authenticator.cs`
- [x] T012 Setup logging builder + formatting (levels, stdout/stderr) in `src/AzureComputeGalleryCopy/Logging/LoggerFactoryBuilder.cs`
- [x] T013 Wire DI container and root command host in `src/AzureComputeGalleryCopy/Program.cs`
- [x] T014 [P] Create test utilities for configuration in `tests/AzureComputeGalleryCopy.Tests/TestHelpers/ConfigurationBuilderHelper.cs`

## Phase 3: User Story 1 (P1) — 全イメージの一括コピー

- Story Goal: ソースACGの全イメージ定義と全バージョンをターゲットに冪等にコピーする。
- Independent Test Criteria: 複数定義・複数バージョンで、未存在のみ作成・既存はスキップし、重複が発生しないこと。

Implementation tasks:
- [x] T015 [US1] Implement ARM clients factory (Compute) in `src/AzureComputeGalleryCopy/Services/Gallery/GalleryClientFactory.cs`
- [x] T016 [US1] Implement query service to enumerate definitions/versions in `src/AzureComputeGalleryCopy/Services/Gallery/GalleryQueryService.cs`
- [x] T017 [US1] Implement copy service (definitions + versions, idempotent) in `src/AzureComputeGalleryCopy/Services/Gallery/GalleryCopyService.cs`
- [x] T018 [US1] Implement immutable-attribute checks + region availability checks in `src/AzureComputeGalleryCopy/Services/Gallery/GalleryCopyService.cs`
- [x] T019 [US1] Implement `copy` command options and binding in `src/AzureComputeGalleryCopy/Cli/CopyCommand.cs`
- [x] T020 [P] [US1] Wire command handler to services/DI in `src/AzureComputeGalleryCopy/Program.cs`
- [x] T021 [P] [US1] Implement copy summary printer and exit codes in `src/AzureComputeGalleryCopy/Cli/Output/SummaryPrinter.cs`

Tests (unit):
- [x] T022 [P] [US1] Tests for `GalleryQueryService` enumeration in `tests/AzureComputeGalleryCopy.Tests/Services/Gallery/GalleryQueryServiceTests.cs`
- [x] T023 [P] [US1] Tests for `GalleryCopyService` idempotency and immutable mismatch in `tests/AzureComputeGalleryCopy.Tests/Services/Gallery/GalleryCopyServiceTests.cs`
- [x] T024 [P] [US1] Tests for `copy` command parsing and binding in `tests/AzureComputeGalleryCopy.Tests/Cli/CopyCommandTests.cs`

Notes:
- すべてのテストが成功し、ビルド警告のうち ConsoleLoggerOptions 非推奨は SimpleConsoleFormatter へ移行して解消。
- `CopyCommand` の `matchMode` は null セーフにし、ギャラリー名のログは ARM ID から抽出する実装へ統一。
- `MSB3277` 警告は、不要な `UseWindowsForms` および `Microsoft.Web.WebView2` 参照を削除することで根本的に解消（抑制なし）。

## Phase 4: User Story 2 (P2) — 条件付きコピー（フィルタ）

- Story Goal: include/exclude と matchMode により対象定義・バージョンを絞り込む。
- Independent Test Criteria: 指定パターンに一致する対象のみが作成/スキップ判定されること。

Implementation tasks:
- [x] T025 [US2] Implement filter matcher service (prefix/contains) in `src/AzureComputeGalleryCopy/Services/Filtering/FilterMatcher.cs`
- [x] T026 [P] [US2] Integrate filtering into copy pipeline in `src/AzureComputeGalleryCopy/Services/Gallery/GalleryCopyService.cs`
- [x] T027 [P] [US2] Map CLI filter options to criteria in `src/AzureComputeGalleryCopy/Cli/CopyCommand.cs`

Tests (unit):
- [x] T028 [P] [US2] Tests for filter matching combinations in `tests/AzureComputeGalleryCopy.Tests/Services/Filtering/FilterMatcherTests.cs`

## Phase 5: User Story 3 (P3) — ドライランで影響確認

- Story Goal: リソース変更なしで、予定操作（作成/スキップ）を出力するドライランを提供。
- Independent Test Criteria: ドライランの出力計画と通常実行結果が一致する（変更がない）。

Implementation tasks:
- [ ] T029 [US3] Add dry-run path to copy service producing `CopySummary` in `src/AzureComputeGalleryCopy/Services/Gallery/GalleryCopyService.cs`
- [ ] T030 [P] [US3] Implement dry-run output printer (plan view) in `src/AzureComputeGalleryCopy/Cli/Output/DryRunPrinter.cs`
- [ ] T031 [P] [US3] Handle `--dry-run` switch and routing in `src/AzureComputeGalleryCopy/Cli/CopyCommand.cs`

Tests (unit):
- [ ] T032 [P] [US3] Tests for dry-run plan generation parity in `tests/AzureComputeGalleryCopy.Tests/Services/Gallery/DryRunTests.cs`

## Final Phase: Polish & Cross-Cutting

- [ ] T033 Implement `list galleries` command in `src/AzureComputeGalleryCopy/Cli/List/ListGalleriesCommand.cs`
- [ ] T034 [P] Implement `list images` command in `src/AzureComputeGalleryCopy/Cli/List/ListImagesCommand.cs`
- [ ] T035 [P] Implement `list versions` command in `src/AzureComputeGalleryCopy/Cli/List/ListVersionsCommand.cs`
- [ ] T036 [P] Implement `validate` command (config + connectivity) in `src/AzureComputeGalleryCopy/Cli/Validate/ValidateCommand.cs`
- [ ] T037 [P] Add structured operation logger (IDs, codes) in `src/AzureComputeGalleryCopy/Logging/OperationLogger.cs`
- [ ] T038 [P] Update root `README.md` with prerequisites and examples in `README.md`
- [ ] T039 [P] Add version info/`--version` wiring in `src/AzureComputeGalleryCopy/Cli/VersionOption.cs`

---

## Dependencies

- Story order: US1 (P1) → US2 (P2), US1 (P1) → US3 (P3)
- Foundational (Phase 2) must precede all user stories.
- CLI utilities (`list`, `validate`) can follow US1 and do not block US2/US3.

## Parallel Execution Examples

- Within US1: Implement `SummaryPrinter` (T021) in parallel with DI wiring (T020).
- Within US2: Wire CLI options (T027) in parallel with service integration (T026).
- Within US3: Implement printer (T030) in parallel with CLI switch handling (T031).
- Cross-cutting: `list` and `validate` commands (T033–T036) can proceed in parallel after US1.

## Implementation Strategy

- MVP: Deliver US1 end-to-end (enumeration, copy, idempotency, summary, exit codes).
- Incremental: Add US2 filtering then US3 dry-run with printers.
- Keep operations sequential (no concurrency) to ensure idempotency and clarity.

## Independent Test Criteria per Story

- US1: 未存在のみ作成・既存スキップ、重複なし、サマリー/終了コード適正。
- US2: include/exclude と matchMode に一致する対象のみが処理対象。
- US3: ドライラン出力計画と通常実行結果に差分なし（同一条件）。

## Format Validation

- All tasks use required checklist format: `- [ ] T### [P] [US#] Description with file path`.
- Task IDs are sequential and paths are explicit.
