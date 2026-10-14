# mm6mcp

**MindManager 6 のマインドマップを、ChatGPT などの MCP クライアントから参照するための実験的なプロジェクトです。**

MindManager 6 は古い Windows アプリケーションですが、COM Automation を介して、開いているマップの論理的なトピック構造を取得できます。mm6mcp はこれを MCP（Model Context Protocol）のツールとして公開し、AI が大きなマインドマップを必要な範囲から段階的に読めるようにした個人開発の実装例です。

主に Zenn 記事の補足・ソースコード公開を目的としています。**そのまま導入できる汎用製品ではなく、動作環境や認証基盤の構築を含めて各自で調整することを想定しています。**

## 特徴

- **レガシーアプリとの接続：** Windows 上の MindManager 6 を COM Automation で操作し、`Document.Xml` から論理アウトラインを取得します。
- **明示的な公開範囲：** Windows の GUI でユーザーが選択した、開いている文書のみを対象とします。MCP クライアントから任意のファイルや文書を指定して開くことはできません。
- **大規模マップの段階的な探索：** トピック数や深さを確認し、`max_depth` と `start_id` で必要なサブツリーだけを取得できます。
- **文脈を保つ検索：** トピック本文を検索し、ヒットしたトピックと Central Topic までの祖先をツリーで返します。
- **変更検出：** `observation_id`、`revision`、`subtree_revision` により、前回の観測から変わったサブツリーを判別できます。revision の比較は同じ `observation_id` 内でのみ有効です。

## 構成

```text
ChatGPT / MCP client
    │ MCP
    ▼
OpenAI Secure MCP Tunnel（tunnel-client）
    │
    ▼
Python MCP Server（Docker / Streamable HTTP）
    │ HTTP
    ▼
MindManager Manipulator（Windows / C# / .NET 10 / x86）
    │ COM Automation
    ▼
MindManager 6
```

**Docker と Manipulator を分離する理由：** MindManager 6 との COM 接続、32-bit プロセス、ユーザーによる文書選択 GUI は Windows 側に残す必要があります。一方、Python の MCP サーバー、認証処理、トンネルクライアントは Docker 内にまとめることで、依存関係や実行環境を Windows アプリから分離できます。両者はローカル HTTP API で接続します。Docker イメージには依存関係をインストールし、Python ソースは `run.cmd` により実行時に `/app` へ読み取り専用マウントします。

これはセキュリティ境界を自動的に保証する構成ではありません。Manipulator の HTTP API は信頼できるローカル環境向けであり、外部ネットワークへ直接公開しないでください。

## MCP ツール

| ツール | 概要 |
| --- | --- |
| `get_document_info` | 選択中の文書のトピック数、深さなどを取得 |
| `read_outline(start_id?, max_depth?)` | 指定したトピック以下の論理アウトラインを取得 |
| `find_topic(groups, start_id?, max_hits?)` | トピック本文を OR-of-AND 条件で検索し、祖先を含むツリーを返却 |

`find_topic` の `groups` は、例えば `[["state", "checker"], ["publish", "slug"]]` なら `(state AND checker) OR (publish AND slug)` を表します。各語は大文字・小文字を区別しない部分一致です。

これら **MCP に公開するツールは読み取り専用**です。ただし、Manipulator 内部の HTTP API には `update-topic-text` があり、GUI にも書き込みテスト用機能が残っています。**システム全体が読み取り専用という意味ではありません。**

## 動作・開発環境

このプロジェクトは、次の組み合わせを前提として作成しています。その他の環境・バージョンでの互換性は未検証です。

- Windows / MindManager 6（32-bit COM Automation）
- .NET SDK 10（Windows Forms、`win-x86`）
- Docker Desktop（Linux コンテナ）
- Python 3.12（コンテナ内）
- OpenAI `tunnel-client` v0.0.13（Linux amd64 runtime）
- OAuth 連携には Amazon Cognito を使用

## ビルドと起動の概要

この節は構築の入口となるメモです。OpenAI のトンネル利用権限、ChatGPT 側の MCP 接続設定、OAuth の設定は各自の環境に合わせて行ってください。

1. **Windows 側をビルド：** .NET 10 SDK を準備して次を実行します。

   ```powershell
   dotnet build MindManagerManipulator/MindManagerManipulator.csproj -c Release -r win-x86
   ```

2. **tunnel-client を配置：** [OpenAI tunnel-client v0.0.13 のリリース](https://github.com/openai/tunnel-client/releases/tag/v0.0.13) から `tunnel-client-runtime-v0.0.13-linux-amd64.zip` を取得・展開し、実行ファイルを `bin/tunnel-client-runtime` に配置します。**バイナリ自体は本リポジトリで配布しません。**
3. **環境変数を設定：** `.env.sample` を `.env` にコピーして、トンネル ID、API キー、必要な OAuth 設定を入力します。`run.cmd` は `%HOME%\mm6mcp.env` が存在する場合はそちらを優先し、なければリポジトリ直下の `.env` を参照します。
4. **MindManager と Manipulator を起動：** MindManager 6 でマップを開き、`gui.cmd` で Manipulator を起動します。GUI で公開対象文書を明示的に選択してください。
5. **Docker 側を起動：** リポジトリのルートディレクトリから以下のコマンドを実行します。

   ```bat
   build.cmd
   run.cmd
   ```

   実行には、トンネルおよび OAuth 側の事前設定も必要です。`run.cmd` はカレントディレクトリを読み取り専用で `/workspace` にマウントします。**機密ファイルを含むディレクトリでは実行しないでください。**

## 認証とセキュリティ上の注意

- OpenAI Secure MCP Tunnel を用いて、MCP サーバーを直接インターネットへ公開せずに接続する構成です。トンネルの利用設定や権限が別途必要です。
- OAuth の動作確認は **Amazon Cognito のみ**で行っています。他の OIDC/OAuth プロバイダを使う場合、`mcp/auth.py` 等の変更が必要になる可能性があります。
- `.env` には API キーなどの秘密情報が入ります。**Git にコミットしないでください。** `MCP_DUMP_TOKEN` は有効にしないでください。
- Manipulator の HTTP API には認証機構がなく、書き込み用エンドポイントも存在します。信頼できる端末内の接続に限定し、外部へ公開しないでください。
- MCP 経由で取得したマップ内容が AI クライアントに渡ることを理解したうえで、公開する文書を選択してください。

OAuth の設定には複数のサービスと URL・スコープの整合が必要です。本 README では詳細な設定手順を扱いません。公式ドキュメントを確認しつつ、必要に応じて ChatGPT などの AI と相談しながら組み立てることを推奨します。ただし、**秘密鍵や API キーを AI との会話に貼り付けないでください。**

## 制約・開発状況

個人利用から生まれたプロトタイプであり、配布用インストーラー、完成済みのセットアップ手順、他環境での動作保証、継続的なサポートは提供していません。MCP ツールの追加や既存仕様の変更が行われる可能性があります。

実装や設計に興味がある場合は、必要に応じて **fork して各自の環境に合わせて改造**してください。

## ライセンス

[MIT License](LICENSE)。なお、MindManager、OpenAI tunnel-client などの外部製品・配布物には、それぞれのライセンスと利用条件が適用されます。
