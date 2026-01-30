# Agent Instructions Pack — dxs-consigliere

1) Коротко: что это за библиотека
- .NET 9.0 сервис-индексатор для BSV/STAS, хранит состояние UTXO и историю в RavenDB; точка входа — `src/Dxs.Consigliere/Program.cs`.
- Используется внутренними системами DXS для индексации STAS токенов и Back-to-Genesis проверки (см. `README.md`).
- Дает HTTP API и SignalR WebSocket для запросов балансов/UTXO/истории/tx и оповещений.
- Интегрируется с BSV node (RPC+ZMQ), JungleBus (GorillaPool), Bitails и WhatsOnChain.
- НЕ является TypeScript библиотекой: в репозитории нет `package.json`/`tsconfig*`, зато есть `.sln` и `.csproj` (см. `Dxs.Consigliere.sln`, `src/Dxs.*/*.csproj`).
- НЕ является кошельком/нодой/майнером/клиентской UI; это серверный индексатор и API.

2) Быстрый старт (локальный)
- Prerequisites:
  - .NET SDK 9.x (по `TargetFramework` = `net9.0` в `src/Dxs.*/*.csproj`).
  - RavenDB (URL и DB name в `src/Dxs.Consigliere/appsettings.json`).
  - BSV node с RPC + ZMQ (см. `BsvNodeApi` и `ZmqClient` в `src/Dxs.Consigliere/appsettings.json`).
  - Опционально JungleBus subscription id (см. `JungleBus` в `src/Dxs.Consigliere/appsettings.json`).
- Install:
  - `dotnet restore Dxs.Consigliere.sln`
  - Результат в этой среде: restore успешен, но есть warning `NU1903` по `System.Text.Json 8.0.4`.
- Build:
  - `dotnet build Dxs.Consigliere.sln -c Release`
  - В этой среде команда таймаутилась (60s). См. секцию 12.
- Test:
  - `dotnet test Dxs.Consigliere.sln -c Release`
  - В этой среде падало с `MSB1025`/`SocketException (13): Permission denied`. См. секцию 12.
- Lint/format:
  - Не найдено (нет `*.editorconfig`, `Directory.Build.*`, `dotnet format` скриптов).
- Sanity check (HTTP API):
  ```bash
  curl -X POST http://localhost:5000/api/address/balance \
    -H 'Content-Type: application/json' \
    -d '{"addresses":["1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"],"tokenIds":[]}'
  ```
  Примечание: порт зависит от hosting; в Docker выставлен `ASPNETCORE_HTTP_PORTS=5000` (см. `Dockerfile`).

3) Карта репозитория (Repo Map)
| Path | Responsibility | Notes |
|---|---|---|
| `Dxs.Consigliere.sln` | Solution | 4 проекта: Dxs.Consigliere, Dxs.Bsv, Dxs.Common, Dxs.Infrastructure |
| `src/Dxs.Consigliere/Program.cs` | Entry point | Host builder, Serilog bootstrap, запускает `Startup` |
| `src/Dxs.Consigliere/Startup.cs` | DI + middleware | Регистрация RavenDB, RPC/ZMQ, фоновых задач, SignalR, Swagger |
| `src/Dxs.Consigliere/Controllers/*` | HTTP API | `AddressController`, `TransactionController`, `AdminController` |
| `src/Dxs.Consigliere/WebSockets/*` | SignalR hub | `WalletHub` + интерфейсы событий |
| `src/Dxs.Consigliere/BackgroundTasks/*` | Фоновые задачи | Инициализация, обработка блоков, JungleBus, мемпул |
| `src/Dxs.Consigliere/Data/*` | RavenDB | конфиг, документ-стор, индексы, модели, запросы |
| `src/Dxs.Consigliere/Dto/*` | API DTO | Request/Response для REST/SignalR |
| `src/Dxs.Consigliere/Services/*` | Бизнес‑логика | UTXO, broadcast, доступ к данным, фильтры |
| `src/Dxs.Bsv/*` | BSV primitives | RPC, ZMQ, транзакции, скрипты, токены |
| `src/Dxs.Infrastructure/*` | Внешние клиенты | Bitails, WhatsOnChain, JungleBus, WebSocket |
| `src/Dxs.Common/*` | Общие утилиты | BackgroundTasks, Cache, Dataflow, Exceptions |
| `src/Dxs.Consigliere/appsettings.json` | Конфиг по умолчанию | Секции Serilog, RavenDb, BsvNodeApi, ZmqClient и др. |
| `Dockerfile` | Docker build | SDK 9.0 build + publish, runtime ASP.NET 9.0 |
| `README.md` | Описание | Обзор, запуск, пример конфигурации |

4) Публичный API (если есть)
Основной публичный интерфейс — HTTP API + SignalR. Экспортов вида `package.json` нет.

