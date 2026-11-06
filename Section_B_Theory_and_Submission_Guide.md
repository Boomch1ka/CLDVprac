# CLDV7112 Practicum — Section B Theory Answers & Architecture Documentation

---

## Part A: Architecture & Service Descriptions (For Documentation Section — 15 Marks)

### 1. System Architecture Overview
AgriStream IoT is designed as a cloud-native, horizontally scalable real-time farm monitoring and automated irrigation platform built on Microsoft Azure. It decouples user-facing web traffic from high-frequency sensor telemetry and asynchronous background processing across five Azure services:

1. **Azure App Service (`AgriStream.Web` — ASP.NET Core 8 MVC):**
   - Hosts the stateless management web application at `https://<student_number>.azurewebsites.net`.
   - Authenticates users via stateless encrypted cookies backed by **Azure SQL Database** and enforces Role-Based Access Control (`Farmer`, `Agronomist`, `Admin`).
   - Configured with **Custom Auto-Scale (Scale-Out)** rules based on CPU and Memory percentage thresholds to handle peak daylight traffic surges.
2. **Azure SQL Database (`AgriStreamDb`):**
   - Stores relational, transactional data requiring strict ACID consistency and foreign-key integrity:
     - `dbo.Roles` (`Farmer`, `Agronomist`, `Admin`)
     - `dbo.Users` (`Id`, `Username`, `PasswordHash`, `RoleId`, `CreatedAt`)
     - `dbo.Devices` (`Id`, `DeviceId`, `Name`, `FieldName`, `Status`, `LastIrrigatedAt`, `IrrigationEndsAt`, `LastCommandBy`, `UpdatedAt`) — the master device registry updated by the Queue-triggered Azure Function.
3. **Azure Blob Storage (`drone-images` container):**
   - Stores unstructured high-resolution aerial field imagery uploaded by farmers and drones, tagged with custom metadata (`uploadedBy`, `fieldName`, `originalName`, `notes`) and served securely via time-limited Read-Only Shared Access Signature (SAS) URLs.
4. **Azure Table Storage (`Telemetry`, `UploadAudit`, `IrrigationEvents` tables):**
   - Stores high-frequency, schema-flexible NoSQL telemetry and audit logs partitioned by `DeviceId` (or `yyyy-MM` for uploads) and ordered newest-first using inverted-tick `RowKey` values (`DateTime.MaxValue.Ticks - UtcTicks`) for single-millisecond point/range queries.
5. **Azure Storage Queue (`irrigation-commands`) & Azure Functions (`AgriStream.Functions` — 4 Functions):**
   - Implements the serverless event-driven pipeline using a **Blob Trigger**, **HTTP GET Trigger**, **HTTP POST Trigger**, and **Queue Trigger**, decoupling command submission from SQL updates so zero data is lost during peak operational bursts.

---

## Section B: Theory Questions (25 Marks)

### 1. Disaster Recovery & Redundancy (Locally Redundant Storage vs. Geo-Redundant Storage)

#### Overview of Azure Storage Redundancy Options
Azure Storage automatically replicates every blob, table entity, and queue message to protect against planned and unplanned events, ranging from transient hardware faults to catastrophic regional disasters. Two core redundancy options are **Locally Redundant Storage (LRS)** and **Geo-Redundant Storage (GRS)**.

| Comparison Dimension | Locally Redundant Storage (LRS) | Geo-Redundant Storage (GRS / RA-GRS) |
|---|---|---|
| **Replication Mechanism** | Synchronously replicates data **three times within a single physical data centre** (single availability zone/scale unit) in the primary region before returning `Success` to the caller. | Replicates data **three times synchronously** in the primary region (using LRS) and then **asynchronously replicates the data to a paired secondary region** hundreds of miles away, where it is again stored three times via LRS (**6 copies total**). |
| **Annual Durability SLA** | At least **99.999999999% (11 nines)** durability over a given year. | At least **99.99999999999999% (16 nines)** durability over a given year. |
| **Failure Scenarios Covered** | Protects against server rack, disk drive, network switch, and power-unit failures inside a single data centre. **Does not** protect against fire, flooding, or power grid collapse affecting the entire data centre or region. | Protects against both local hardware failures **and** a complete regional outage in the primary region. With **Read-Access GRS (RA-GRS)**, applications can immediately read from the secondary region endpoint even before Microsoft initiates a regional failover. |
| **Cost & Network Overhead** | Lowest storage cost per gigabyte; zero cross-region replication bandwidth charges. | Approximately double the per-GB storage cost of LRS, plus geo-replication egress bandwidth charges for every write operation. |

