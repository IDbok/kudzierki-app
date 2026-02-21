# Kudzierki API

ASP.NET Core 10 Web API с MS SQL Server, JWT аутентификацией и layered архитектурой.

## Технологический стек

- **.NET 10** - ASP.NET Core Web API
- **MS SQL Server 2022** - база данных в Docker
- **EF Core 10** - ORM с миграциями
- **JWT** - аутентификация с access + refresh tokens
- **Serilog** - структурированное логирование
- **Swagger/OpenAPI** - документация API
- **xUnit + TestContainers** - integration tests

## Быстрый старт

### Требования

- Docker Desktop
- .NET 10 SDK (опционально, для локальной разработки)

### Запуск с Docker

```bash
docker compose up --build
```

Перед запуском задайте секреты Altegio в окружении (или в `.env`, который не коммитится):

```bash
ALTEGIO_BEARER_TOKEN=your-bearer-token
ALTEGIO_USER_TOKEN=your-user-token
ALTEGIO_COMPANY_ID=your-company-id
```

API будет доступно по адресу: **http://localhost:5000**
Swagger UI: **http://localhost:5000/swagger**

### Тестовые пользователи

- **Admin**
  - Email: `admin@local`
  - Password: `Admin123!`
  - Role: Admin

- **Owner**
  - Email: `owner@local`
  - Password: `Owner123!`
  - Role: Owner

## API Тестирование (curl)

### Health Check

```bash
curl http://localhost:5000/health
```

**Response:**
```json
{
  "status": "healthy",
  "database": "connected"
}
```

### Login

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@local",
    "password": "Admin123!"
  }'
```

**Response:**
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "CfDJ8..."
}
```

### Get Current User

```bash
curl http://localhost:5000/api/v1/auth/me \
  -H "Authorization: Bearer <your-access-token>"
```

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "admin@local",
  "role": "Admin"
}
```

### Logout

```bash
curl -X POST http://localhost:5000/api/v1/auth/logout \
  -H "Authorization: Bearer <your-access-token>"
```

**Response:**
```json
{
  "message": "Logged out successfully"
}
```

## Архитектура

### Структура решения

```
kudzierki-app/
├── src/
│   ├── Api/                    # REST API, controllers, middleware
│   ├── Infrastructure/         # EF Core, repositories, services
│   └── Shared/                # Result pattern, abstractions
├── tests/
│   └── Api.IntegrationTests/  # Integration tests
├── docker-compose.yml
└── README.md
```

### Ключевые компоненты

**Shared Layer**:
- `Result<T>` - паттерн для обработки успеха/ошибок
- `Error` - типизированные ошибки
- `ITimeProvider` - абстракция времени для тестирования

**Infrastructure Layer**:
- `ApplicationDbContext` - EF Core контекст
- `User`, `RefreshToken` - entity models
- `UserRepository`, `RefreshTokenRepository` - data access
- `AuthService` - логика аутентификации
- `TokenService` - генерация/валидация JWT

**Api Layer**:
- `AuthController` - login, logout, me endpoints
- `HealthController` - health checks
- `CorrelationIdMiddleware` - трекинг запросов
- Configuration extensions (Serilog, JWT, Swagger)

### JWT Authentication Flow

1. **Login**: пользователь отправляет email + password
2. **Validation**: система проверяет credentials
3. **Token Generation**:
   - Access token (15 минут) - содержит claims (id, email, role)
   - Refresh token (7 дней) - хранится в БД для ревокации
4. **Authorization**: access token в заголовке `Authorization: Bearer <token>`
5. **Logout**: ревокация refresh tokens в БД

### База данных

**Таблицы**:
- `Users` - id, email (unique), passwordHash, role (Admin/Owner), timestamps
- `RefreshTokens` - id, userId (FK), token (unique), expiresAt, createdAt, revokedAt

**Миграции**:
```bash
cd src/Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Api
```

**Применение миграций**:
Миграции применяются автоматически при запуске приложения через Polly retry policy.

## Локальная разработка (без Docker)

### 1. Запуск MS SQL Server

```bash
docker run -d -p 1433:1433 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='YourStrong@Password' \
  mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Применение миграций

```bash
cd src/Api
dotnet ef database update --project ../Infrastructure
```

### 3. Запуск API

```bash
dotnet run --project src/Api
```

## Тестирование

### Запуск integration tests

```bash
dotnet test
```

**Инфраструктура тестов**:
- `CustomWebApplicationFactory` - TestContainers с MS SQL
- `Respawn` - сброс БД между тестами
- `FluentAssertions` - readable assertions