HTTP API (реализация в `src/Dxs.Consigliere/Controllers/*.cs`):
- `POST /api/address/balance`
  - Вход: `BalanceRequest` (`Addresses[]`, `TokenIds[]`) из `src/Dxs.Consigliere/Dto/Requests/BalanceRequest.cs`.
  - Выход: `BalanceDto[]` из `src/Dxs.Consigliere/Dto/BalanceDto.cs`.
  - Побочные эффекты: нет (чтение RavenDB).
  - Ошибки: 400 при invalid input.
- `POST /api/address/batch/utxo-set`
  - Вход: `GetUtxoSetBatchRequest` (`TokenIds[]`, `Addresses[]`).
  - Выход: `GetUtxoSetResponse` (UTXO list).
  - Побочные эффекты: нет.
- `POST /api/address/utxo-set`
  - Вход: `GetUtxoSetRequest` (`TokenId?`, `Address?`, `Satoshis?`).
  - Выход: `GetUtxoSetResponse`.
  - Ошибки: 400 если не указан ни `Address`, ни `TokenId`.
- `POST /api/address/history`
  - Вход: `GetAddressHistoryRequest` (`Address`, `TokenIds[]`, `Skip`, `Take`, `Desc`, `SkipZeroBalance`).
  - Выход: `AddressHistoryResponse`.
- `GET /api/tx/get/{id}`
  - Вход: `id` (txid, 64 hex).
  - Выход: hex string или 404.
  - Ошибки: 400 если txid malformed; 500 если запись без `Hex`.
- `GET /api/tx/batch/get?ids=...`
  - Вход: query `ids` (макс 1000).
  - Выход: `Dictionary<string,string>` txid → hex (пустая строка если не найдено).
  - Ошибки: 400 при malformed id или превышении лимита.
- `GET /api/tx/by-height/get?blockHeight=...&skip=...`
  - Вход: `blockHeight`, `skip`.
  - Выход: `GetTransactionsByBlockResponse` (page size = 500).
  - Ошибки: 400 если `blockHeight==0`.
- `POST /api/tx/broadcast/{raw}`
  - Вход: raw tx hex в path.
  - Выход: `Broadcast` (см. `src/Dxs.Consigliere/Data/Models/Broadcast.cs`).
  - Побочные эффекты: broadcast через node RPC + запись в RavenDB.
- `GET /api/tx/stas/validate/{id}`
  - Вход: txid (64 hex).
  - Выход: `ValidateStasResponse`.
  - Ошибки: 400 malformed; 404 если tx не найден; 418 если tx не STAS.
- `POST /api/admin/manage/address`
  - Вход: `WatchAddressRequest` (`Address`, `Name`).
  - Побочные эффекты: запись `WatchingAddress` в RavenDB; постановка адреса в `ITransactionFilter`.
- `POST /api/admin/manage/stas-token`
  - Вход: `WatchStasTokenRequest` (`TokenId`, `Symbol`).
  - Побочные эффекты: запись `WatchingToken` в RavenDB; постановка токена в `ITransactionFilter`.
- `GET /api/admin/blockchain/sync-status`
  - Выход: `SyncStatusResponse` (`Height`, `IsSynced`).
  - Побочные эффекты: RPC к ноде + чтение RavenDB.

SignalR Hub (реализация в `src/Dxs.Consigliere/WebSockets/WalletHub.cs`):
- Hub route: `/ws/consigliere` (см. `WalletHub.Route`).
- Методы сервера (клиент вызывает):
  - `SubscribeToTransactionStream(address)` / `UnsubscribeToTransactionStream(address)`.
  - `GetBalance`, `GetHistory`, `GetUtxoSet`, `GetTransactions`, `Broadcast`.
- Методы клиента (сервер вызывает):
  - `OnTransactionFound(hex)`, `OnTransactionDeleted(txid)`, `OnBalanceChanged(BalanceDto)`.

5) Архитектура и поток данных
Ключевые сущности:
- `MetaTransaction`, `MetaOutput`, `TransactionHexData` — проиндексированные tx и UTXO (`src/Dxs.Consigliere/Data/Models/Transactions/*`).
- `BlockProcessContext` — очередь и состояние обработки блока (`src/Dxs.Consigliere/Data/Models/BlockProcessContext.cs`).
- RavenDB индексы: `AddressHistoryIndex`, `StasUtxoSetIndex`, `FoundMissingRootsIndex` (см. `src/Dxs.Consigliere/Data/Indexes/*`).
- Внешние источники: BSV node (RPC+ZMQ), JungleBus, Bitails, WhatsOnChain.

