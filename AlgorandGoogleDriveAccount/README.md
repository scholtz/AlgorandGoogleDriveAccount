# Biatec MCP Server - Algorand Google Drive Integration

A secure Model Context Protocol (MCP) server that enables AI assistants to interact with Algorand accounts stored encrypted in Google Drive.

## ?? Features

- **Secure Account Storage**: Algorand private keys encrypted and stored in your personal Google Drive
- **MCP Integration**: Compatible with Claude Desktop and Visual Studio Code
- **Device Pairing**: Secure cross-device synchronization with OAuth
- **Cross-Account Protection**: Enhanced security monitoring and threat detection
- **Incremental Authorization**: Request permissions only when needed

## ?? Quick Start

### Prerequisites

- .NET 8.0 or later
- Redis server
- Google OAuth 2.0 credentials
- Google Drive API access

### Installation

1. Clone the repository:git clone <repository-url>
cd AlgorandGoogleDriveAccount
2. Configure settings in `appsettings.json`:{
  "App": {
    "Host": "https://your-domain.com",
    "ClientId": "your-google-client-id",
    "ClientSecret": "your-google-client-secret"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
3. Run the application:dotnet run
## ?? Configuration

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google Drive API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URIs:
   - `https://your-domain.com/signin-google`
   - `https://your-domain.com/api/device/paired-device`

### MCP Client Configuration

#### Claude Desktop{
  "mcpServers": {
    "biatec-algorand": {
      "command": "node",
      "args": ["path/to/biatec-mcp-client.js"],
      "env": {
        "MCP_SERVER_URL": "https://your-domain.com/mcp",
        "SESSION_ID": "your-unique-session-id"
      }
    }
  }
}
#### Visual Studio Code{
  "mcp.servers": [
    {
      "name": "biatec-algorand",
      "url": "https://your-domain.com/mcp",
      "sessionId": "your-unique-session-id"
    }
  ]
}
## ?? Project Structure
AlgorandGoogleDriveAccount/
??? Controllers/           # API controllers
?   ??? DevicePairingController.cs
?   ??? DriveController.cs
??? BusinessLogic/         # Service layer
?   ??? DevicePairingService.cs
?   ??? DriveService.cs
?   ??? CrossAccountProtectionService.cs
??? Repository/            # Data access layer
?   ??? GoogleDriveRepository.cs
??? Model/                 # Data models
?   ??? Configuration.cs
??? MCP/                   # MCP server implementation
?   ??? BiatecMCPGoogle.cs
??? Helper/                # Utility classes
?   ??? AesEncryptionHelper.cs
??? wwwroot/               # Static web files
    ??? index.html
    ??? pair.html
    ??? privacy.html
    ??? terms.html
## ?? Security

- **Encryption**: All private keys encrypted with AES-256
- **OAuth 2.0**: Secure Google authentication
- **CORS**: Configurable cross-origin resource sharing
- **Rate Limiting**: API call limits by service tier
- **Audit Logging**: Comprehensive security event logging
- **UTF-8 Compliance**: All HTML pages properly encoded with UTF-8 for international character support

## ?? Wealth Management Tiers

Service tiers are automatically determined based on your total Algorand portfolio value:

| Tier | Portfolio Value | Devices | Support | SLA | Features |
|------|----------------|---------|---------|-----|----------|
| Free | < €10,000 | 1 | Community | Best effort | Basic account management |
| Professional | €10,000 - €1,000,000 | 5 | Priority | 99.5% | Portfolio analytics, risk tools |
| Enterprise | > €1,000,000 | Unlimited | Dedicated | 99.9% | Custom integrations, account manager |

**Note:** No monthly fees - tier assignment is automatic based on portfolio value calculated daily using real-time market prices.

## ?? Deployment

### Docker Deploymentdocker build -t biatec-mcp-server .
docker run -p 80:80 biatec-mcp-server
### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `ConnectionStrings__Redis`: Redis connection string
- `App__ClientId`: Google OAuth client ID
- `App__ClientSecret`: Google OAuth client secret

## ?? API Documentation

Visit `/swagger` when running the application to access the interactive API documentation.

### Key Endpoints

- `GET /`: Main application page
- `GET /api/device/app`: Device pairing interface
- `GET /api/device/pair-device`: Initiate device pairing
- `GET /api/device/access-token/{sessionId}`: Get access token
- `GET /mcp`: MCP server endpoint

## ?? Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## ?? Legal

- **Privacy Policy**: [/privacy.html](./wwwroot/privacy.html)
- **Terms of Service**: [/terms.html](./wwwroot/terms.html)
- **Company**: Scholtz & Company, j.s.a. (Slovakia)
- **Company ID**: 51882272
- **Tax ID**: 2120828105

## ?? Support

- **General**: support@biatec.io
- **Privacy**: privacy@biatec.io
- **Legal**: legal@biatec.io

## ?? License

This project is proprietary software owned by Scholtz & Company, j.s.a.

---

*Built with ?? for the Algorand ecosystem*