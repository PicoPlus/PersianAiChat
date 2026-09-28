# دستیار هوش مصنوعی — Persian AI Chatbot

A production-quality, mobile-first Persian RTL AI chatbot built with .NET 8.  
Authentication via HubSpot CRM + SMS.ir OTP. AI powered by GapGPT (OpenAI-compatible).

---

## Features

- 📱 **Persian RTL mobile-first UI** — fully Persian interface with dark/light/system theme
- 🔐 **HubSpot-gated authentication** — only contacts in your HubSpot CRM can log in
- 📲 **SMS.ir OTP** — cryptographically secure 6-digit OTP sent via SMS.ir
- 🤖 **GapGPT integration** — uses `gpt-6-luna` with web search tool
- 🌐 **Web citations** — renders source cards from URL annotations
- 🪙 **Token usage tracking** — cumulative token counter with detailed breakdown
- 💬 **Conversation history** — persistent conversations with rename/delete
- 🔒 **Security-first** — HttpOnly cookies, CSRF protection, rate limiting, no secrets in frontend
- 🐳 **Docker-ready** — multi-stage Dockerfile with non-root user
- ☁️ **Cloudflare-compatible** — designed to run behind Cloudflare WAF/CDN
- 📦 **PWA** — installable on Android/iOS with offline shell support

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- HubSpot account with CRM contacts
- SMS.ir account with a verified template
- GapGPT API key

---

## Project Structure

```
PersianAiChat/
├── PersianAiChat.sln
├── src/
│   ├── PersianAiChat.Api/           # ASP.NET Core 8 Web API + static frontend
│   │   ├── Controllers/             # Thin API controllers
│   │   ├── wwwroot/                 # HTML, CSS, JS, PWA assets
│   │   └── Program.cs               # DI, middleware, auth configuration
│   ├── PersianAiChat.Application/   # Business logic, interfaces, DTOs
│   │   ├── Abstractions/            # Service interfaces + DTOs
│   │   ├── Options/                 # Strongly typed configuration
│   │   ├── Services/                # OtpService, ChatService, etc.
│   │   └── Validators/              # PhoneNormalizer
│   ├── PersianAiChat.Domain/        # Domain entities + enums
│   └── PersianAiChat.Infrastructure/
│       ├── Persistence/             # EF Core DbContext + migrations
│       ├── HubSpot/                 # HubSpotService
│       ├── Sms/                     # SmsIrService
│       ├── GapGpt/                  # GapGptService + response parser
│       └── Authentication/          # JwtTokenService
└── tests/
    ├── PersianAiChat.UnitTests/     # Unit tests
    ├── PersianAiChat.IntegrationTests/
    └── Fixtures/gapgpt-response.json
```

---

## Quick Start

### 1. Configure secrets (never commit these)

```powershell
# Windows PowerShell / Linux bash
cd PersianAiChat/src/PersianAiChat.Api

dotnet user-secrets init
dotnet user-secrets set "GapGpt:ApiKey"           "your-gapgpt-api-key"
dotnet user-secrets set "HubSpot:AccessToken"     "your-hubspot-access-token"
dotnet user-secrets set "SmsIr:ApiKey"            "your-smsir-api-key"
dotnet user-secrets set "SmsIr:LineNumber"        "your-smsir-line-number"
dotnet user-secrets set "SmsIr:TemplateId"        "your-smsir-template-id"
dotnet user-secrets set "Security:JwtSecretKey"   "your-256-bit-random-secret"
```

> **SMS.ir template**: Your template must include a parameter named `CODE`.  
> Example template text: `کد تأیید شما: {CODE}`

### 2. Restore packages

```powershell
cd PersianAiChat
dotnet restore
```

### 3. Apply EF Core migrations

```powershell
cd src/PersianAiChat.Api
dotnet ef database update
```

If `dotnet ef` is not installed:
```powershell
dotnet tool install --global dotnet-ef
```

### 4. Run the application

```powershell
cd src/PersianAiChat.Api
dotnet run
```

