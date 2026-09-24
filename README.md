# SE499 Industry Training Projects

This repository contains the enterprise backend software systems engineered during the compulsory Software Engineering Industry Training (SE499) at **Üçüncü Binyıl Akademi**.

## Technology Stack
* **Language & Framework:** C# (.NET 10.0)
* **ORM:** Entity Framework Core (Code-First)
* **Database Management:** Microsoft SQL Server (LocalDB) / SSMS
* **Architecture:** Layered Architecture, Repository & Service Pattern, CLI Presentation

---

## Projects Overview

### 1. Inventory & Supply Chain Management System
* **Directory:** `/InventoryManagementSystem`
* **Core Capabilities:**
  * Multi-tier commercial inventory synchronization (Wholesaler Warehouse ➔ Greengrocer Retail Store).
  * Automated 35% margin markup calculation upon stock transfer.
  * Relational schema integrity enforced via Category foreign keys (`FK_Products_Categories_CategoryId`).
  * Explicit ACID transactions (`IDbContextTransaction`) with simulated hardware fault injection and rollback validation.

### 2. Core Banking ATM Management System
* **Directory:** `/AtmManagementSystem`
* **Core Capabilities:**
  * Cardholder authentication using one-way cryptographic **SHA-256 PIN hashing**.
  * Stateful brute-force attack mitigation (automated account lockout `IsBlocked = true` after 3 failed PIN attempts).
  * Exception-driven risk engine intercepting overdrafts and daily withdrawal quotas.
  * Atomic inter-account fund transfers with automatic rollback upon mid-flight network interruptions.

---

## How to Run Locally

1. **Clone the repository:**
   ```bash
   git clone [https://github.com/murselyildirim/SE499-Industry-Training-Projects.git](https://github.com/murselyildirim/SE499-Industry-Training-Projects.git)
