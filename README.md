# **DotNetChatServer**

![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet)
![Minimal API](https://img.shields.io/badge/ASP.NET-Minimal%20API-512BD4)
![OpenAPI](https://img.shields.io/badge/OpenAPI-3.1-6BA539)
![Scalar UI](https://img.shields.io/badge/Docs-Scalar%20UI-1F7A8C)
![License: MIT](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-success)

DotNetChatServer is a backend for a real time chat application built with ASP.NET Core Minimal APIs.
It handles authentication, user management, message flow, online presence, and long polling updates with a concurrency safe in memory architecture.
The project highlights backend engineering techniques such as structured endpoint design, session based security, efficient message delivery, and lock protected data stores.

## **Features**

### **Authentication and Security**

• Session based authentication token system
• Login and registration flows
• Role based authorization through admin flags
• Automatic token regeneration on login

### **Messaging**

• Message creation with server generated IDs
• Message history retrieval
• Long polling update channel with efficient wake up notifications
• Admin controlled message clearing

### **Users**

• Full user lifecycle: create, update, delete
• Username management with conflict detection
• Real time presence tracking using heartbeat and activity timestamps
• Admin elevation support

### **Concurrency and Architecture**

• ReaderWriterLockSlim protected stores
• Asynchronous log writer with file rotation
• Task based long poll notifier
• Clear separation of concerns across endpoints, stores, and services

### **Developer Experience**

• Strong OpenAPI and Scalar UI integration
• Interactive API reference available at `/scalar`
• Custom OpenAPI transformer that describes routes, schemas, and security
• Local development on a clean endpoint structure using Minimal APIs

## **Tech Stack**

| Area             | Technology                                        |
| ---------------- | ------------------------------------------------- |
| Framework        | .NET 9, ASP.NET Core Minimal API                  |
| Documentation    | OpenAPI, Scalar UI                                |
| Concurrency      | ReaderWriterLockSlim, TaskCompletionSource        |
| Logging          | Asynchronous log writer with rotation             |
| Data Models      | Custom DTOs and in memory stores                  |
| Deployment Ready | Configurable ports, environment based appsettings |

## **Project Structure**

```
DotNetChatServer
├── ChatServer
│   ├── Auth
│   ├── Configuration
│   ├── Endpoints
│   ├── Logger
│   ├── Models
│   ├── Services
│   └── Store
└── Shared
    ├── DTOs
    └── Shared.csproj
```

**ChatServer**
Contains the backend logic, endpoint definitions, stores, and utilities.

**Shared**
Contains DTOs shared between server and possible clients.

## **API Overview**

All API routes are documented visually at
**`/scalar`** (Scalar UI)
and programmatically at
**`/openapi/v1.json`**.

### **Authentication (`/auth`)**

• `POST /auth/login`
• `POST /auth/register`

### **Users (`/users`)**

• `GET /users` – list usernames
• `POST /users/update` – update username and optionally password
• `POST /users/delete` – remove user
• `GET /users/status` – list online states
• `POST /users/heartbeat` – update presence

### **Messages (`/messages`)**

• `POST /messages/send` – create message
• `GET /messages/history` – full or limited history
• `GET /messages/updates` – long polling endpoint
• `POST /messages/clear` – admin clear

### **System (`/system`)**

• `GET /system/health` – health check for uptime monitors

## **Long Polling Implementation**

The server avoids constant polling by pairing:

**MessageNotifier**
Stores a list of TaskCompletionSources that represent waiting clients.

**MessageStore.Add**
Signals all pending long pollers through the notifier.

**`/messages/updates`**
Awaits notification or a 25 second timeout.
This creates an efficient real time pipeline for clients.

## **Concurrency Model**

Both the message store and user store inherit from **ConcurrentStoreBase**, which wraps all reads and writes in ReaderWriterLockSlim.
This ensures:

• Unlimited parallel reads
• Exclusive writes
• Safe multi threaded access
• Clean isolation of locking logic

## **Logging**

The custom logger writes asynchronously using a queue.
Capabilities include:

• File rotation
• Severity filtering
• Graceful shutdown
• Structured timestamped lines

Logs are stored in the `/logs` directory.

## **Running Locally**

Run the server on port **5201**:

```
dotnet run --project ChatServer
```

Navigate to:

• **[http://localhost:5201/scalar](http://localhost:5201/scalar)** for the interactive API
• **[http://localhost:5201/openapi/v1.json](http://localhost:5201/openapi/v1.json)** for the raw JSON schema
