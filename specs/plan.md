# Implementation Plan: Azure Compute Gallery クロスサブスクリプションコピー

**Branch**: `001-acg-gallery-copy` | **Date**: 2025-11-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-acg-gallery-copy/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

同一テナント内の異なるサブスクリプション間で、Azure Compute Gallery（ACG）のイメージ定義および全バージョンをコピーするCLIツールを開発する。WebView2埋め込みInteractiveBrowserCredential認証（Windows専用、ブラウザキャッシュ非依存）を使用し、.NET 10で実装。フィルタ機能、ドライラン、冪等性を備え、ログによる詳細なトラブルシューティングを可能にする。

## Technical Context

**Language/Version**: C# / .NET 10 (最新版)
**Primary Dependencies**: 
- Azure.Identity (MSAL) - WebView2埋め込みInteractiveBrowserCredential認証用
- Azure.ResourceManager.Compute - ACG操作用
- Microsoft.Identity.Client.Extensions.Msal - トークンキャッシュ永続化用
- Microsoft.Web.WebView2 - WebView2埋め込み用
- Microsoft.Extensions.Configuration - 設定管理用
- Microsoft.Extensions.Logging - ログ出力用
- System.CommandLine - CLIインターフェース用

**Storage**: N/A（Azure ACG APIのみを使用、ローカルストレージ不要）
**Testing**: NUnit + Moq（Assert.That形式）
**Target Platform**: Windows 10/11のみ（WebView2ランタイム必須）
**Project Type**: single（CLIツール単体）
**Performance Goals**: 
- 100イメージバージョンのコピーを30分以内（Azure API制約に依存）
- メモリ使用量: <500MB（通常運用時）

**Constraints**: 
- 同一テナント内のサブスクリプション間コピーのみ
- Windows専用（WebView2埋め込みInteractiveBrowserCredential使用）
- WebView2ランタイムが必須（Windows 11は標準搭載、Windows 10は要インストール）
- Webブラウザの認証キャッシュに依存しない独自認証フロー
- 変更不可能な属性（OS種別/世代/アーキテクチャ）の不整合時は作成中断
- レート制限時は適切なエラーメッセージで終了（自動リトライなし）

**Scale/Scope**: 
- 数百のイメージ定義、数千のイメージバージョンに対応
- 同時処理なし（順次処理で冪等性を保証）

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Core Principles Compliance

1. **ドキュメント言語**: ✅ 適合
   - ドキュメント: 日本語で作成
   - ソースコードコメント: 日本語
   - ソースコード・ログ: 英語

2. **エラーハンドリング**: ✅ 適合
   - フォールバック禁止、エラー・例外を返す設計
   - Exception.ToString()をトレースログ出力
   - 仕様FR-011に明記済み

3. **テスト戦略**: ✅ 適合
   - NUnit + Moq使用
   - Assert.That形式
   - TDD: 要件定義済み、テストファースト方針

4. **Spec-Kit プロセス**: ✅ 適合
   - Specify完了（spec.md）
   - 現在Plan実行中
   - Tasks, Implementは後続フェーズ

5. **ビルドツール**: ✅ 適合
   - MSBuild使用（vswhere経由）
   - dotnet build非使用方針

### 違反なし

このプロジェクトはconstitutionに定義された全基準を満たしており、例外承認は不要です。

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── AzureComputeGalleryCopy/           # メインCLIプロジェクト
│   ├── Models/                        # データモデル（設定、コンテキスト、フィルタ等）
│   ├── Services/                      # ビジネスロジック（コピーサービス、認証サービス等）
│   │   ├── Authentication/            # MSAL認証関連
│   │   ├── Gallery/                   # ACG操作関連
│   │   └── Filtering/                 # フィルタロジック
│   ├── Cli/                          # CLIインターフェース（System.CommandLine）
│   ├── Logging/                      # ログ設定・フォーマッタ
│   └── Program.cs                    # エントリーポイント

tests/
├── AzureComputeGalleryCopy.Tests/    # ユニットテスト
│   ├── Models/
│   ├── Services/
│   └── Cli/
└── AzureComputeGalleryCopy.IntegrationTests/  # 統合テスト（実際のAzure環境）
    ├── Authentication/
    └── Gallery/
```

**Structure Decision**: Single project構成を選択。CLIツールとして単一実行ファイルを提供するため、backend/frontendの分離は不要。テストは単体テストと統合テスト（実Azure環境）に分離し、モック可能な部分と実環境検証を明確化。

## Complexity Tracking

該当なし（Constitution違反なし）

---

## Phase 0 & Phase 1: Completion Summary

### Phase 0: Research & Technology Selection ✅ Complete

**成果物**:
- `research.md`: 技術選定とベストプラクティスの調査完了
  - 認証: WebView2埋め込みInteractiveBrowserCredential（Windows専用、ブラウザキャッシュ非依存）
  - Azure SDK: Azure.ResourceManager.Compute
  - CLI Framework: System.CommandLine
  - ログ: Microsoft.Extensions.Logging

**重要な決定事項**:
- WebView2埋め込み認証を採用（Windowsユーザー向け、Webブラウザ依存なし）
- Windows専用ツールとして開発（クロスプラットフォーム対応は将来検討）
- .NET 10、最新ライブラリの使用を確認

### Phase 1: Design & Contracts ✅ Complete

**成果物**:
1. `data-model.md`: エンティティ、バリデーションルール、状態遷移の定義完了
2. `contracts/`:
   - `cli-interface.md`: CLIコマンド構造、オプション、出力形式の定義
   - `config-schema.json`: JSON Schema形式の設定ファイル仕様
   - `appsettings.example.json`: 設定ファイルのサンプル
3. `quickstart.md`: 使用者向けドキュメント（セットアップ、使い方、トラブルシューティング）完成
4. `.github/agents/copilot-instructions.md`: Copilot agent contextの更新完了

**Constitution Check再評価**: ✅ 全項目適合

### 次のステップ: Phase 2 - Tasks

次のコマンドで Phase 2（タスク分解）を実行してください:

```bash
# タスク分解コマンド（未実装の場合は手動でtasks.mdを作成）
# /speckit.tasks
```

Phase 2では以下を行います:
- 実装タスクへの分解（小さい、レビュー可能な単位）
- タスク間の依存関係の整理
- 実行順序の明確化

