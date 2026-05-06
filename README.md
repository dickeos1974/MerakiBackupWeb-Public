Meraki Backup & Restore Tool

Internal ASP.NET Core web application for automated Cisco Meraki configuration backups and controlled restores.

⸻

🚀 Features

Automated Backups

* Nightly scheduled backups
* Manual backup execution from web UI
* 30-day retention policy
* Per-run logging

Restore Capabilities

* SSID restore
* VLAN restore
* L3 firewall restore
* Switch port restore (single-port controlled restore)

Built-in Safety Controls

* Network tag enforcement (merakiRestore)
* Confirmation prompts before restore
* Pre-flight preview checks
* Uplink protection for switch ports

⸻

🏗️ Architecture

Browser UI
    ↓
Razor Pages
    ↓
MerakiBackupRunner
    ↓
Cisco Meraki API
    ↓
Local Backup Storage

⸻

📂 Backup Contents

Each backup run stores:

organizations.json
networks_<org>.json
ssids_<org>_<network>.json
vlans_<org>_<network>.json
firewall_<org>_<network>.json
switch_ports_<org>_<network>_<serial>.json
devices_<org>_<network>.json
log.txt

⸻

⚠️ Important Notes

Firewall Restore

Firewall restores overwrite the entire L3 firewall ruleset.

Switch Port Restore

Switch port restore is intentionally restricted to:

* One port at a time
* Non-uplink ports only

Bulk restore is intentionally disabled to reduce outage risk.

⸻

🔐 Restore Protection

Restores are blocked unless the target network contains the tag:

merakiRestore

All restores also require manual confirmation.

⸻

🧰 Technology Stack

* ASP.NET Core Razor Pages
* IIS Hosting
* Cisco Meraki Dashboard API
* JSON-based backup storage

⸻

⚙️ Configuration

appsettings.Production.json

{
  "Meraki": {
    "ApiKey": "YOUR_API_KEY",
    "BaseUrl": "https://api.meraki.com/api/v1/",
    "OrgId": "YOUR_ORG_ID"
  },
  "Scheduler": {
    "Key": "YOUR_SECRET_KEY"
  },
  "Backup": {
    "BackupPath": "Backups"
  }
}

⸻

🛠️ Local Development

Requirements

* .NET 8 SDK
* Visual Studio 2022+
* Meraki API key

⸻

Run locally

dotnet restore
dotnet build
dotnet run

Application default URL:

https://localhost:5001

⸻

🌐 IIS Deployment

Install prerequisites

* IIS
* ASP.NET Core Hosting Bundle

⸻

Publish

In Visual Studio:

Right Click Project
→ Publish
→ Folder

Copy published files to:

C:\inetpub\MerakiBackupWeb

⸻

IIS Configuration

Application Pool

Name: MerakiBackupWeb
.NET CLR Version: No Managed Code
Pipeline: Integrated

⸻

Permissions

Grant modify access to:

C:\inetpub\MerakiBackupWeb\Backups

For:

IIS AppPool\MerakiBackupWeb

⸻

⏰ Scheduled Backups

Backups are triggered using Task Scheduler.

Example:

Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:8082/?handler=RunScheduledBackup&key=YOUR_SECRET_KEY"

Typical schedule:

Daily at 22:00

⸻

🗂️ Retention Policy

The application automatically deletes backups older than:

30 days

⸻

📡 Restore Workflows

SSID Restore

* Supports PSK and RADIUS configurations
* Updates existing SSID slots

VLAN Restore

* Creates missing VLANs
* Updates existing VLANs

Firewall Restore

* Replaces full firewall ruleset
* Confirmation required

Switch Port Restore

* Controlled one-port-at-a-time restore
* Uplink ports blocked

⸻

🧪 Troubleshooting

Backup fails

Check:

* Meraki API key
* Internet connectivity
* IIS permissions
* Task Scheduler history

⸻

Restore blocked

Check:

* Target network contains merakiRestore
* Confirmation checkbox selected

⸻

No backups created

Check:

* AppPool permissions
* Backup folder exists
* IIS application identity access

⸻

🚀 Future Enhancements

* Auto-backup before restore
* Restore audit logging
* Configuration diff view
* Email notifications
* Multi-organisation support

⸻

📄 License

Internal use only.

⸻

👤 Ownership

System: Meraki Backup & Restore Tool
Purpose: Automated backup and controlled restore
Platform: ASP.NET Core / IIS
