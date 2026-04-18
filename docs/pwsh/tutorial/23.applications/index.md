---
layout: default
title: Applications
parent: Tutorials
nav_order: 23
has_children: true
---

# Applications

Walk through larger Kestrun samples that combine routing, shared state, authentication,
OpenAPI, packaging, and browser-facing clients into one application shape.

## Chapters

| # | Topic | Summary |
|---|-------|---------|
| 1 | [Bike Rental Shop Application](./1.BikeRentalShop) | Build a realistic rental workflow with persisted inventory, staff operations, optional CORS, and a separate Razor web client |
| 2 | [Bike Rental Shop Web Client](./2.BikeRentalShop-Web) | Connect a standalone Razor Pages frontend to the rental API and keep browser concerns out of the backend |
| 3 | [Bike Rental Shop Backend Variants](./3.BikeRentalShop-Variants) | Compare the synchronized and concurrent backends while keeping the same API contract and packaging story |
| 4 | [Bike Rental Shop Packaging](./4.BikeRentalShop-Packaging) | Package the backend or web client into a `.krpack` and keep `data/` and `logs/` declared for deployment-safe upgrades |
| 5 | [Bike Rental Shop Install and Update](./5.BikeRentalShop-Install-Update) | Turn a packaged BikeRentalShop target into concrete `service install` and `service update` commands for deployment |

Return to the [Tutorial Index](../index).