Open: http://localhost:5000

### 5. Run tests

```powershell
cd PersianAiChat
dotnet test
```

---

## Environment Variables (Production)

All configuration uses the `__` double-underscore separator for nested keys:

| Variable | Description |
|---|---|
| `ConnectionStrings__Default` | SQLite connection string |
| `GapGpt__ApiKey` | GapGPT API key |
| `GapGpt__Model` | Model name (default: `gpt-6-luna`) |
| `HubSpot__AccessToken` | HubSpot private app access token |
| `SmsIr__ApiKey` | SMS.ir API key |
| `SmsIr__LineNumber` | SMS.ir dedicated line number |
| `SmsIr__TemplateId` | SMS.ir OTP template ID (integer) |
| `Security__JwtSecretKey` | JWT signing secret (min 32 chars) |
| `Otp__ExpirationMinutes` | OTP validity window (default: 5) |
| `Otp__MaxAttempts` | Max OTP verify attempts (default: 5) |
| `Otp__MaxRequestsPerWindow` | Max OTP requests per window (default: 5) |
| `AllowedOrigins` | Comma-separated CORS origins (production) |

---

## Docker

### Build and run with Docker Compose

1. Create a `.env` file (never commit):

```env
GAPGPT_API_KEY=your-key
HUBSPOT_ACCESS_TOKEN=your-token
SMSIR_API_KEY=your-key
SMSIR_LINE_NUMBER=your-number
SMSIR_TEMPLATE_ID=12345
JWT_SECRET_KEY=your-32-char-minimum-random-secret
ALLOWED_ORIGINS=https://yourdomain.com
```

2. Build and start:

```bash
docker compose up --build -d
```

3. Verify health:

```bash
curl http://localhost:8080/api/health
```

### Standalone Docker

```bash
docker build -t persian-ai-chat .
docker run -d \
  -p 8080:8080 \
  -v $(pwd)/data:/data \
  -e GapGpt__ApiKey=your-key \
  -e HubSpot__AccessToken=your-token \
  -e SmsIr__ApiKey=your-key \
  -e SmsIr__TemplateId=12345 \
  -e Security__JwtSecretKey=your-32-char-secret \
  persian-ai-chat
```

---

## EF Core Migrations

To create a new migration after schema changes:

```bash
cd src/PersianAiChat.Api
dotnet ef migrations add MigrationName \
  --project ../PersianAiChat.Infrastructure \
  --startup-project .

dotnet ef database update
```

---

## Architecture

```
Browser
  │  HttpOnly Cookie (auth)
  ▼
ASP.NET Core 8 API  ──►  HubSpot CRM API
  │                  ──►  SMS.ir OTP API
  ▼                  ──►  GapGPT API
Application Layer
  │
  ▼
Domain Layer
  │
  ▼
Infrastructure (EF Core / SQLite)
```

**Dependency direction:**
```
Api → Application → Domain
Infrastructure → Application + Domain
```

---

## Security Notes

- **OTP** values are never stored — only their SHA-256 hash
- **Authentication** uses HttpOnly, Secure, SameSite=Strict cookies
- **CSRF** protection via antiforgery tokens (X-XSRF-TOKEN header)
- **HubSpot token** is never exposed to the frontend
- **Reasoning** from GapGPT (`message.reasoning`, `reasoning_details`) is discarded before the response reaches the API controller
- **Rate limiting** on OTP (5 req/15 min) and Chat (30 req/min) endpoints
- **SQL injection** impossible via EF Core parameterized queries
- **XSS** protection via Content-Security-Policy headers + client-side sanitization

---

## Cloudflare Configuration

Recommended setup:
```
User → Cloudflare (DNS, TLS, WAF, DDoS) → ASP.NET Core 8 (port 8080)
```

- Enable **Full (Strict) TLS** in Cloudflare
- Add a **WAF rule** to block non-Iranian IPs if needed
- Set **Cache Rules** to bypass cache for `/api/*`
- Enable **Bot Fight Mode** for additional protection

---

## License

MIT
