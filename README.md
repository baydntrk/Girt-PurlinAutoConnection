# BCA Soft – Engineering Automation Launcher (Internal)

This project is an internal Windows Forms application developed by BCA Soft to centralize and streamline access to proprietary engineering automation tools.

## 🔒 Repository Notice

This repository is private and intended for internal use only.
It contains integrations with proprietary tools and workflows that are not publicly distributed.

---

## 🚀 Overview

The application serves as a centralized launcher for multiple engineering automation tools, with a strong focus on **Tekla Structures-based workflows**, including **automated girt–purlin connection systems**.

It enables engineers to access and execute complex automation tools from a single interface, improving efficiency and reducing manual intervention.

---

## 🧩 Core Capabilities

* Centralized execution of internal engineering tools
* Automated **girt–purlin connection workflows**
* Integration with Tekla-based automation systems
* One-click launch for multiple utilities
* Structured and scalable UI architecture

---

## 🛠️ Integrated Tools

The launcher provides access to internally developed tools such as:

* Mesh Panel
* Preparing Macros
* Import / Export Tool
* Equipment Connection
* Sag Rod Tool
* Data Check
* Assembly Check
* **Girt–Purlin Auto Connection System**

These tools are designed to automate repetitive engineering tasks, reduce human error, and standardize modeling workflows.

---

## ⚙️ Technical Details

* **Language:** C#
* **Framework:** .NET (Windows Forms)
* **Architecture:** UI-based launcher with external process execution
* Uses `ProcessStartInfo` for running external executables

---

## 📁 System Requirements

* Windows OS
* Pre-installed internal tools under:

```
C:\ProgramData\BCASOFT\
```

Each executable must exist in its expected directory.

---

## ⚠️ Important Notes

* This system relies on predefined file paths.
* Missing tools will trigger runtime warnings.
* Some tools may require administrator privileges.
* Not intended for external distribution.

---

## 💡 Internal Roadmap (Optional)

* Configurable tool paths
* Centralized update system
* Logging & monitoring
* User-based access control
* Integration with licensing infrastructure

---

## 🏢 About BCA Soft

BCA Soft develops advanced engineering automation and interactive systems focused on BIM, structural modeling, and construction workflows.

The goal is to reduce manual workload, minimize errors, and deliver scalable, production-ready engineering solutions.

---

