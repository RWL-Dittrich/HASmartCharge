# HASmartCharge

⚠️ **Work in Progress** ⚠️

## Overview

HASmartCharge is a Home Assistant integration that enables price-based scheduled charging for Home Assistant integrated vehicles. The system intelligently schedules EV charging based on electricity price data to minimize charging costs.

## Purpose

This application allows Home Assistant integrated cars to have scheduled price-based charging, optimizing when your vehicle charges based on electricity pricing to save money while ensuring your car is ready when you need it.

## Features (Planned/In Development)

- Home Assistant OAuth2 authentication integration
- Vehicle discovery and management through Home Assistant entities
- Price-based charging schedule optimization
- SQLite database for persistent configuration
- **OCPP 1.6J WebSocket server for direct charge point integration** ✅

## Tech Stack

- .NET 10.0
- ASP.NET Core Web API
- Entity Framework Core
- SQLite Database
- Home Assistant API Integration
- OCPP 1.6J (Open Charge Point Protocol)
- WebSocket Communication

## Project Status

This project is currently in active development. Features and functionality are subject to change.

## OCPP Server

The application includes a full OCPP 1.6J WebSocket server implementation in a separate class library project (`HASmartCharge.Backend.OCPP`). For detailed information about the OCPP server, including connection details, supported messages, and testing instructions, see [OCPP_README.md](HASmartCharge.Backend/OCPP_README.md).

Quick start:
- **WebSocket Endpoint**: `ws://localhost:5000/ocpp/1.6/{chargePointId}`
- **Sub-protocol**: `ocpp1.6`
- **Supported Operations**: All OCPP 1.6J core profile and firmware management messages