#### Recommendation for AgriStream IoT Workloads
1. **Critical Historical Crop Harvest Records → Geo-Redundant Storage (GRS / RA-GRS):**
   - Multi-season harvest yields, soil fertility baselines, compliance audits, and agronomic imagery archives are **irreplaceable, high-value business records**. If a regional disaster struck the primary Azure region (for example, `South Africa North` in Johannesburg), permanent loss of historical harvest records would destroy years of longitudinal crop analytics and regulatory evidence.
   - Because these datasets are written relatively infrequently (end of harvest or periodic summaries) and have a strict Recovery Point Objective (RPO) and durability requirement, **GRS (or RA-GRS)** is the most appropriate choice. Its 6-copy cross-region replication to the paired region (`South Africa West` in Cape Town) guarantees 16 nines of durability and regional disaster recovery.
2. **Temporary Sensor Logs → Locally Redundant Storage (LRS):**
   - Millions of field sensors transmit soil moisture, temperature, and battery telemetry every few seconds. Individual raw sensor log entries are **ephemeral, append-heavy, and rapidly superseded** by newer readings minutes later.
   - Replicating millions of transient sensor log writes across regions via GRS would double storage costs and incur massive cross-region egress fees for data that has low long-term criticality and can be re-sampled by active sensors. **LRS** provides 11 nines of durability against local hardware/disk failures at the lowest cost and write latency, making it ideal for temporary sensor logs (`Telemetry`) and transient processing queues.

---

### 2. Stateless vs. Stateful Architecture in Horizontal Scaling

#### Why Statelessness Is Crucial for Azure App Services and Azure Functions
- **Horizontal Scaling (Scale-Out)** increases capacity during traffic surges by adding independent compute instances (`Instance 1`, `Instance 2`, `Instance 3`, …) behind an Azure load balancer and removing them (**Scale-In**) when load subsides.
- A **stateless service** stores no client session state in local process memory (`RAM`) and no user files on the local VM disk (`C:\home` / local temp folders) between requests. Instead, every request is self-contained (e.g., carrying an encrypted authentication cookie or bearer token), and all persistent state is externalised to shared backing services (**Azure SQL Database**, **Azure Blob Storage**, **Azure Table Storage**, **Azure Storage Queues**, or **Azure Cache for Redis**).
- In **AgriStream IoT**:
  - `AgriStream.Web` uses stateless ASP.NET Core Cookie Authentication and persists users/devices in **Azure SQL Database** and drone media in **Azure Blob Storage**. Any App Service instance can serve any request from any farmer at any time.
  - `AgriStream.Functions` executes four completely stateless functions. Whether the Azure Functions scale controller runs 1 worker or 20 concurrent workers during a daylight telemetry surge, every worker reads and writes directly to Azure Table, Blob, Queue, and SQL storage without depending on any other worker's memory.

#### How Storing Session Data or Files Locally Negatively Affects Scale-Out Behaviour
1. **In-Memory Session State Breaks User Experience & Load Balancing:**
   - Suppose a web application stores user login state or form workflow data in local server memory (`InProc` session or `IMemoryCache`) on `Instance 1`. When CPU usage exceeds 70% and Azure App Service scales out to `Instance 2`, the load balancer routes the user's next HTTP request to `Instance 2`. Because `Instance 2` has a separate memory space, the user's session is missing—resulting in **unexpected logouts, lost form state, or `NullReferenceException` errors**.
   - Enabling **ARR Affinity (Sticky Sessions)** to force a user back to `Instance 1` undermines horizontal scaling: existing users remain pinned to the already-overloaded `Instance 1` while newly provisioned instances sit underutilised, and if `Instance 1` restarts or scales in, every pinned user still loses their session immediately.
