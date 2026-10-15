# Controllarr

An automated console ROM and game collection manager for Usenet and BitTorrent users, forked from the *arr stack.

## Core Features
- IGDB metadata integration
- No-Intro/Redump/TOSEC classification
- 1G1R collection management
- Full-set archive unpacking
- Opt-in chdman disc compression
- Dynamic network symlinks
- BIOS tracking
- Multi-device SD card export profiles (MiSTer, OnionOS, Analogue Pocket, EverDrive, EZ-Flash, FXPak Pro, R4)

## Tech Stack
- .NET 8 backend
- React/TypeScript frontend
- SQLite/PostgreSQL datastore

## Basic local setup instructions
```bash
dotnet build
yarn start
```