Happy‑path потоки:
- Запуск сервиса и инициализация:
  - `Program.cs` создает host и запускает `Startup`.
  - `Startup.InitializeDatabase` вызывает Raven migrations и создает индексы через `RavenDbDocumentStore`.
  - `AppInitBackgroundTask` стартует ZMQ, сканирует mempool (опционально) и ставит в очередь последние блоки.
- Обработка блоков:
  - ZMQ сообщает новые блоки → `IBlockMessageBus` → `BlockProcessBackgroundTask`.
  - `BlockProcessBackgroundTask` создает/обновляет `BlockProcessContext`, читает заголовок блока через `IRpcClient`.
  - Выбирается источник данных: `JungleBusBlockchainDataProvider` (если включен) или `NodeBlockchainDataProvider`.
  - Для каждого tx создается `TxMessage` → в pipeline фильтров/обработчиков → запись в RavenDB.
- Поток транзакций в реальном времени:
  - `IZmqClient` читает mempool/blocks → `ITxMessageBus` → `IFilteredTransactionMessageBus`.
  - `ConnectionManager` подписан на filtered stream и пушит `OnTransactionFound`/`OnBalanceChanged` в SignalR группы по адресу.
- Запросы API:
  - Контроллеры → сервисы (`IUtxoManager`, `IAddressHistoryService`, `IBroadcastService`) → RavenDB запросы/индексы.

6) Инварианты, предположения, ограничения
- Сеть должна быть `Mainnet` или `Testnet`, иначе `NetworkProvider` бросит исключение (`src/Dxs.Consigliere/Services/Impl/NetworkProvider.cs`).
- TxId валидируется как 64 hex (см. `TransactionController`).
- Лимиты:
  - `GET /api/tx/batch/get`: максимум 1000 ids.
  - `WalletHub.GetTransactions`: максимум 100 ids.
  - `GET /api/tx/by-height/get`: page size = 500.
  - `GetUtxoSetBatch`: `Take(1000)`.
- `BackgroundTasksConfig`: если `EnabledTasks != null`, запускаются только перечисленные; иначе исключаются `DisabledTasks` (см. `src/Dxs.Common/BackgroundTasks/PeriodicTask.cs`).
- JungleBus используется только если `JungleBus.Enabled=true` и задан `BlockSubscriptionId`/`MempoolSubscriptionId`.
- WhatsOnChain client всегда использует сеть `main` (жестко в коде `src/Dxs.Infrastructure/WoC/WhatsOnChainRestApiClient.cs`).
- Bitails ограничивает частоту запросов (10 req/s) и max history count 5000 (см. `BitailsRestApiClient`).

7) Конфигурация и окружение
Источник конфигурации:
- `CreateDefaultBuilder` + `appsettings.json` + `appsettings.{Environment}.json` (см. `src/Dxs.Consigliere/Program.cs`).
- `ASPNETCORE_ENVIRONMENT` (или `DOTNET_ENVIRONMENT`) влияет на выбор appsettings.{env}.json.

Секции конфигурации:
- `Serilog` — логирование (см. `src/Dxs.Consigliere/appsettings.json`).
- `BackgroundTasks` — включение/выключение задач.
- `RavenDb` — `Urls`, `DbName`, `ClientCertificate`, `CertificatePassword`.
- `Network` — `Mainnet`/`Testnet`.
- `ScanMempoolOnStart` (bool), `BlockCountToScanOnStart` (int).
- `ZmqClient` — адреса ZMQ (`RawTx2Address`, `RemovedFromMempoolBlockAddress`, `DiscardedFromMempoolAddress`, `HashBlock2Address`).
- `BsvNodeApi` — RPC (`BaseUrl`, `User`, `Password`).
- `TransactionFilter` — `Addresses[]`, `Tokens[]` (стартовая фильтрация).
- `JungleBus` — `Enabled`, `BlockSubscriptionId`, `MempoolSubscriptionId`.

8) Ошибки и обработка ошибок
- Ошибки REST выражаются HTTP статусами в контроллерах (`BadRequest`, `NotFound`, `Conflict`, `InternalError`).
- Есть собственные исключения: `DetailedHttpRequestException`, `NotEnoughMoneyException` (см. `src/Dxs.Common/Exceptions/*`).
- RPC ошибки выбрасываются через `EnsureSuccess()` (см. `src/Dxs.Bsv/Rpc/Models/RpcResponseExtensions.cs`).
- Логирование через Serilog + Microsoft ILogger; минимальный уровень в `appsettings.json`.
- Добавление новых ошибок: следовать паттерну `Exception` + расширения `EnsureSuccess`, а в контроллерах конвертировать в корректный HTTP статус.

