# Future Enhancements

Items listed here are planned but deferred from the initial implementation. Remove items once they are implemented.

## Server Enrichment

- **IP Geolocation** — Resolve incoming IP addresses to country/region during ingestion. Requires integrating a geolocation database (e.g., MaxMind GeoLite2). The enriched event schema already includes `country` and `region` fields; the pipeline should geolocate then discard the raw IP. Until implemented, those fields will be null in enriched events.

## Database Providers

- **MySQL support** — via Pomelo.EntityFrameworkCore.MySql. Deferred until Pomelo ships a .NET 10-compatible package. PostgreSQL and SQL Server are available now.

## Admin UI

- **Event Stream page** — Live/paginated feed of recent enriched events with filtering and expandable payload detail rows.