2. **Local File Storage Causes `404 Not Found` Errors and Permanent Data Loss:**
   - If a farmer uploads a drone image and the web server saves it to a local folder on `Instance 1`'s disk (`C:\local\uploads\field1.jpg`), subsequent `GET` requests routed to `Instance 2` or `Instance 3` will fail with `404 Not Found` because the file only exists on `Instance 1`.
   - Worse, when peak daylight hours end and App Service **scales in** (terminating `Instance 2` and `Instance 3`) or recycles containers during deployment, all files stored on local instance disks are **permanently deleted**. Storing media in **Azure Blob Storage** ensures every instance sees the exact same durable files immediately.

---

### 3. Serverless Triggers and Bindings Comparison Table

In Azure Functions, **Triggers** and **Input/Output Bindings** provide a declarative programming model that connects serverless code to Azure services without manual polling loops or boilerplate client lifecycle code.

| Concept | Definition | Purpose | Example Used in the AgriStream IoT Pipeline |
|---|---|---|---|
| **Trigger** | A declarative attribute placed on a function parameter that defines the **single event source** that invokes the function and passes the triggering event payload into the method. Each Azure Function has **exactly one** trigger. | Automatically starts function execution when a specific cloud event occurs (an HTTP request, a new blob upload, or a new queue message) and allows the Azure Functions scale controller to scale worker instances dynamically based on the incoming event rate. | 1. **Blob Trigger** (`[BlobTrigger("drone-images/{name}", Connection = "AzureWebJobsStorage")]`) on `DroneImageAuditFunction`: fires automatically when a drone photo is uploaded to the `drone-images` Blob container.<br>2. **HTTP GET Trigger** (`[HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/latest")]`) on `GetLatestTelemetry`: fires when the MVC dashboard requests recent sensor telemetry.<br>3. **HTTP POST Trigger** (`[HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation/trigger")]`) on `TriggerIrrigation`: fires when a farmer clicks **"Trigger Irrigation"** in the MVC app.<br>4. **Queue Trigger** (`[QueueTrigger("irrigation-commands", Connection = "AzureWebJobsStorage")]`) on `ProcessIrrigationCommand`: fires whenever an irrigation command message appears on the `irrigation-commands` queue. |
| **Input Binding** | A declarative parameter binding (or host-managed storage client) that **retrieves supporting data** from an external Azure resource and supplies it to the function as a strongly typed object when the trigger fires. | Removes repetitive connection-string management, SDK boilerplate, and deserialization code when reading blobs, metadata, or table entities needed during function execution. | - In `DroneImageAuditFunction`, the `[BlobTrigger("drone-images/{name}")]` attribute binds the incoming blob directly to an `Azure.Storage.Blobs.BlobClient` parameter so the function can immediately read the blob's `ContentLength`, `ContentType`, and custom metadata (`uploadedBy`, `fieldName`, `originalName`) from Azure Blob Storage.<br>- In `GetLatestTelemetry`, the function queries partitioned `TableEntity` records from the `Telemetry` table in `AzureWebJobsStorage`. |
| **Output Binding** | A declarative attribute placed on a function's return value (or a multi-output response class) that instructs the Azure Functions runtime to **write, insert, or enqueue the returned object** into a target service automatically upon completion. | Decouples the function's business logic from downstream storage/queue SDK calls; the runtime handles serialization, connection pooling, and atomic delivery of the output payload. | 1. **Table Output Binding** (`[TableOutput("UploadAudit", Connection = "AzureWebJobsStorage")]`) on `DroneImageAuditFunction`: automatically writes the returned `UploadAuditEntity` object into the `UploadAudit` Azure Table whenever a drone image is processed.<br>2. **Queue Output Binding** (`[QueueOutput("irrigation-commands", Connection = "AzureWebJobsStorage")]`) on `TriggerIrrigationOutput` in `TriggerIrrigation`: serializes the `IrrigationCommand` object to JSON and pushes it onto the `irrigation-commands` Azure Storage Queue while simultaneously returning `202 Accepted` via `HttpResponseData`.<br>3. **Table Output Binding** (`[TableOutput("IrrigationEvents", Connection = "AzureWebJobsStorage")]`) on `ProcessIrrigationCommand`: writes an `IrrigationEventEntity` audit row to the `IrrigationEvents` table after updating `dbo.Devices` in Azure SQL Database. |
