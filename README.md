# AgriStream IoT — Real-Time Farm Monitoring & Serverless Data Pipeline (CLDV7112 Practicum)

A casual, colourful **ASP.NET Core 8 MVC** web application and **4-Function Azure Functions (.NET 8 Isolated)** serverless data pipeline built for **AgriStream IoT**.

---

## 📁 Project Structure

| Path | Description |
|---|---|
| `AgriStream.sln` | Visual Studio Solution containing the MVC Web App and Azure Functions projects |
| `AgriStream.Web/` | ASP.NET Core 8 MVC Web Application (Casual UI, Role-Based Auth, Blob/Table/SQL/Functions integration) |
| `AgriStream.Functions/` | .NET 8 Isolated Azure Functions App with **4 Functions** (Blob Trigger, HTTP GET, HTTP POST + Queue Output, Queue Trigger + SQL/Table Output) |
| `sql/schema.sql` | T-SQL script for Azure SQL (`Roles`, `Users`, `Devices` tables + seed data) |
| `load-test/load-test.ps1` | PowerShell concurrent load generator & sensor surge simulator |
| `load-test/AgriStream-LoadTest.jmx` | Apache JMeter test plan for load testing & auto-scale verification |

---

## 🔌 Where to Plug In Your Azure Connection Strings

### 1. MVC Web App (`AgriStream.Web/appsettings.json` or Azure App Service → Environment Variables)

Update `AgriStream.Web/appsettings.json` (or set these in **Azure App Service → Settings → Environment variables**):

```json
{
  "ConnectionStrings": {
    "AgriStreamDb": "Server=tcp:<your-sql-server>.database.windows.net,1433;Initial Catalog=AgriStreamDb;Persist Security Info=False;User ID=<your-user>;Password=<your-password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=<your-storage>;AccountKey=<your-key>;EndpointSuffix=core.windows.net"
  },
  "Functions": {
    "GetTelemetryUrl": "https://<your-func-app>.azurewebsites.net/api/telemetry/latest?code=<function-key>",
    "TriggerIrrigationUrl": "https://<your-func-app>.azurewebsites.net/api/irrigation/trigger?code=<function-key>"
  },
  "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=...;IngestionEndpoint=..."
}
```

> **Default Admin Login** (automatically seeded into Azure SQL on first startup):
> - **Username:** `admin`
> - **Password:** `Admin@123`
> - Self-registration (`/Account/Register`) lets you create `Farmer` and `Agronomist` accounts, and the Admin **Users** page (`/Admin/Users`) lets you create or change any user's role (`Farmer`, `Agronomist`, `Admin`).

---

### 2. Azure Functions (`AgriStream.Functions/local.settings.json` or Function App → Environment Variables)

In `AgriStream.Functions/local.settings.json` (and in **Azure Function App → Settings → Environment variables**):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=<your-storage>;AccountKey=<your-key>;EndpointSuffix=core.windows.net",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "Server=tcp:<your-sql-server>.database.windows.net,1433;Initial Catalog=AgriStreamDb;Persist Security Info=False;User ID=<your-user>;Password=<your-password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=...;IngestionEndpoint=..."
  }
}
```

---

## ⚡ The 4 Azure Functions (`AgriStream.Functions`)

1. **`DroneImageAuditFunction` (Blob Trigger + Table Output Binding)**
   - **Trigger:** `[BlobTrigger("drone-images/{name}", Connection = "AzureWebJobsStorage")]`
   - **Output:** `[TableOutput("UploadAudit", Connection = "AzureWebJobsStorage")]`
   - **What it does:** Automatically fires whenever a drone image is uploaded to the `drone-images` Blob container via the MVC app, inspects its content type, size, and metadata (`uploadedBy`, `fieldName`, `notes`), and writes a processing audit entry directly into the `UploadAudit` Azure Table.
2. **`GetLatestTelemetry` (HTTP GET Trigger)**
   - **Trigger:** `[HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/latest")]`
   - **What it does:** Queries the `Telemetry` Azure Table Storage for the latest reading per sensor and recent time-series history for near-instant rendering on the Farmer/Agronomist dashboard.
3. **`TriggerIrrigation` (HTTP POST Trigger + Queue Output Binding)**
   - **Trigger:** `[HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation/trigger")]`
   - **Output:** `[QueueOutput("irrigation-commands", Connection = "AzureWebJobsStorage")]`
   - **What it does:** Validates an irrigation request from the MVC app and enqueues an `IrrigationCommand` message onto the `irrigation-commands` Azure Storage Queue so bursts of commands are buffered reliably.
4. **`ProcessIrrigationCommand` (Queue Trigger + Azure SQL Update + Table Output Binding)**
   - **Trigger:** `[QueueTrigger("irrigation-commands", Connection = "AzureWebJobsStorage")]`
   - **Output:** `[TableOutput("IrrigationEvents", Connection = "AzureWebJobsStorage")]`
   - **What it does:** Dequeues the irrigation command, updates the device's operational status (`Status = 'Irrigating'`, `LastIrrigatedAt`, `IrrigationEndsAt`, `LastCommandBy`) in the `dbo.Devices` master registry table in **Azure SQL Database**, and writes an audit record to the `IrrigationEvents` table.

---

## 🧪 Running Load Tests & Auto-Scaling (Tasks e, f, g)

### Auto-Scale Rules (Task g)
1. In the Azure Portal, open your **App Service Plan** → **Scale out (App Service plan)** → choose **Custom autoscale**.
2. Add a **Scale out rule**:
   - Metric: **CPU Percentage** (or **Memory Percentage**)
   - Operator: **Greater than** `70%`
   - Duration: `5 minutes`
   - Operation: **Increase count by** `1` instance (Maximum `3` instances, Cool down `5 minutes`).
3. Add a **Scale in rule**:
   - Metric: **CPU Percentage** **Less than** `30%` → **Decrease count by** `1` instance.

### Running the Load Test (Task e)
Run the included PowerShell load script against your deployed App Service URL:

```powershell
.\load-test\load-test.ps1 -BaseUrl "https://<student_number>.azurewebsites.net" -ConcurrentUsers 30 -DurationSeconds 180
```

Or open `load-test/AgriStream-LoadTest.jmx` in **Apache JMeter** or **Azure Load Testing**.
The script hits `/api/telemetry/forecast` (Monte-Carlo soil moisture simulation) and `/api/telemetry` (Table Storage sensor ingestion) concurrently, printing total throughput, average/p95 latency, and the list of distinct App Service instance IDs (`WEBSITE_INSTANCE_ID`) that served the requests.
