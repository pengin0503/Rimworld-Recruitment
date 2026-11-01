# Colonist Recruitment（入植者募集）

RimWorld 1.7 用の MOD です。失った入植者を、費用と日数をかけた「募集」で補充できます。前提MODとして Harmony が必要です。

## 募集方式

| 方式 | 費用 | 期間 | 必要研究 | 開始方法 | 到着 |
|---|---|---|---|---|---|
| キャラバンで募集 | 50 | 6〜10日 | なし | 入植者を選択して、来訪中の商人を右クリック | 徒歩 |
| 拠点で募集を依頼（使者） | 100 | 依頼後4〜7日 | なし | シルバーを持たせたキャラバンで、敵対していない拠点を右クリック | 徒歩 |
| 無線放送 | 250 | 3〜5日 | マイクロエレクトロニクス 基礎 | 通信機を右クリック →「入植者募集を呼びかける」で開く画面から選ぶ | 徒歩 |
| 軌道通信 | 600 | 1〜3日 | AIコンピュータコア | 同上（宇宙でも可） | 輸送ポッド |

- 応募者は0〜3人です（初期値は各25%）。0人のときは費用が戻りません。
- 応募者はワールドの派閥から来ます。種族・ゼノタイプ・思想はその派閥に従います。帝国の出身者は、入植者の誰かがナイト以上（帝国のキャラバンと交易できる爵位）のときだけ来ます。
- 初期設定では、到着した応募者を画面で比べて採用・不採用を選びます（24時間以内）。MOD 設定で自動加入にもできます。
- 費用が200シルバー以上の方式（初期値では無線放送と軌道通信）は、開始前に確認画面が出ます。額は MOD 設定で変えられ、確認をオフにもできます。
- 使者は普通のキャラバンとして拠点まで旅をします。支払いはキャラバンのシルバーからで、シルバーが足りないまま依頼先へ出発しようとすると警告が出ます。同じ拠点に再び依頼できるのは15日後です（設定で変更可）。
- 使者の交渉役の社交スキルが高いほど、応募者が0人になりにくくなります。
- 費用・期間・人数の確率・使者の社交補正・同時募集数などは、MOD 設定で変更できます。

## 補足

- 使者の依頼では、募集枠に空きがある拠点が候補になります。候補が複数ある場合は、出発指示時・訪問中の依頼時に到着先を選択します。到着先が消失したり枠が埋まった場合、支払い前に拒否します。
- 応募者画面は、説明文の長さに応じて行の高さを変えます。
- 採否を確定した後に移送が失敗しても、未処理の応募者と選択内容を保持し、250 tickごとに再試行します。募集タブから手動で再試行することもできます。確定済みの採否は再試行中に変更できません。
- 募集タブの履歴に、支払額・出身・予定/実際の到着先・人数・結果を最新100件まで保存します。履歴はポーンを保持しません。旧セーブの完了済み募集は遡って復元できず、進行中の旧募集は残っている情報から記録します。

## 注意

- 判断待ちの応募者は、この MOD のデータとしてセーブされます。その状態で MOD を外すと、その応募者は失われます。
- Harmony で変更しているのは、キャラバン編成画面の出発時（シルバーの警告）と、通信機の通信先一覧（「入植者募集を呼びかける」の追加）の2か所だけです。

## English

Colonist Recruitment adds ways to recruit replacement colonists in RimWorld 1.7. Harmony is required.

| Method | Cost (silver) | Time | Requirement | Start |
|---|---:|---|---|---|
| Caravan | 50 | 6–10 days | None | Select a colonist and right-click a visiting trader |
| Settlement envoy | 100 | 4–7 days after the request | None | Send a caravan with silver to a non-hostile settlement |
| Radio broadcast | 250 | 3–5 days | Microelectronics Basics | Use the comms console |
| Orbital communication | 600 | 1–3 days | AI Computer Core | Use the comms console; arrivals use drop pods |

These are defaults and can be adjusted in mod settings. Each request produces 0–3 applicants; a request with no applicants is not refunded. Applicants come from world factions, with race, xenotype and ideology determined by their faction. Empire applicants require a colonist with at least the Knight title. By default, accept or reject applicants within 24 hours; automatic joining is configurable. Envoy social skill reduces the chance of zero applicants. Costs, timing, probabilities, simultaneous requests and other options can be changed in settings.

The recruitment tab keeps a history of up to 100 requests. Removing the mod while applicants are awaiting a decision loses those applicants; back up your save first. Download an available package from [Releases](https://github.com/pengin0503/Rimworld-Recruitment/releases), put the mod folder in RimWorld's `Mods` folder, enable it after Harmony and restart the game.
