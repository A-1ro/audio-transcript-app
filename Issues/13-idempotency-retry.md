# Issue: 冪等性・再実行対応

## 目的
再実行時に二重処理を防ぐ。

## 受け入れ条件
- FileId 単位で結果の存在チェックができる
- Orchestrator再実行でも重複登録しない

## 参考
- docs/2.functions-transcription-design.md
