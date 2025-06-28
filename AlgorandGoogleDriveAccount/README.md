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
   cd AlgorandGoogleDriveAccount2. Configure settings in `appsettings.json`:{
  "App": {
    "Host": "https://your-domain.com",
    "ClientId": "your-google-client-id",
    "ClientSecret": "your-google-client-secret"
  },
  "Redis": {
       "ConnectionString": "localhost:6379"
     }
   }3. Run the application:dotnet run
## ?? Configuration & Setup Instructions

### Prerequisites Setup

Before connecting to the MCP server, you need:

1. **Active Internet Connection**: To access https://google.biatec.io/mcp/
2. **Google Account**: For authentication and Drive access
3. **Session ID**: A unique identifier for your session (you can generate one or use any unique string)

### ?? Claude Desktop Setup

Claude Desktop has built-in MCP support. Follow these steps to connect to the Biatec MCP Server:

#### Step 1: Locate Claude Desktop Configuration

1. **Windows**: Open `%APPDATA%\Claude\claude_desktop_config.json`
2. **macOS**: Open `~/Library/Application Support/Claude/claude_desktop_config.json`
3. **Linux**: Open `~/.config/Claude/claude_desktop_config.json`

If the file doesn't exist, create it.

#### Step 2: Add Biatec MCP Server Configuration

Add the following configuration to your `claude_desktop_config.json`:
{
  "mcpServers": {
    "biatec-algorand": {
      "command": "npx",
      "args": [
        "@modelcontextprotocol/server-fetch",
        "https://google.biatec.io/mcp/"
      ],
      "env": {
        "MCP_SERVER_URL": "https://google.biatec.io/mcp/",
        "SESSION_ID": "your-unique-session-id-here"
      }
    }
  }
}
**Important**: Replace `"your-unique-session-id-here"` with your own unique session ID (e.g., `"claude_session_123456"`)

#### Step 3: Restart Claude Desktop

1. Close Claude Desktop completely
2. Reopen Claude Desktop
3. The Biatec MCP Server should now be available

#### Step 4: Pair Your Device

1. Visit: `https://google.biatec.io/pair.html?session=your-unique-session-id-here`
2. Replace `your-unique-session-id-here` with the same Session ID from Step 2
3. Complete the Google OAuth authentication
4. Grant Google Drive permissions when prompted

#### Step 5: Test the Connection

In Claude Desktop, try asking:"Can you get my Algorand address using the Biatec MCP server?"
Claude should now be able to access your Algorand account information!

### ?? Visual Studio Code Setup

Visual Studio Code requires the MCP extension and configuration file.

#### Step 1: Install MCP Extension

1. Open Visual Studio Code
2. Go to Extensions (Ctrl+Shift+X)
3. Search for "Model Context Protocol" or "MCP"
4. Install the official MCP extension

#### Step 2: Create MCP Configuration File

Create a file named `mcp.json` in your project root or workspace folder:
{
  "mcp": {
    "servers": [
      {
        "name": "biatec-algorand",
        "url": "https://google.biatec.io/mcp/",
        "description": "Biatec Algorand Google Drive MCP Server",
        "sessionId": "your-unique-session-id-here",
        "headers": {
          "Content-Type": "application/json"
        }
      }
    ]
  }
}
**Important**: Replace `"your-unique-session-id-here"` with your own unique session ID (e.g., `"vscode_session_789012"`)

#### Step 3: Configure VS Code Settings

Open VS Code settings (`Ctrl+,`) and add:
{
  "mcp.configFile": "./mcp.json",
  "mcp.autoConnect": true
}
Or add to your workspace settings in `.vscode/settings.json`:
{
  "mcp.servers": [
    {
      "name": "biatec-algorand",
      "url": "https://google.biatec.io/mcp/",
      "sessionId": "your-unique-session-id-here"
    }
  ]
}
#### Step 4: Pair Your Device

1. Visit: `https://google.biatec.io/pair.html?session=your-unique-session-id-here`
2. Replace `your-unique-session-id-here` with the same Session ID from Step 2
3. Complete the Google OAuth authentication
4. Grant Google Drive permissions when prompted

#### Step 5: Test the Connection

1. Open Command Palette (`Ctrl+Shift+P`)
2. Run "MCP: Connect to Server"
3. Select "biatec-algorand"
4. Try using MCP tools in your workflow

### ?? Available MCP Tools

Once connected, you can use these tools:

- **`getAlgorandAddress`**: Retrieves your Algorand account address from encrypted Google Drive storage

### ?? Troubleshooting

#### Common Issues:

1. **"Session not found" error**:
   - Make sure you've completed device pairing at `https://google.biatec.io/pair.html?session=YOUR_SESSION_ID`
   - Verify your Session ID matches in both the configuration and pairing URL

2. **"Access token expired" error**:
   - Re-visit the pairing page to refresh your authentication
   - Ensure Google Drive permissions are granted

3. **"MCP server not responding" error**:
   - Check your internet connection
   - Verify the server URL: `https://google.biatec.io/mcp/`
   - Try restarting your IDE

#### Getting Help:

- **Technical Support**: support@biatec.io
- **Device Pairing Issues**: Visit https://google.biatec.io/pair.html
- **Documentation**: This README and inline help

### Google OAuth Setup (For Developers)

If you're setting up your own instance:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google Drive API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URIs:
   - `https://your-domain.com/signin-google`
   - `https://your-domain.com/api/device/paired-device`

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

### Docker Deployment
docker build -t biatec-mcp-server .
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