### Тестовые сценарии

- ✅ Login с валидными/невалидными credentials
- ✅ GetMe с токеном/без токена
- ✅ Logout flow
- ✅ Валидация запросов (400 Bad Request)
- ✅ Health endpoint с correlation ID

## Конфигурация

### Ключевые настройки (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=mssql,1433;Database=kudzierki;..."
  },
  "Jwt": {
    "Secret": "change-in-production",
    "Issuer": "Kudzierki.Api",
    "Audience": "Kudzierki.Client"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

### Переменные окружения

Connection string можно переопределить:
```bash
ConnectionStrings__DefaultConnection="Server=...;Database=...;"
```

Секреты Altegio можно передавать так:

```bash
ALTEGIO_BEARER_TOKEN="..."
ALTEGIO_USER_TOKEN="..."
Altegio__CompanyId="1"

# Optional: background sync config
Altegio__TransactionsSync__Enabled="true"
Altegio__TransactionsSync__PollingIntervalMinutes="5"
Altegio__TransactionsSync__ShortFromDaysOffset="-1"
Altegio__TransactionsSync__ShortToDaysOffset="30"
Altegio__TransactionsSync__FullFromDaysOffset="-30"
Altegio__TransactionsSync__FullToDaysOffset="365"
Altegio__TransactionsSync__FullSyncIntervalHours="24"
```

Приложение валидирует `Altegio` настройки на старте и завершится с ошибкой, если токены не заданы.

## Логирование

### Serilog

- Структурированные логи в JSON формате
- Correlation ID для трекинга запросов
- Enrichers: Environment, MachineName
- Console sink для Development

### Пример лога

```
[2026-01-13 12:00:00.123 +00:00] [INF] [a1b2c3d4] Login attempt for email: admin@local
```

## Безопасность

- ✅ Пароли хешируются через `PasswordHasher<User>` (bcrypt-based)
- ✅ JWT подписываются HMAC SHA256
- ✅ Access tokens - short-lived (15 мин)
- ✅ Refresh tokens - long-lived (7 дней), хранятся в БД
- ✅ Logout ревоцирует refresh tokens
- ⚠️ **ВАЖНО**: измените JWT Secret в production!

## Доступ к базе данных

### Через sqlcmd

```bash
docker exec -it kudzierki-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Password' -C \
  -Q 'SELECT * FROM Users'
```

### Через Azure Data Studio

- Server: `localhost,1433`
- Username: `sa`
- Password: `YourStrong@Password`
- Trust Server Certificate: Yes

## Troubleshooting

### API не стартует

1. Проверьте логи: `docker compose logs api`
2. Убедитесь, что MSSQL healthy: `docker compose ps`
3. Проверьте connection string в логах

### Ошибки миграций

```bash
# Пересоздать БД
docker compose down -v
docker compose up --build
```

### Тесты падают

```bash
# Очистить контейнеры TestContainers
docker ps -a | grep testcontainers | awk '{print $1}' | xargs docker rm -f
```

## Следующие шаги

После успешного запуска базового каркаса:

1. ✅ Добавить доменные модули (Scheduling, Cash)
2. ✅ Реализовать business logic endpoints
3. ✅ Добавить unit tests для services
4. ✅ Настроить CI/CD pipeline
5. ✅ Добавить rate limiting и CORS

## Лицензия

MIT


## Altegio Transactions Sync and Snapshot

- `POST /api/v1/altegio/finance/transactions/sync` loads transactions from Altegio and stores them in local tables.
- `GET /api/v1/altegio/finance/transactions` reads from local snapshots, not directly from Altegio API.
- Response field `CreatedAt` is mapped from `FirstSeenAtUtc` (first observation time in this service).
- Raw payload history is stored in `AltegioTransactionRaws` with deduplication key `(ExternalId, PayloadHash)`.
- Current snapshot state is stored in `AltegioTransactionSnapshots` with unique key `ExternalId`.

### Background Sync

Configure under `Altegio:TransactionsSync` in `appsettings.json` (or env vars).
Default behavior: disabled (`Enabled: false`).

Example config:

```json
"Altegio": {
  "TransactionsSync": {
    "Enabled": true,
    "PollingIntervalMinutes": 5,
    "ShortFromDaysOffset": -1,
    "ShortToDaysOffset": 30,
    "FullFromDaysOffset": -30,
    "FullToDaysOffset": 365,
    "FullSyncIntervalHours": 24
  }
}
```