9) Тестирование
- Тестовый фреймворк: НЕ НАЙДЕНО (нет `tests/` и нет упоминаний xUnit/NUnit/MSTest; проверено `rg -n "xunit|nunit|mstest"`).
- Команда: `dotnet test Dxs.Consigliere.sln -c Release`.
- В этой среде команда падает из‑за `MSB1025`/`SocketException (13): Permission denied`.
- Добавление теста: создать отдельный test‑проект и добавить в `Dxs.Consigliere.sln` (подтверждений наличия тестовой инфраструктуры нет).

10) Стандарты кодстайла и правила PR
- Форматирование/линтинг: явных правил/конфигураций не обнаружено.
- Commit conventions: НЕ НАЙДЕНО (нет CONTRIBUTING/PR гайдлайнов).
- Модульные соглашения: проекты разделены по слоям (`Dxs.Common`, `Dxs.Bsv`, `Dxs.Infrastructure`, `Dxs.Consigliere`).
- Как добавить новый модуль:
  - Выберите правильный проект по ответственности (Common/Bsv/Infrastructure/Consigliere).
  - Подключите через `ProjectReference` в соответствующем `.csproj`.
  - Зарегистрируйте в DI в `src/Dxs.Consigliere/Startup.cs`.

11) Типичные задачи для агентов (Runbook)
“Добавить новую фичу/модуль”
- Определите слой: `Dxs.Common` (общие утилиты), `Dxs.Bsv` (BSV логика), `Dxs.Infrastructure` (внешние API), `Dxs.Consigliere` (API/оркестрация).
- Создайте класс/namespace внутри проекта.
- Если нужен DI, добавьте регистрацию в `src/Dxs.Consigliere/Startup.cs`.
- При необходимости добавьте конфиг в `src/Dxs.Consigliere/appsettings.json` и соответствующий Config class.
- Обновите DTO/контроллер/SignalR hub при расширении API.
- Прогоните `dotnet build Dxs.Consigliere.sln -c Release`.

“Поменять публичный API без поломок”
- Идентифицируйте endpoint/Hub метод (см. `src/Dxs.Consigliere/Controllers/*`, `src/Dxs.Consigliere/WebSockets/*`).
- Добавьте новую версию поля/DTO, не удаляя существующие.
- Оставьте старые маршруты/контракты; добавьте новые (если нужно).
- Обновите Swagger/DTO аннотации при необходимости.
- Проверьте сериализацию (SignalR JSON/MessagePack) и обратную совместимость.

“Отладить падение тестов”
- Запустите `dotnet test Dxs.Consigliere.sln -c Release`.
- Если падает MSBuild с `SocketException (13)`, попробуйте `dotnet test ... /m:1` (однопоточный) или проверить ограничения окружения.
- Проверяйте логи MSBuild и Serilog; при необходимости увеличить timeout.
- Убедитесь, что RavenDB и BSV node доступны, если тесты их используют (UNKNOWN — тестов нет).

“Собрать релиз/пакет”
- Publishing flow not found для NuGet/пакетов.
- Для контейнера: используйте `Dockerfile` (build + publish `Dxs.Consigliere`).
- Проверено: `Dockerfile` собирает и публикует `Dxs.Consigliere.dll` на ASP.NET runtime.

“Добавить новую зависимость безопасно”
- Добавьте `PackageReference` в нужный `.csproj` (например `src/Dxs.Consigliere/Dxs.Consigliere.csproj`).
- Запустите `dotnet restore Dxs.Consigliere.sln`.
- Убедитесь, что не нарушены правила DI и нет конфликтов версий.
- При необходимости обновите `Dockerfile` (если зависит от SDK/runtime).
- Зафиксируйте версии пакетов явно (по текущей практике).

12) Known issues / TODO (только факты)
- TODO/FIXME из кода:
  - `src/Dxs.Bsv/BinaryHelpers.cs` — `// TODO Refactor`.
  - `src/Dxs.Bsv/Tokens/Stas/StasProtocolTransactionFactory.cs` — `//TODO` и комментарии об ограничениях.
  - `src/Dxs.Bsv/Address.cs` — `// TODO? verify checksum`.
  - `src/Dxs.Bsv/Protocol/BitcoinStreamReader.cs` — `//TODO [Oleg] handle stream end`.
  - `src/Dxs.Bsv/Script/Read/LockingScriptReader.cs` — `//TODO [Oleg] use slices`.
- Build/Test проблемы в этой среде:
  - `dotnet build Dxs.Consigliere.sln -c Release` — таймаут (60s).
  - `dotnet test Dxs.Consigliere.sln -c Release` — `MSB1025` + `SocketException (13): Permission denied`.
- Restore warning:
  - `NU1903` для `System.Text.Json 8.0.4` при `dotnet restore`.
- Документация:
  - Нет отдельного `docs/` или CONTRIBUTING; подробные инструкции только в `README.md`.
