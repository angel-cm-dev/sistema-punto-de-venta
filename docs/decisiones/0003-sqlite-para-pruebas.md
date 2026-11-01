# ADR-0003: SQLite en memoria para pruebas de integración y desarrollo local

- **Estado:** aceptada
- **Fecha:** 2026-11-01

## Contexto

Las pruebas deben ejecutarse en cualquier máquina y en la CI **sin Docker**. EF Core InMemory no es relacional: no soporta transacciones, `ExecuteUpdate`, restricciones `CHECK` ni índices únicos, justamente lo que se quiere probar (stock atómico, rollback, correlativos únicos).

## Decisión

- Las pruebas de integración levantan la API real con `WebApplicationFactory<Program>` y `Database:Provider=Sqlite` sobre una base **en memoria compartida** (`Mode=Memory;Cache=Shared`), mantenida viva por una conexión ancla durante la sesión de pruebas.
- El modo `Development` usa SQLite en archivo, para que la API corra con `dotnet run` sin instalar PostgreSQL.
- Producción y docker-compose usan PostgreSQL con migraciones versionadas.
- Para SQLite, los `decimal` se mapean a `REAL` (SQLite no suma ni ordena `decimal` en SQL). PostgreSQL mantiene `numeric(12,2)` exacto.

## Consecuencias

- ✅ `dotnet test` corre en segundos, sin servicios externos, y prueba transacciones y restricciones reales.
- ✅ Los datos demo se siembran igual que en producción, por lo que las pruebas ejercitan el seed.
- ⚠️ SQLite no reproduce el bloqueo de filas concurrente de PostgreSQL; la concurrencia real queda cubierta por diseño (ADR-0001) y se planea una suite con Testcontainers (ver Roadmap).
- ⚠️ El esquema SQLite se crea con `EnsureCreated`, no con las migraciones (que son específicas de PostgreSQL).
